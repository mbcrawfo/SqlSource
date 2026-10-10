using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SqlSource.Diagnostics;
using SqlSource.Generation;
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
/// <c>FileParseInput</c>, <c>SqlFileReader</c>, <c>QuerySettings</c> and <c>SqlQueryHash</c>.
/// <c>GeneratorParityTests</c> holds the two together.
/// </remarks>
internal static class RunPlanner
{
    /// <summary>The setting that the tool reads and the generator does not.</summary>
    public const string DatabaseSetting = "SqlSourceDatabase";

    public static RunPlanResult Plan(ImmutableArray<ProjectManifest> manifests, CancellationToken cancellationToken)
    {
        var errors = ImmutableArray.CreateBuilder<ToolDiagnostic>();

        // A file that two projects claim is planned once, under the first.
        var works = new List<FileWork>();
        var byPath = new Dictionary<string, FileWork>(SqlPath.Comparer);
        foreach (var manifest in manifests)
        {
            foreach (var claimed in ReadProject(manifest, errors))
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

        return new RunPlanResult(
            new RunPlan(new EquatableArray<PlannedFile>([.. files]), EquatableArray<PlannedDatabase>.Empty),
            new EquatableArray<ToolDiagnostic>(errors.ToImmutable())
        );
    }

    // The claimed files of one project, in the order of their paths, each with the settings of this project.  The
    // errors of the project are added: SQLSRC208, then SQLSRC011, then SQLSRC014.
    private static List<ClaimedFile> ReadProject(
        ProjectManifest manifest,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        var claims = AttributeReader.Read(manifest);
        errors.AddRange(claims.Errors);

        var options = ManifestOptions.ForProject(manifest);
        var dialect = DialectSetting.ReadProperty(options);
        var settings = ProjectSettings.Read(options);

        // Each value once, in ordinal order, as the generator reports them.
        var invalidDialects = new SortedSet<string>(StringComparer.Ordinal);
        var invalidSettings = new HashSet<InvalidSetting>(settings.Invalid.Where(IsRead));
        if (dialect.InvalidValue is { } invalidProperty)
        {
            _ = invalidDialects.Add(invalidProperty);
        }

        var database = ReadDatabase(manifest.Properties, invalidSettings);

        // What each claim of a file says: its Output, and nothing else.
        var listed = ListedFiles.Read(manifest);
        var claimsOf = new Dictionary<string, List<SettingsLevel>>(SqlPath.Comparer);
        foreach (var claim in claims.Claims)
        {
            var level = claim.Output is { } output ? new SettingsLevel { Output = output } : SettingsLevel.None;
            foreach (var path in listed.ClaimedBy(claim))
            {
                if (!claimsOf.TryGetValue(path, out var levels))
                {
                    levels = [];
                    claimsOf.Add(path, levels);
                }

                levels.Add(level);
            }
        }

        // The metadata of a file that no type claims is not read, as nothing else of such a file is.
        var claimed = new List<ClaimedFile>(claimsOf.Count);
        foreach (var path in listed.Paths)
        {
            if (!claimsOf.TryGetValue(path, out var levels))
            {
                continue;
            }

            var file = listed[path];
            var metadata = FileMetadata.Read(new SqlFileText(file.Path), ManifestOptions.ForFile(file));
            if (metadata.Dialect.InvalidValue is { } invalidMetadata)
            {
                _ = invalidDialects.Add(invalidMetadata);
            }

            if (metadata.Settings is { } fileSettings)
            {
                invalidSettings.UnionWith(fileSettings.Invalid.Where(IsRead));
            }

            claimed.Add(
                new ClaimedFile(
                    manifest.ProjectPath,
                    path,
                    metadata,
                    dialect,
                    settings.Level,
                    ReadDatabase(file.Metadata, invalidSettings) ?? database,
                    levels
                )
            );
        }

        foreach (var value in invalidDialects)
        {
            errors.Add(ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, manifest.ProjectPath, value));
        }

        foreach (
            var setting in invalidSettings
                .OrderBy(static setting => setting.Name, StringComparer.Ordinal)
                .ThenBy(static setting => setting.Value, StringComparer.Ordinal)
        )
        {
            errors.Add(
                ToolDiagnostic.ForFile(
                    SqlDiagnostics.InvalidSettingValue,
                    manifest.ProjectPath,
                    setting.Value,
                    setting.Name
                )
            );
        }

        return claimed;
    }

    // The generator's reader gives every setting that is not valid.  The tool reports the ones it reads: the build
    // reports the rest.
    private static bool IsRead(InvalidSetting setting) => setting.Name == MSBuildSettings.OutputName;

    private static string? ReadDatabase(ImmutableDictionary<string, string> values, HashSet<InvalidSetting> invalid)
    {
        if (!values.TryGetValue(DatabaseSetting, out var written) || string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var value = written.Trim();
        if (SettingValue.IsDatabaseName(value.AsSpan()))
        {
            return value;
        }

        _ = invalid.Add(new InvalidSetting(DatabaseSetting, value));
        return null;
    }

    // Parses a claimed file once, with the dialect of the project it is planned under, and gives each query its
    // values.  Comments are never wanted: the hash comes from the SQL without them.
    private static PlannedFile PlanFile(
        FileWork work,
        ImmutableArray<ToolDiagnostic>.Builder errors,
        CancellationToken cancellationToken
    )
    {
        var owner = work.Owner;
        var input = FileParseInput.Resolve(
            owner.Metadata,
            owner.ProjectDialect,
            projectKeepsComments: false,
            EquatableArray<string>.Empty
        );
        var parsed = SqlFileReader.Read(
            owner.Metadata.File,
            owner.NormalizedPath,
            input.Dialect,
            input.InvalidDialect,
            commentsWanted: false,
            cancellationToken
        );

        if (parsed.Errors.Count > 0)
        {
            errors.AddRange(parsed.Errors.Select(ToolDiagnostic.From));
            return new PlannedFile(
                owner.Metadata.File.Path,
                owner.NormalizedPath,
                owner.ProjectPath,
                parsed.Dialect,
                PlannedFileState.HasParseErrors,
                EquatableArray<PlannedQuery>.Empty
            );
        }

        var queries = ImmutableArray.CreateBuilder<PlannedQuery>(parsed.Queries.Count);
        foreach (var query in parsed.Queries)
        {
            var output = GreatestOutput(query, work);
            if (output == OutputKind.Sql)
            {
                // It needs no entry, and so belongs to no database and has no hash.
                queries.Add(new PlannedQuery(query, NeedsEntry: false, Database: null, Hash: null));
                continue;
            }

            queries.Add(
                new PlannedQuery(
                    query,
                    NeedsEntry: true,
                    query.Markers.Database ?? owner.Database ?? SqlDialectName.Canonical(parsed.Dialect),
                    SqlQueryHash.Compute(parsed.Dialect, query.Segments, query.Tokens, query.Parameters)
                )
            );
        }

        return new PlannedFile(
            owner.Metadata.File.Path,
            owner.NormalizedPath,
            owner.ProjectPath,
            parsed.Dialect,
            PlannedFileState.Ready,
            new EquatableArray<PlannedQuery>(queries.MoveToImmutable())
        );
    }

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

    // One claimed file as one project sees it.
    private sealed record ClaimedFile(
        string ProjectPath,
        string NormalizedPath,
        FileMetadata Metadata,
        DialectSetting ProjectDialect,
        SettingsLevel Property,
        string? Database,
        List<SettingsLevel> Claims
    );

    // One claimed file of the run, with every project that claims it.  The first is the one it is planned under.
    private sealed class FileWork(ClaimedFile owner)
    {
        public ClaimedFile Owner => owner;

        public List<ClaimedFile> Claimants { get; } = [owner];
    }
}
