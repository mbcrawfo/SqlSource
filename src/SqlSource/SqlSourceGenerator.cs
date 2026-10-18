using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;
using SqlSource.Generation.Sidecars;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource;

/// <summary>
/// Generates C# source for SQL queries.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SqlSourceGenerator : IIncrementalGenerator
{
    // The generated code targets .NET 8 and later.  This method first appeared there, and a generated method calls
    // it to check an argument, so its presence is the test.
    private const string FloorType = "System.ArgumentException";

    private const string FloorMember = "ThrowIfNullOrWhiteSpace";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output =>
            output.AddSource(AttributeSource.HintName, SourceText.From(AttributeSource.Text, Encoding.UTF8))
        );

        var targetTypes = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                AttributeSource.AttributeMetadataName,
                static (node, _) => TargetTypeReader.IsCandidate(node),
                TargetTypeReader.Read
            )
            .Where(static type => type is not null)
            .Select(static (type, _) => type!)
            .WithTrackingName(TrackingNames.TargetTypes);

        var sqlFiles = context.AdditionalTextsProvider.Where(static file => SqlPath.IsSqlFile(file.Path));

        // The paths alone, so that resolving a type's Path does not depend on the text of any file.
        var listedPaths = sqlFiles
            .Select(static (file, _) => SqlFilePath.Create(file.Path))
            .Where(static path => path is not null)
            .Select(static (path, _) => path!)
            .Collect();

        var sqlPaths = listedPaths
            .Select(static (paths, _) => SqlPath.ToSortedSet(paths.Select(static path => path.NormalizedPath)))
            .WithTrackingName(TrackingNames.SqlPaths);

        // Two files whose paths differ only by case.  Almost always none, so this value almost never changes.
        var caseCollisions = listedPaths
            .Combine(sqlPaths)
            .Select(static (input, _) => PathCollision.Find(input.Left, input.Right))
            .WithTrackingName(TrackingNames.CaseCollisions);

        var isSupportedFramework = context
            .CompilationProvider.Select(
                static (compilation, _) =>
                    compilation.GetTypeByMetadataName(FloorType)?.GetMembers(FloorMember).IsEmpty == false
            )
            .WithTrackingName(TrackingNames.SupportedFramework);

        // The Npgsql the project references, which chooses the column of the base map.  Like the framework check, it
        // runs on every compilation and gives an equal value until a reference changes.
        var npgsqlProbe = context
            .CompilationProvider.Select(static (compilation, _) => NpgsqlProbe.Read(compilation))
            .WithTrackingName(TrackingNames.NpgsqlProbe);

        // The project's language version, which is the same value until the project's parse options change.
        var unsupportedLanguageVersion = context
            .ParseOptionsProvider.Select(static (options, _) => LanguageSupport.FindUnsupportedVersion(options))
            .WithTrackingName(TrackingNames.UnsupportedLanguageVersion);

        var typeFiles = targetTypes
            .Combine(sqlPaths)
            .Combine(isSupportedFramework.Combine(unsupportedLanguageVersion))
            .Select(
                static (input, _) =>
                    PathResolver.Resolve(input.Left.Left, input.Left.Right, input.Right.Left, input.Right.Right)
            )
            .WithTrackingName(TrackingNames.TypeFiles);

        var claimedPaths = typeFiles
            .SelectMany(static (type, _) => type.Files)
            .Collect()
            .Select(static (paths, _) => SqlPath.ToSortedSet(paths))
            .WithTrackingName(TrackingNames.ClaimedPaths);

        // The files of the types whose attribute asks for comments.  Almost always none, so this value almost never
        // changes.
        var commentPaths = typeFiles
            .SelectMany(
                static (type, _) => type.Type.Settings.KeepsComments ? type.Files : EquatableArray<string>.Empty
            )
            .Collect()
            .Select(static (paths, _) => SqlPath.ToSortedSet(paths))
            .WithTrackingName(TrackingNames.CommentPaths);

        // Reported for a file that a type claims only, as every other problem of a file is.
        context.RegisterSourceOutput(
            caseCollisions.Combine(claimedPaths),
            static (output, input) =>
            {
                foreach (
                    var collision in input.Left.Where(collision =>
                        SqlPath.Contains(input.Right, collision.NormalizedPath)
                    )
                )
                {
                    output.ReportDiagnostic(collision.ToDiagnostic());
                }
            }
        );

        // The project's dialect, which is the same value until the property itself changes.
        var projectDialect = context
            .AnalyzerConfigOptionsProvider.Select(
                static (options, _) => DialectSetting.ReadProperty(options.GlobalOptions)
            )
            .WithTrackingName(TrackingNames.ProjectDialect);

        // What the project's properties say about the settings, which is the same value until one of them changes.
        var projectSettings = context
            .AnalyzerConfigOptionsProvider.Select(static (options, _) => ProjectSettings.Read(options.GlobalOptions))
            .WithTrackingName(TrackingNames.ProjectSettings);

        // Whether the property's list asks for comments.  It is an input of the parse, so it is a value of its own
        // that changes only when the answer does.
        var projectKeepsComments = projectSettings.Select(static (settings, _) => settings.Level.KeepsComments);

        // What the metadata of each file's item says: its dialect and its settings.
        var fileMetadata = sqlFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (input, _) => FileMetadata.Read(input.Left, input.Right.GetOptions(input.Left)));

        // Stage one of the settings: each file with what its parse depends on.  It is resolved here, before the
        // parse, so that a change to a property parses only the files whose input it changes.
        var fileInputs = fileMetadata
            .Combine(projectDialect)
            .Combine(projectKeepsComments.Combine(commentPaths))
            .Select(
                static (input, _) =>
                    FileParseInput.Resolve(input.Left.Left, input.Left.Right, input.Right.Left, input.Right.Right)
            )
            .WithTrackingName(TrackingNames.FileParseInput);

        // A file that no type claims is never read.
        var parsedFiles = fileInputs
            .Combine(claimedPaths)
            .Select(
                static (input, cancellationToken) =>
                    input.Left.NormalizedPath is { } path && SqlPath.Contains(input.Right, path)
                        ? SqlFileReader.Read(input.Left, cancellationToken)
                        : null
            )
            .Where(static file => file is not null)
            .Select(static (file, _) => file!)
            .WithTrackingName(TrackingNames.ParsedFile)
            .Collect()
            .Select(static (files, _) => ToSorted(files, static file => file.NormalizedPath))
            .WithTrackingName(TrackingNames.ParsedFiles);

        // Reported from the collected files, so that a file the project lists twice is reported once.  A dialect that
        // is not valid is reported here too, once for each value: the compiler does not say where an MSBuild
        // property or the metadata of an item was set, so it has no position.
        context.RegisterSourceOutput(
            parsedFiles.Combine(projectDialect),
            static (output, input) =>
            {
                foreach (var error in input.Left.SelectMany(static file => file.Errors))
                {
                    output.ReportDiagnostic(error.ToDiagnostic());
                }

                foreach (var value in FindInvalidDialects(input.Left, input.Right))
                {
                    output.ReportDiagnostic(Diagnostic.Create(SqlDiagnostics.InvalidDialect, Location.None, value));
                }
            }
        );

        // The sidecars are an input of their own, so that an edit to one reads nothing that a .sql file feeds.  A
        // sidecar whose .sql file no type claims is never read, and no step after the read holds its text.
        var sidecars = context
            .AdditionalTextsProvider.Where(static file => SqlPath.IsSidecarFile(file.Path))
            .Combine(claimedPaths)
            .Select(static (input, _) => SidecarInput.Create(input.Left, input.Right))
            .WithTrackingName(TrackingNames.SidecarInput)
            .Where(static input => input is not null)
            .Select(static (input, cancellationToken) => SidecarFileReader.Read(input!, cancellationToken))
            .WithTrackingName(TrackingNames.SidecarFile)
            .Collect()
            .Select(static (files, _) => ToSortedSidecars(files))
            .WithTrackingName(TrackingNames.Sidecars);

        // Stage two of the settings: the metadata of the files that have any, which are few.  It never reaches the
        // parse of a file.
        var filesSettings = fileMetadata
            .Select(static (metadata, _) => metadata.Settings)
            .Where(static settings => settings is not null)
            .Select(static (settings, _) => settings!)
            .WithTrackingName(TrackingNames.FileSettings)
            .Collect()
            .Select(static (settings, _) => ToSorted(settings, static file => file.NormalizedPath))
            .WithTrackingName(TrackingNames.FilesSettings);

        // Not located, and once for each setting and value.  The metadata of a file that no type claims is not
        // reported, as nothing else about such a file is.
        context.RegisterSourceOutput(
            projectSettings.Combine(filesSettings).Combine(claimedPaths),
            static (output, input) =>
            {
                foreach (var setting in FindInvalidSettings(input.Left.Left, input.Left.Right, input.Right))
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(
                            SqlDiagnostics.InvalidSettingValue,
                            Location.None,
                            setting.Value,
                            setting.Name
                        )
                    );
                }
            }
        );

        // Two types whose files would have names equal ignoring case.  Almost always none, so this value almost
        // never changes and costs the steps after it nothing.
        var ambiguousHintNames = typeFiles
            .Select(static (type, _) => HintName.Create(type.Type))
            .Collect()
            .Select(static (names, _) => HintName.FindAmbiguous(names))
            .WithTrackingName(TrackingNames.AmbiguousHintNames);

        var typeQueries = typeFiles
            .Combine(parsedFiles)
            .Combine(ambiguousHintNames.Combine(filesSettings))
            .Select(
                static (input, _) => SelectFiles(input.Left.Left, input.Left.Right, input.Right.Left, input.Right.Right)
            )
            .WithTrackingName(TrackingNames.TypeQueries);

        // The properties join after a type's queries are selected, so that they never reach the parse of a file.
        var projectLevel = projectSettings.Select(static (settings, _) => settings.Level);

        var typeOutputs = typeQueries
            .Combine(projectLevel)
            .Select(static (input, _) => TypeEmitter.Emit(input.Left, input.Right))
            .WithTrackingName(TrackingNames.TypeOutput);

        // What every type needs of each file, merged and sorted by path, so that a file finds its needs with a search.
        // A change to a type that leaves what it needs alone gives an equal value and checks nothing again.
        var fileNeeds = typeQueries
            .Combine(projectLevel)
            .Select(static (input, _) => SidecarNeeds.OfType(input.Left, input.Right))
            .WithTrackingName(TrackingNames.TypeNeeds)
            .SelectMany(static (files, _) => files)
            .Collect()
            .Select(static (files, _) => SidecarNeeds.Merge(files))
            .WithTrackingName(TrackingNames.FileNeeds);

        // One check for each claimed file, whatever the number of its types, so that each problem is reported once.
        // Each file gathers its own sidecar and needs, so an edit to one file or one sidecar checks that file alone.
        var sidecarChecks = parsedFiles
            .SelectMany(static (files, _) => files)
            .Combine(fileNeeds.Combine(sidecars))
            .Select(static (input, _) => SidecarCheckInput.Gather(input.Left, input.Right.Left, input.Right.Right))
            .WithTrackingName(TrackingNames.SidecarCheckInput)
            .Select(static (input, _) => SidecarChecker.Check(input))
            .WithTrackingName(TrackingNames.SidecarCheck);

        // The C# types that the type override names at any level, as a sorted list of their names, which an edit that
        // leaves the names alone gives again, equal.
        var mappedTypeNames = projectSettings
            .Combine(filesSettings)
            .Combine(parsedFiles)
            .Select(static (input, _) => MappedTypeNames.Collect(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.MappedTypeNames);

        // Those types resolved against the compilation.  Like the probe, it runs on every run, since the driver gives
        // a new compilation each time: a few lookups for each name, and none without an override.  Its value is equal
        // until a type appears or goes, so nothing after it runs again.
        var mappedTypes = context
            .CompilationProvider.Combine(mappedTypeNames)
            .Select(
                static (input, cancellationToken) =>
                    MappedTypeResolver.Resolve(input.Left, input.Right, cancellationToken)
            )
            .WithTrackingName(TrackingNames.MappedTypes);

        // Nothing reports on the mapped types yet.  A step that no output reads never runs, so this one makes the
        // resolution run, and its caching testable, until the types of the queries read it.
        context.RegisterSourceOutput(mappedTypes, static (_, _) => { });

        // Whether a query of the project needs an entry of postgres, whose types the driver decides.  Without one, the
        // driver is not reported on.
        var usesPostgresEntries = fileNeeds
            .Combine(parsedFiles)
            .Select(static (input, _) => UsesPostgresEntries(input.Left, input.Right))
            .WithTrackingName(TrackingNames.UsesPostgresEntries);

        // Not located, and once for the project, however many of its queries need the driver.
        context.RegisterSourceOutput(
            npgsqlProbe.Combine(usesPostgresEntries),
            static (output, input) =>
            {
                if (!input.Right)
                {
                    return;
                }

                var probe = input.Left;

                if (probe.IsBelowTheFloor)
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(SqlDiagnostics.NpgsqlTooOld, Location.None, probe.VersionText)
                    );
                }

                if (probe.IsNewerThanKnown)
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(SqlDiagnostics.NpgsqlNewerThanKnown, Location.None, probe.VersionText)
                    );
                }
            }
        );

        context.RegisterSourceOutput(
            sidecarChecks,
            static (output, check) =>
            {
                foreach (var diagnostic in check.Diagnostics)
                {
                    output.ReportDiagnostic(diagnostic.ToDiagnostic());
                }
            }
        );

        context.RegisterSourceOutput(
            typeOutputs,
            static (output, type) =>
            {
                foreach (var diagnostic in type.Diagnostics)
                {
                    output.ReportDiagnostic(diagnostic.ToDiagnostic());
                }

                if (type.Source is not null)
                {
                    output.AddSource(type.HintName, SourceText.From(type.Source, Encoding.UTF8));
                }
            }
        );
    }

    // One element for each path, the first the project lists, in the order of SqlPath.Comparer.
    private static EquatableArray<T> ToSorted<T>(ImmutableArray<T> items, Func<T, string> pathOf)
        where T : IEquatable<T> =>
        new(
            items
                .GroupBy(pathOf, SqlPath.Comparer)
                .Select(static group => group.First())
                .OrderBy(pathOf, SqlPath.Comparer)
                .ToImmutableArray()
        );

    // One sidecar for each .sql file, in the order of SqlPath.Comparer.  Of two whose paths differ only by case, the
    // one spelled as its .sql file's path with .json after it is kept, since that is the file the tool reads and
    // writes, and the first the project lists when neither is.  A path almost always has one sidecar, which is kept
    // without a look at its spelling.
    private static EquatableArray<SidecarFile> ToSortedSidecars(ImmutableArray<SidecarFile> files) =>
        new(
            files
                .GroupBy(static file => file.SqlPath, SqlPath.Comparer)
                .Select(static group =>
                    group.Skip(1).Any()
                        ? group.FirstOrDefault(static file => file.IsSpelledAsItsSqlFile) ?? group.First()
                        : group.First()
                )
                .OrderBy(static file => file.SqlPath, SqlPath.Comparer)
                .ToImmutableArray()
        );

    // Each setting and value once, in ordinal order, so that the errors of a build do not depend on the order of its
    // files.
    private static IEnumerable<InvalidSetting> FindInvalidSettings(
        ProjectSettings project,
        EquatableArray<FileSettings> files,
        EquatableArray<string> claimedPaths
    )
    {
        var found = new HashSet<InvalidSetting>(project.Invalid);
        foreach (var file in files.Where(file => SqlPath.Contains(claimedPaths, file.NormalizedPath)))
        {
            found.UnionWith(file.Invalid);
        }

        return found
            .OrderBy(static setting => setting.Name, StringComparer.Ordinal)
            .ThenBy(static setting => setting.Value, StringComparer.Ordinal);
    }

    // Each value once, in ordinal order, so that the errors of a build do not depend on the order of its files.
    private static SortedSet<string> FindInvalidDialects(EquatableArray<ParsedSqlFile> files, DialectSetting project)
    {
        var values = new SortedSet<string>(StringComparer.Ordinal);
        if (project.InvalidValue is { } property)
        {
            _ = values.Add(property);
        }

        foreach (var file in files)
        {
            if (file.InvalidDialect is { } metadata)
            {
                _ = values.Add(metadata);
            }
        }

        return values;
    }

    // A search for each file with needs, so that the cost does not grow with the files that have none.
    private static bool UsesPostgresEntries(EquatableArray<FileNeeds> fileNeeds, EquatableArray<ParsedSqlFile> files)
    {
        foreach (var needs in fileNeeds)
        {
            var index = SqlPath.IndexOf(files, needs.NormalizedPath, static file => file.NormalizedPath);
            if (index >= 0 && files[index].Dialect == SqlDialect.PostgreSql)
            {
                return true;
            }
        }

        return false;
    }

    private static TypeQueries SelectFiles(
        TypeFiles type,
        EquatableArray<ParsedSqlFile> parsedFiles,
        EquatableArray<string> ambiguousHintNames,
        EquatableArray<FileSettings> filesSettings
    )
    {
        var files = ImmutableArray.CreateBuilder<ParsedSqlFile>(type.Files.Count);
        var settings = ImmutableArray.CreateBuilder<SettingsLevel>(type.Files.Count);

        // A search for each of the type's files, so that the cost does not grow with the files of other types.
        foreach (var path in type.Files)
        {
            var index = SqlPath.IndexOf(parsedFiles, path, static file => file.NormalizedPath);
            if (index < 0)
            {
                continue;
            }

            files.Add(parsedFiles[index]);
            var settingsIndex = SqlPath.IndexOf(filesSettings, path, static file => file.NormalizedPath);
            settings.Add(settingsIndex < 0 ? SettingsLevel.None : filesSettings[settingsIndex].Level);
        }

        return new TypeQueries(
            type,
            new EquatableArray<ParsedSqlFile>(files.ToImmutable()),
            new EquatableArray<SettingsLevel>(settings.ToImmutable()),
            HintName.MakeUnique(HintName.Create(type.Type), ambiguousHintNames)
        );
    }
}
