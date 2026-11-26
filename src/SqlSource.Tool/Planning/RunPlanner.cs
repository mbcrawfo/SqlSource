using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Makes the plan of a run from the manifests of its projects: which files are claimed, and what each query of them
/// needs.  It reports nothing: what is wrong is in the result, in the order it is to be reported.
/// </summary>
/// <remarks>
/// The plan must agree with the generator on which type claims which file, and on which of its queries need an entry,
/// so every rule here is the generator's own code: <c>PathResolver</c>, <c>FileParseInput</c>, <c>SqlFileReader</c>,
/// <c>FileLevel</c>, <c>QuerySettings</c> and <c>SqlQueryHash</c>.  The settings come from the settings files alone,
/// which the generator's <c>ListedSettingsFile</c> and <c>SettingsFileSet</c> read, through
/// <see cref="RunSettingsFiles" />, and the compile paths that reach one are <c>BuildFolders</c>'.
/// <c>GeneratorParityTests</c> holds the two together.
/// </remarks>
internal static class RunPlanner
{
    /// <summary>
    /// The plan of the manifests of the run's projects, which the filters narrow.
    /// </summary>
    /// <param name="manifests">The manifests of the run's projects, a project's frameworks one after another.</param>
    /// <param name="filters">What the run is restricted to.</param>
    /// <param name="cancellationToken">Ends the plan.</param>
    /// <param name="others">
    /// The manifests of the projects of the solution that <c>--project</c> leaves out.  They add what they need of a
    /// file that a project of the run claims, so that its sidecar keeps their entries, and nothing else: no file that
    /// only they claim, no error of their own claims or settings files, no count of unclaimed files, and no query that
    /// only they need is selected.  An error of the run's file under their needs is the file's, and is reported.
    /// </param>
    public static RunPlanResult Plan(
        ImmutableArray<ProjectManifest> manifests,
        RunFilters filters,
        CancellationToken cancellationToken,
        ImmutableArray<ProjectManifest> others = default
    )
    {
        var errors = ImmutableArray.CreateBuilder<ToolDiagnostic>();
        var settingsFiles = new RunSettingsFiles();

        // The manifests of one project are consecutive, a framework each.  A file that two of them or two projects
        // claim is planned once, under the first.
        var projects = new List<ProjectWork>();
        var projectsByPath = new Dictionary<string, ProjectWork>(StringComparer.Ordinal);
        var works = new List<FileWork>();
        var byPath = new Dictionary<string, FileWork>(SqlPath.Comparer);
        foreach (var manifest in manifests)
        {
            if (!projectsByPath.TryGetValue(manifest.ProjectPath, out var project))
            {
                project = new ProjectWork(manifest.ProjectPath);
                projectsByPath.Add(manifest.ProjectPath, project);
                projects.Add(project);
            }

            var claimedFiles = ReadProject(manifest, project, settingsFiles, errors, cancellationToken);
            foreach (var claimed in claimedFiles)
            {
                if (byPath.TryGetValue(claimed.NormalizedPath, out var work))
                {
                    work.Claimants.Add(claimed);
                }
                else
                {
                    work = new FileWork(claimed);
                    byPath.Add(claimed.NormalizedPath, work);
                    works.Add(work);
                }
            }
        }

        // A project that --project leaves out claims a file of the run alongside its projects, after them, so the file
        // is still planned under a project of the run.
        foreach (var manifest in others.IsDefault ? [] : others)
        {
            if (byPath.Count == 0)
            {
                break;
            }

            foreach (var claimed in ReadOther(manifest, settingsFiles, byPath, cancellationToken))
            {
                byPath[claimed.NormalizedPath].Claimants.Add(claimed);
            }
        }

        var files = new List<PlannedFile>(works.Count);
        foreach (var work in works)
        {
            files.Add(PlanFile(work, errors, cancellationToken));
        }

        var databases = AssignDatabases(files, errors);
        Select(files, filters, errors);

        return new RunPlanResult(
            new RunPlan(new EquatableArray<PlannedFile>([.. files]), databases, filters),
            new EquatableArray<ToolDiagnostic>(errors.ToImmutable()),
            new EquatableArray<(string ProjectPath, int Files)>([
                .. projects.Select(static project => (project.ProjectPath, project.Unclaimed)),
            ])
        );
    }

    // The claimed files of one manifest, in the order of their paths, each with the settings files that reach it.  The
    // project learns which of its files are listed and which claimed.  The errors are added: those of reading the
    // attributes, SQLSRC208 and SQLSRC271, once for the project when another framework compiles the same file, then
    // the problems of the settings files that its .sql files and C# files reach, as the generator reports them, of
    // each file once in the run.
    private static List<ClaimedFile> ReadProject(
        ProjectManifest manifest,
        ProjectWork project,
        RunSettingsFiles settingsFiles,
        ImmutableArray<ToolDiagnostic>.Builder errors,
        CancellationToken cancellationToken
    )
    {
        var claims = AttributeReader.Read(manifest, cancellationToken);
        errors.AddRange(claims.Errors.Where(project.Reports));

        var settingsFileSet = settingsFiles.Of(manifest, cancellationToken);
        var listed = ListedFiles.Read(manifest);
        var claimsOf = ClaimsByFile(claims.Claims, listed);

        // A file that no type claims is not read, as nothing else of such a file is.
        var claimed = new List<ClaimedFile>(claimsOf.Count);
        foreach (var path in listed.Paths)
        {
            _ = project.Listed.Add(path);
            if (!claimsOf.TryGetValue(path, out var types))
            {
                continue;
            }

            _ = project.Claimed.Add(path);
            claimed.Add(ClaimedFileOf(manifest, path, listed, settingsFileSet, types, isInRun: true));
        }

        // Every listed .sql file reaches, claimed or not, as every C# file does that is not the build's.
        var buildFolders = BuildFolders.Of(manifest.BuildFolders);
        settingsFiles.AddProblems(
            settingsFileSet,
            listed.Paths.Concat(buildFolders.ReachingPaths(manifest.CompileFiles)),
            errors
        );
        return claimed;
    }

    // The files of the run that a project left out by --project claims too, each with the settings files that reach it
    // in that project.  Nothing of that project is reported: its errors are its own build's.
    private static List<ClaimedFile> ReadOther(
        ProjectManifest manifest,
        RunSettingsFiles settingsFiles,
        Dictionary<string, FileWork> planned,
        CancellationToken cancellationToken
    )
    {
        var listed = ListedFiles.Read(manifest);
        if (!listed.Paths.Any(planned.ContainsKey))
        {
            return [];
        }

        var claimsOf = ClaimsByFile(AttributeReader.Read(manifest, cancellationToken).Claims, listed);
        var settingsFileSet = settingsFiles.Of(manifest, cancellationToken);
        return
        [
            .. listed
                .Paths.Where(planned.ContainsKey)
                .Select(path =>
                    claimsOf.TryGetValue(path, out var types)
                        ? ClaimedFileOf(manifest, path, listed, settingsFileSet, types, isInRun: false)
                        : null
                )
                .OfType<ClaimedFile>(),
        ];
    }

    // One claimed file as one project sees it, with the chain of the file's folder in the project's settings files, as
    // the generator finds it.
    private static ClaimedFile ClaimedFileOf(
        ProjectManifest manifest,
        string path,
        ListedFiles listed,
        SettingsFileSet settingsFileSet,
        List<TypeClaim> types,
        bool isInRun
    )
    {
        var chain = settingsFileSet.ChainOf(path);
        return new ClaimedFile(
            manifest.ProjectPath,
            path,
            new SqlFileText(listed[path]),
            chain,
            FileLevel.Resolve(path, chain)?.Level ?? SettingsLevel.None,
            types,
            isInRun
        );
    }

    // The claims of each claimed file, by its path, in the order they came.  A claim says nothing of what its files
    // need: their markers and the settings files do.
    private static Dictionary<string, List<TypeClaim>> ClaimsByFile(
        ImmutableArray<TypeClaim> claims,
        ListedFiles listed
    )
    {
        var byFile = new Dictionary<string, List<TypeClaim>>(SqlPath.Comparer);
        foreach (var claim in claims)
        {
            foreach (var path in listed.ClaimedBy(claim))
            {
                if (!byFile.TryGetValue(path, out var types))
                {
                    types = [];
                    byFile.Add(path, types);
                }

                types.Add(claim);
            }
        }

        return byFile;
    }

    // Plans a claimed file once, under the first project that claims it.  A file with a parse error has no query.  When
    // the compiler would read the file in its fallback encoding, another one on Windows, the errors may be of the
    // tool's reading alone, so SQLSRC271 is reported in their place.
    private static PlannedFile PlanFile(
        FileWork work,
        ImmutableArray<ToolDiagnostic>.Builder errors,
        CancellationToken cancellationToken
    )
    {
        // A run that was cancelled reads no further file, and reports nothing of what it read.
        cancellationToken.ThrowIfCancellationRequested();

        var parsed = Parse(work.Owner, cancellationToken);
        if (parsed.Errors.Count > 0)
        {
            if (work.Owner.Text.CompilerFallsBack)
            {
                errors.Add(FileNotUtf8(work));
                return FileOf(work, parsed.Dialect, PlannedFileState.NotUtf8, EquatableArray<PlannedQuery>.Empty);
            }

            errors.AddRange(parsed.Errors.Select(ToolDiagnostic.From));
            return FileOf(work, parsed.Dialect, PlannedFileState.HasParseErrors, EquatableArray<PlannedQuery>.Empty);
        }

        var state = PlannedFileState.Ready;
        var queries = ImmutableArray.CreateBuilder<PlannedQuery>(parsed.Queries.Count);
        foreach (var query in parsed.Queries)
        {
            queries.Add(PlanQuery(query, work, parsed.Dialect, ref state, errors));
        }

        return FileOf(work, parsed.Dialect, state, new EquatableArray<PlannedQuery>(queries.MoveToImmutable()));
    }

    // Parses a claimed file with its dialect: its header marker, else the chain's, else ansi.  Comments are never
    // wanted: the hash comes from the SQL without them.
    private static ParsedSqlFile Parse(ClaimedFile owner, CancellationToken cancellationToken)
    {
        var input = FileParseInput.Resolve(owner.Text, owner.NormalizedPath, owner.Chain);
        return SqlFileReader.Read(
            owner.Text,
            owner.NormalizedPath,
            input.Dialect,
            commentsWanted: false,
            cancellationToken
        );
    }

    // One query with its values, and its errors.  The first query of a file that needs an entry changes the state of
    // the file when the file is not saved as UTF-8, or else when its dialect cannot be described.
    private static PlannedQuery PlanQuery(
        SqlQuery query,
        FileWork work,
        SqlDialect dialect,
        ref PlannedFileState state,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        var output = GreatestOutput(query, work, inRunOnly: false);
        if (output == OutputKind.Sql)
        {
            // It needs no entry, and so belongs to no database and has no hash.
            return new PlannedQuery(query, NeedsEntry: false, Database: null, Hash: null);
        }

        // Once for the file, at its first query that needs an entry.  A file that is not UTF-8 is that error alone,
        // as saving it mends it.  The dialect is one setting, and the parser keeps no position for the marker that
        // set the output.
        if (state == PlannedFileState.Ready && work.Owner.Text.CompilerFallsBack)
        {
            state = PlannedFileState.NotUtf8;
            errors.Add(FileNotUtf8(work));
        }
        else if (state == PlannedFileState.Ready && !SqlDescribableDialects.Contains(dialect))
        {
            state = PlannedFileState.NotDescribable;
            errors.Add(
                ToolDiagnostic.At(
                    ToolDiagnostics.OutputNeedsDescribableDialect,
                    query.NameLocation,
                    SettingValue.NameOf(output),
                    SqlDialectName.Canonical(dialect)
                )
            );
        }

        var problems = QueryProblems.None;
        // An empty default is one: the query is described with nothing in the token's place.
        foreach (
            var token in query.Tokens.Where(static token => token.Default is null).Select(static token => token.Name)
        )
        {
            problems |= QueryProblems.TokenWithoutDefault;
            errors.Add(
                ToolDiagnostic
                    .At(ToolDiagnostics.TokenHasNoDefault, query.NameLocation, token, SettingValue.NameOf(output))
                    .WithLines(HelpForToken(token))
            );
        }

        var owner = work.Owner;
        return new PlannedQuery(
            query,
            NeedsEntry: true,
            QuerySettings.ResolveDatabase(query.Markers, owner.Level, SqlDialectName.Canonical(dialect)),
            SqlQueryHash.Compute(dialect, query.Segments, query.Tokens, query.Parameters, query.MarkedVariables),
            problems,
            // A query that only a project left out by --project needs is kept when it is current, and is not described.
            IsSelected: GreatestOutput(query, work, inRunOnly: true) != OutputKind.Sql
        );
    }

    private static ToolDiagnostic FileNotUtf8(FileWork work) =>
        ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, work.Owner.Text.Path);

    // The planned file of a work, under its owner.
    private static PlannedFile FileOf(
        FileWork work,
        SqlDialect dialect,
        PlannedFileState state,
        EquatableArray<PlannedQuery> queries
    ) =>
        new(
            work.Owner.Text.Path,
            work.Owner.NormalizedPath,
            work.Owner.ProjectPath,
            dialect,
            state,
            queries,
            ClaimsOf(work)
        );

    // Who claims the file, for the log: every claim of every project, in their order, once each when several
    // frameworks of a project make it.
    private static EquatableArray<FileClaim> ClaimsOf(FileWork work) =>
        new([
            .. work
                .Claimants.SelectMany(claimant =>
                    claimant.Types.Select(type => new FileClaim(claimant.ProjectPath, type.SourcePath, type.Position))
                )
                .Distinct(),
        ]);

    private static ContinuationLine HelpForToken(string name) =>
        new(
            "help",
            $"write the token with a default, {{{{{name}:default}}}}, or give one in a marker of the query: "
                + $"-- token: {{{{{name}:default}}}}"
        );

    // The output resolves for each query from its markers and its file's chain, which every type of one project
    // shares, and a file needs what any project that claims it needs: the greatest over every project, each with the
    // chain of its own settings files.  With inRunOnly, over the projects of the run alone.
    private static OutputKind GreatestOutput(SqlQuery query, FileWork work, bool inRunOnly)
    {
        var greatest = OutputKind.Sql;
        foreach (var claimant in work.Claimants.Where(claimant => !inRunOnly || claimant.IsInRun))
        {
            var output = QuerySettings.Resolve(query.Markers, claimant.Level).Output;
            if (output > greatest)
            {
                greatest = output;
            }
        }

        return greatest;
    }

    // The databases of the run, in the order the plan first holds a query of each.  Names are compared ignoring
    // case, and every query gets the first spelling.  A database has the dialect of its first query in a ready
    // file, and a file that is not ready neither decides it nor conflicts with it: SQLSRC209, SQLSRC271 or a parse
    // error is that file's error.  A database that no ready file holds keeps the dialect of its first file.  A
    // query of the database in a ready file of another dialect has a problem, and its file's first such query is
    // SQLSRC211.
    private static EquatableArray<PlannedDatabase> AssignDatabases(
        List<PlannedFile> files,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        var names = new List<string>();
        var known = new Dictionary<string, DatabaseWork>(DatabaseName.Comparer);
        foreach (var file in files)
        {
            foreach (var query in file.Queries)
            {
                if (query.Database is not { } name)
                {
                    continue;
                }

                if (!known.TryGetValue(name, out var database))
                {
                    database = new DatabaseWork(name, file.Dialect);
                    known.Add(name, database);
                    names.Add(name);
                }

                if (database.File is null && file.State == PlannedFileState.Ready)
                {
                    database.Dialect = file.Dialect;
                    database.File = file.Path;
                }
            }
        }

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            HashSet<string>? reported = null;
            var queries = ImmutableArray.CreateBuilder<PlannedQuery>(file.Queries.Count);
            foreach (var query in file.Queries)
            {
                if (query.Database is not { } name)
                {
                    queries.Add(query);
                    continue;
                }

                var database = known[name];
                var problems = query.Problems;
                if (
                    file.State == PlannedFileState.Ready
                    && database.File is { } firstFile
                    && database.Dialect != file.Dialect
                )
                {
                    problems |= QueryProblems.DatabaseDialectConflict;
                    if ((reported ??= [with(DatabaseName.Comparer)]).Add(name))
                    {
                        errors.Add(
                            ToolDiagnostic.At(
                                ToolDiagnostics.DatabaseHasTwoDialects,
                                query.Query.NameLocation,
                                database.Name,
                                SqlDialectName.Canonical(file.Dialect),
                                SqlDialectName.Canonical(database.Dialect),
                                firstFile
                            )
                        );
                    }
                }

                queries.Add(query with { Database = database.Name, Problems = problems });
            }

            files[index] = file with { Queries = new EquatableArray<PlannedQuery>(queries.MoveToImmutable()) };
        }

        return new EquatableArray<PlannedDatabase>([
            .. names.Select(name => new PlannedDatabase(known[name].Name, known[name].Dialect)),
        ]);
    }

    // A database while the plan is made.  File is the first file that can be described and holds a query of it.
    private sealed class DatabaseWork(string name, SqlDialect dialect)
    {
        public string Name => name;

        public SqlDialect Dialect { get; set; } = dialect;

        public string? File { get; set; }
    }

    // Says which queries the filters take.  A file filter that names no planned file, or no file at all, is SQLSRC212,
    // and the run goes on.  Under --database a query that needs no entry is not selected: it belongs to no database.
    private static void Select(
        List<PlannedFile> files,
        RunFilters filters,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        if (filters.IsEmpty)
        {
            return;
        }

        var named = new HashSet<string>(
            filters.Files.Select(static filter => filter.NormalizedPath).OfType<string>(),
            SqlPath.Comparer
        );
        var planned = new HashSet<string>(files.Select(static file => file.NormalizedPath), SqlPath.Comparer);
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var isNamed = filters.Files.IsEmpty || named.Contains(file.NormalizedPath);
            files[index] = file with
            {
                Queries = new EquatableArray<PlannedQuery>([
                    .. file.Queries.Select(query =>
                        query with
                        {
                            IsSelected = query.IsSelected && isNamed && IsNamed(query, filters),
                        }
                    ),
                ]),
            };
        }

        errors.AddRange(
            filters
                .Files.Where(filter => filter.NormalizedPath is not { } path || !planned.Contains(path))
                .Select(static filter => ToolDiagnostic.Create(ToolDiagnostics.FileNotInRun, filter.Named))
        );
    }

    private static bool IsNamed(PlannedQuery query, RunFilters filters) =>
        filters.Databases.IsEmpty || (query.Database is { } database && filters.NamesDatabase(database));

    // One project of the run while its manifests are read: the files any of its frameworks lists and the files any
    // claims, and the errors it has reported.
    private sealed class ProjectWork(string projectPath)
    {
        private readonly HashSet<ToolDiagnostic> _reported = [];

        public string ProjectPath => projectPath;

        public HashSet<string> Listed { get; } = [with(SqlPath.Comparer)];

        public HashSet<string> Claimed { get; } = [with(SqlPath.Comparer)];

        public int Unclaimed => Listed.Count(path => !Claimed.Contains(path));

        // Whether the error is new to the project.
        public bool Reports(ToolDiagnostic error) => _reported.Add(error);
    }

    // One claimed file as one project sees it.  Chain is that of the file's folder in the project's settings files, and
    // Level is the chain's, as the generator's FileLevel gives it.  IsInRun is false for a project that --project
    // leaves out.
    private sealed record ClaimedFile(
        string ProjectPath,
        string NormalizedPath,
        SqlFileText Text,
        SettingsChain Chain,
        SettingsLevel Level,
        List<TypeClaim> Types,
        bool IsInRun
    );

    // One claimed file of the run, with every project that claims it.  The first is the one it is planned under.
    private sealed class FileWork(ClaimedFile owner)
    {
        public ClaimedFile Owner => owner;

        public List<ClaimedFile> Claimants { get; } = [owner];
    }
}
