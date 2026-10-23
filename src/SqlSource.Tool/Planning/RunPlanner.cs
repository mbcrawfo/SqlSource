using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.MSBuild;
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
/// The plan must agree with the generator on which type claims which file with which output, so every rule here is
/// the generator's own code: <c>PathResolver</c>, <c>MSBuildSettings</c>, <c>DialectSetting</c>,
/// <c>FileParseInput</c>, <c>SqlFileReader</c>, <c>QuerySettings</c> and <c>SqlQueryHash</c>.  The order of the
/// errors for values that are not valid is the generator's too: both sides take it from <c>InvalidValues</c>.
/// <c>GeneratorParityTests</c> holds the two together.
/// </remarks>
internal static class RunPlanner
{
    public static RunPlanResult Plan(
        ImmutableArray<ProjectManifest> manifests,
        RunFilters filters,
        CancellationToken cancellationToken
    )
    {
        var errors = ImmutableArray.CreateBuilder<ToolDiagnostic>();
        var unclaimed = ImmutableArray.CreateBuilder<int>(manifests.Length);

        // A file that two projects claim is planned once, under the first.
        var works = new List<FileWork>();
        var byPath = new Dictionary<string, FileWork>(SqlPath.Comparer);
        foreach (var manifest in manifests)
        {
            var (claimedFiles, unclaimedCount) = ReadProject(manifest, errors, cancellationToken);
            unclaimed.Add(unclaimedCount);
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
            new EquatableArray<int>(unclaimed.MoveToImmutable())
        );
    }

    // The claimed files of one project, in the order of their paths, each with the settings of this project, and the
    // number of its listed files that no type of it claims.  The errors of the project are added: SQLSRC208, then
    // SQLSRC011, then SQLSRC014.
    private static (List<ClaimedFile> Claimed, int Unclaimed) ReadProject(
        ProjectManifest manifest,
        ImmutableArray<ToolDiagnostic>.Builder errors,
        CancellationToken cancellationToken
    )
    {
        var claims = AttributeReader.Read(manifest, cancellationToken);
        errors.AddRange(claims.Errors);

        var options = ManifestOptions.ForProject(manifest);
        var dialect = DialectSetting.ReadProperty(options);
        // The keys of a manifest: they have the database, which the keys of the compiler do not.
        var settings = ProjectSettings.Read(options, MSBuildSettings.ManifestProperty);
        var invalid = new InvalidValues();
        invalid.AddDialect(dialect.InvalidValue);
        AddSettings(invalid, settings.Invalid);

        var listed = ListedFiles.Read(manifest);
        var claimsOf = ClaimsByFile(claims.Claims, listed);

        // The metadata of a file that no type claims is not read, as nothing else of such a file is.
        var claimed = new List<ClaimedFile>(claimsOf.Count);
        foreach (var path in listed.Paths)
        {
            if (!claimsOf.TryGetValue(path, out var fileClaims))
            {
                continue;
            }

            var text = new SqlFileText(listed[path].Path);
            claimed.Add(
                new ClaimedFile(
                    manifest.ProjectPath,
                    path,
                    text,
                    ReadMetadata(text, listed[path], invalid),
                    dialect,
                    settings.Level,
                    fileClaims.Levels,
                    fileClaims.Types
                )
            );
        }

        AddInvalid(errors, manifest.ProjectPath, invalid);
        return (claimed, listed.Paths.Count - claimed.Count);
    }

    // The claims of each claimed file, by its path.  What a claim of a file says is its Output, and nothing else.
    private static Dictionary<string, FileClaims> ClaimsByFile(ImmutableArray<TypeClaim> claims, ListedFiles listed)
    {
        var byFile = new Dictionary<string, FileClaims>(SqlPath.Comparer);
        foreach (var claim in claims)
        {
            var level = claim.Output is { } output ? new SettingsLevel { Output = output } : SettingsLevel.None;
            foreach (var path in listed.ClaimedBy(claim))
            {
                if (!byFile.TryGetValue(path, out var fileClaims))
                {
                    fileClaims = new FileClaims();
                    byFile.Add(path, fileClaims);
                }

                fileClaims.Levels.Add(level);
                fileClaims.Types.Add(claim);
            }
        }

        return byFile;
    }

    // The metadata of a claimed file's item.  What is not valid in it is kept for the errors of the project.
    private static FileMetadata ReadMetadata(SqlFileText text, ManifestFile file, InvalidValues invalid)
    {
        var metadata = FileMetadata.Read(text, ManifestOptions.ForFile(file), MSBuildSettings.ManifestMetadata);
        invalid.AddDialect(metadata.Dialect.InvalidValue);
        if (metadata.Settings is { } settings)
        {
            AddSettings(invalid, settings.Invalid);
        }

        return metadata;
    }

    // Plans a claimed file once, under the first project that claims it.  A file with a parse error has no query.  When
    // the compiler would read the file in another encoding the errors may be of the tool's reading, so SQLSRC271 is
    // reported in their place.
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

    // Parses a claimed file with the dialect of the project it is planned under.  Comments are never wanted: the
    // hash comes from the SQL without them.
    private static ParsedSqlFile Parse(ClaimedFile owner, CancellationToken cancellationToken)
    {
        var input = FileParseInput.Resolve(
            owner.Metadata,
            owner.ProjectDialect,
            projectKeepsComments: false,
            EquatableArray<string>.Empty
        );
        return SqlFileReader.Read(
            owner.Metadata.File,
            owner.NormalizedPath,
            input.Dialect,
            input.InvalidDialect,
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
        var output = GreatestOutput(query, work);
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
            QuerySettings.ResolveDatabase(
                query.Markers,
                owner.Metadata.Settings?.Level ?? SettingsLevel.None,
                owner.Property,
                SqlDialectName.Canonical(dialect)
            ),
            SqlQueryHash.Compute(dialect, query.Segments, query.Tokens, query.Parameters),
            problems
        );
    }

    private static ToolDiagnostic FileNotUtf8(FileWork work) =>
        ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, work.Owner.Metadata.File.Path);

    // The planned file of a work, under its owner.
    private static PlannedFile FileOf(
        FileWork work,
        SqlDialect dialect,
        PlannedFileState state,
        EquatableArray<PlannedQuery> queries
    ) =>
        new(
            work.Owner.Metadata.File.Path,
            work.Owner.NormalizedPath,
            work.Owner.ProjectPath,
            dialect,
            state,
            queries,
            ClaimsOf(work)
        );

    // Who claims the file, for the log: every claim of every project, in their order.
    private static EquatableArray<FileClaim> ClaimsOf(FileWork work) =>
        new([
            .. work.Claimants.SelectMany(claimant =>
                claimant.Types.Select(type => new FileClaim(claimant.ProjectPath, type.SourcePath, type.Position))
            ),
        ]);

    private static ContinuationLine HelpForToken(string name) =>
        new(
            "help",
            $"write the token with a default, {{{{{name}:default}}}}, or give one in a marker of the query: "
                + $"-- token: {{{{{name}:default}}}}"
        );

    // The output resolves for each type and query, and a file needs what any type that claims it needs: the
    // greatest over every claim of every project, each with the metadata and the property of its own project.
    private static OutputKind GreatestOutput(SqlQuery query, FileWork work)
    {
        var greatest = OutputKind.Sql;
        foreach (var claimant in work.Claimants)
        {
            var metadata = claimant.Metadata.Settings?.Level ?? SettingsLevel.None;
            foreach (var claim in claimant.Claims)
            {
                var output = QuerySettings.Resolve(query.Markers, claim, metadata, claimant.Property).Output;
                if (output > greatest)
                {
                    greatest = output;
                }
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
                    .. file.Queries.Select(query => query with { IsSelected = isNamed && IsNamed(query, filters) }),
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

    // The generator's reader gives every setting that is not valid.  The tool reports the ones it reads: the build
    // reports the rest.  The database is the tool's alone: the generator does not read it.
    private static void AddSettings(InvalidValues invalid, IEnumerable<InvalidSetting> settings) =>
        invalid.AddSettings(
            settings.Where(static setting => setting.Name is MSBuildSettings.OutputName or MSBuildSettings.DatabaseName)
        );

    // SQLSRC011 for each dialect that is not valid, then SQLSRC014 for each setting, in the order of InvalidValues.
    private static void AddInvalid(
        ImmutableArray<ToolDiagnostic>.Builder errors,
        string projectPath,
        InvalidValues invalid
    )
    {
        foreach (var value in invalid.Dialects)
        {
            errors.Add(ToolDiagnostic.AtFile(SqlDiagnostics.InvalidDialect, projectPath, value));
        }

        foreach (var setting in invalid.Settings)
        {
            errors.Add(
                ToolDiagnostic.AtFile(
                    SqlDiagnostics.InvalidSettingValue,
                    projectPath,
                    setting.Value,
                    setting.Name,
                    setting.Help
                )
            );
        }
    }

    // The claims of one file in one project: what each says, and the type that says it, in the order they came.
    private sealed class FileClaims
    {
        public List<SettingsLevel> Levels { get; } = [];

        public List<TypeClaim> Types { get; } = [];
    }

    // One claimed file as one project sees it.
    private sealed record ClaimedFile(
        string ProjectPath,
        string NormalizedPath,
        SqlFileText Text,
        FileMetadata Metadata,
        DialectSetting ProjectDialect,
        SettingsLevel Property,
        List<SettingsLevel> Claims,
        List<TypeClaim> Types
    );

    // One claimed file of the run, with every project that claims it.  The first is the one it is planned under.
    private sealed class FileWork(ClaimedFile owner)
    {
        public ClaimedFile Owner => owner;

        public List<ClaimedFile> Claimants { get; } = [owner];
    }
}
