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
using SqlSource.Generation.Models;
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
        RegisterAttribute(context);
        var claims = ClaimFiles(context);
        var settings = ReadSettings(context, claims.SqlFiles);
        var parsedFiles = ParseFiles(
            context,
            settings.FileMetadata,
            settings.ProjectDialect,
            settings.ProjectSettings,
            claims.CommentPaths,
            claims.ClaimedPaths
        );
        ReportInvalidSettings(context, settings.ProjectSettings, settings.FilesSettings, claims.ClaimedPaths);
        var typeQueries = SelectTypeQueries(claims.ClaimingTypes, parsedFiles, settings.FilesSettings);
        var sidecars = CheckSidecars(context, claims.ClaimedPaths, typeQueries, settings.ProjectLevel, parsedFiles);
        var typing = TypeFiles(
            context,
            sidecars.Checks,
            parsedFiles,
            settings.FilesSettings,
            settings.ProjectSettings,
            settings.ProjectLevel
        );
        var models = CollectModels(context, typing.Files, typeQueries, settings.ProjectLevel);
        ReportDriver(context, typing.NpgsqlProbe, sidecars.Needs, parsedFiles);
        ReportProblems(context, sidecars.Checks, typing.FileTypes, models.FileModels, models.Models);
        RegisterModelSources(context, models.Models);
        RegisterTypeSources(context, typeQueries, settings.ProjectLevel);
    }

    // The attribute's file, which every compilation gets before the pipeline runs.
    private static void RegisterAttribute(IncrementalGeneratorInitializationContext context) =>
        context.RegisterPostInitializationOutput(static output =>
            output.AddSource(AttributeSource.HintName, SourceText.From(AttributeSource.Text, Encoding.UTF8))
        );

    // The files each type claims, from the declarations that carry the attribute and the list of paths alone.
    private static ClaimedFiles ClaimFiles(IncrementalGeneratorInitializationContext context)
    {
        var targetTypes = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                AttributeSource.AttributeMetadataName,
                static (node, _) => TargetTypeReader.IsCandidate(node),
                TargetTypeReader.Read
            )
            .WhereNotNull()
            .WithTrackingName(TrackingNames.TargetTypes);

        var sqlFiles = context.AdditionalTextsProvider.Where(static file => SqlPath.IsSqlFile(file.Path));

        // The paths alone, so that resolving a type's Path does not depend on the text of any file.
        var listedPaths = sqlFiles.Select(static (file, _) => SqlFilePath.Create(file.Path)).WhereNotNull().Collect();

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

        return new ClaimedFiles(sqlFiles, typeFiles, claimedPaths, commentPaths);
    }

    // What the project's properties and each file's metadata say.
    private static ProjectAndFileSettings ReadSettings(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<AdditionalText> sqlFiles
    )
    {
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

        // The properties' level alone, which never reaches the parse of a file.
        var projectLevel = projectSettings.Select(static (settings, _) => settings.Level);

        // What the metadata of each file's item says: its dialect and its settings.
        var fileMetadata = sqlFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (input, _) => FileMetadata.Read(input.Left, input.Right.GetOptions(input.Left)));

        // Stage two of the settings: the metadata of the files that have any, which are few.  It never reaches the
        // parse of a file.
        var filesSettings = fileMetadata
            .Select(static (metadata, _) => metadata.Settings)
            .WhereNotNull()
            .WithTrackingName(TrackingNames.FileSettings)
            .Collect()
            .Select(static (settings, _) => ToSorted(settings, static file => file.NormalizedPath))
            .WithTrackingName(TrackingNames.FilesSettings);

        return new ProjectAndFileSettings(projectDialect, projectSettings, projectLevel, fileMetadata, filesSettings);
    }

    // Each claimed file parsed, sorted by path, with its parse errors and the dialects that are not valid reported.
    private static IncrementalValueProvider<EquatableArray<ParsedSqlFile>> ParseFiles(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<FileMetadata> fileMetadata,
        IncrementalValueProvider<DialectSetting> projectDialect,
        IncrementalValueProvider<ProjectSettings> projectSettings,
        IncrementalValueProvider<EquatableArray<string>> commentPaths,
        IncrementalValueProvider<EquatableArray<string>> claimedPaths
    )
    {
        // Whether the property's list asks for comments.  It is an input of the parse, so it is a value of its own
        // that changes only when the answer does.
        var projectKeepsComments = projectSettings.Select(static (settings, _) => settings.Level.KeepsComments);

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
            .WhereNotNull()
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

        return parsedFiles;
    }

    // Each setting and value that is not valid, once and with no position, of the project and the claimed files alone.
    private static void ReportInvalidSettings(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ProjectSettings> projectSettings,
        IncrementalValueProvider<EquatableArray<FileSettings>> filesSettings,
        IncrementalValueProvider<EquatableArray<string>> claimedPaths
    ) =>
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

    // Each type with the parsed files it claims, the settings of their metadata, and the name of its file.
    private static IncrementalValuesProvider<TypeQueries> SelectTypeQueries(
        IncrementalValuesProvider<TypeFiles> typeFiles,
        IncrementalValueProvider<EquatableArray<ParsedSqlFile>> parsedFiles,
        IncrementalValueProvider<EquatableArray<FileSettings>> filesSettings
    )
    {
        // Two types whose files would have names equal ignoring case.  Almost always none, so this value almost
        // never changes and costs the steps after it nothing.
        var ambiguousHintNames = typeFiles
            .Select(static (type, _) => HintName.Create(type.Type))
            .Collect()
            .Select(static (names, _) => HintName.FindAmbiguous(names))
            .WithTrackingName(TrackingNames.AmbiguousHintNames);

        return typeFiles
            .Combine(parsedFiles)
            .Combine(ambiguousHintNames.Combine(filesSettings))
            .Select(
                static (input, _) => SelectFiles(input.Left.Left, input.Left.Right, input.Right.Left, input.Right.Right)
            )
            .WithTrackingName(TrackingNames.TypeQueries);
    }

    // What every type needs of each file, and the check of each claimed file's sidecar against it.
    private static CheckedSidecars CheckSidecars(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<EquatableArray<string>> claimedPaths,
        IncrementalValuesProvider<TypeQueries> typeQueries,
        IncrementalValueProvider<SettingsLevel> projectLevel,
        IncrementalValueProvider<EquatableArray<ParsedSqlFile>> parsedFiles
    )
    {
        // The sidecars are an input of their own, so that an edit to one reads nothing that a .sql file feeds.  A
        // sidecar whose .sql file no type claims is never read, and no step after the read holds its text.
        var sidecars = context
            .AdditionalTextsProvider.Where(static file => SqlPath.IsSidecarFile(file.Path))
            .Combine(claimedPaths)
            .Select(static (input, _) => SidecarInput.Create(input.Left, input.Right))
            .WithTrackingName(TrackingNames.SidecarInput)
            .WhereNotNull()
            .Select(static (input, cancellationToken) => SidecarFileReader.Read(input, cancellationToken))
            .WithTrackingName(TrackingNames.SidecarFile)
            .Collect()
            .Select(static (files, _) => ToSortedSidecars(files))
            .WithTrackingName(TrackingNames.Sidecars);

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

        return new CheckedSidecars(fileNeeds, sidecarChecks);
    }

    // The C# types of each claimed file's usable entries, with the Npgsql and the mapped types they depend on.
    private static TypedFiles TypeFiles(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<SidecarCheckResult> sidecarChecks,
        IncrementalValueProvider<EquatableArray<ParsedSqlFile>> parsedFiles,
        IncrementalValueProvider<EquatableArray<FileSettings>> filesSettings,
        IncrementalValueProvider<ProjectSettings> projectSettings,
        IncrementalValueProvider<SettingsLevel> projectLevel
    )
    {
        // The Npgsql the project references, which chooses the column of the base map.  Like the framework check, it
        // runs on every compilation and gives an equal value until a reference changes.
        var npgsqlProbe = context
            .CompilationProvider.Select(static (compilation, _) => NpgsqlProbe.Read(compilation))
            .WithTrackingName(TrackingNames.NpgsqlProbe);

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

        // The property's type override alone, so that a change to another property types no file again.
        var projectTypeOverrides = projectLevel.Select(static (level, _) => FileTypesInput.TypeOverridesOf(level));

        // The C# types of each claimed file's usable entries, a node for each file after its check, so that an edit to
        // one file or one sidecar types that file alone.  A file with no usable entry carries nothing to type.  Each
        // file keeps its input beside its types, so that the models read its entries without typing it again.
        var typedFiles = sidecarChecks
            .Combine(parsedFiles.Combine(filesSettings))
            .Combine(projectTypeOverrides.Combine(npgsqlProbe.Combine(mappedTypes)))
            .Select(
                static (input, _) =>
                    FileTypesInput.Gather(
                        input.Left.Left,
                        input.Left.Right.Left,
                        input.Left.Right.Right,
                        input.Right.Left,
                        input.Right.Right.Left,
                        input.Right.Right.Right
                    )
            )
            .WithTrackingName(TrackingNames.FileTypesInput)
            .Select(static (input, _) => new TypedFile(input, QueryTyper.Type(input)))
            .WithTrackingName(TrackingNames.TypedFile);

        var fileTypes = typedFiles.Select(static (file, _) => file.Types).WithTrackingName(TrackingNames.FileTypes);

        return new TypedFiles(npgsqlProbe, typedFiles, fileTypes);
    }

    // Each file's models, and the models every type's requests come to, checked against each other and the project.
    private static CollectedModels CollectModels(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TypedFile> typedFiles,
        IncrementalValuesProvider<TypeQueries> typeQueries,
        IncrementalValueProvider<SettingsLevel> projectLevel
    )
    {
        // The properties of each file's models and the errors of their names, once for each file, whatever the number
        // of types that claim it, since no setting of a type changes them.
        var fileModels = typedFiles
            .Select(static (file, _) => ModelReader.Read(file))
            .WithTrackingName(TrackingNames.FileModels);

        // Every file's models, sorted by path, so that a type finds the models of each of its files with a search.
        var allFileModels = fileModels
            .Collect()
            .Select(static (files, _) => ToSorted(files, static file => file.NormalizedPath))
            .WithTrackingName(TrackingNames.AllFileModels);

        // What each type asks for of its queries' models, which only its own settings decide.  A change to a type or
        // to a file's models that leaves the type's requests alone gives an equal value, and nothing after it runs.
        var typeModels = typeQueries
            .Combine(allFileModels)
            .Combine(projectLevel)
            .Select(static (input, _) => TypeModels.Of(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.TypeModels);

        // Every type's requests together, by full name, since two types or two queries can ask for one model.
        var modelGroups = typeModels
            .SelectMany(static (requests, _) => requests)
            .Collect()
            .Select(static (requests, _) => ModelCollector.Group(requests))
            .WithTrackingName(TrackingNames.ModelGroups);

        // The models' full names alone, which an edit that leaves every name alone gives again, equal.
        var modelNames = modelGroups
            .Select(static (groups, _) => ModelCollector.FullNamesOf(groups))
            .WithTrackingName(TrackingNames.ModelNames);

        // Those names that the project has already, as a type or a namespace.  Like the lookup of the mapped types, it
        // runs on every run, since the driver gives a new compilation each time: a few lookups for each name, and none
        // for a project without a model.  Its value is equal until a type or a namespace of a name appears or goes.
        var takenModelNames = context
            .CompilationProvider.Combine(modelNames)
            .Select(
                static (input, cancellationToken) =>
                    ModelNameChecker.FindTaken(input.Left, input.Right, cancellationToken)
            )
            .WithTrackingName(TrackingNames.TakenModelNames);

        // A step of its own, since it sees every request at once: it is where the requests of one full name are
        // checked against each other, and each model's name against the other models' and the project's, which
        // decides which models are generated and what is reported.
        var models = modelGroups
            .Combine(takenModelNames)
            .Select(static (input, _) => ModelCollector.Resolve(input.Left, input.Right))
            .WithTrackingName(TrackingNames.Models);

        return new CollectedModels(fileModels, models);
    }

    // Whether the Npgsql the project references is too old or newer than known, if a query needs an entry of postgres.
    private static void ReportDriver(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<NpgsqlProbe> npgsqlProbe,
        IncrementalValueProvider<EquatableArray<FileNeeds>> fileNeeds,
        IncrementalValueProvider<EquatableArray<ParsedSqlFile>> parsedFiles
    )
    {
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
    }

    // The problems of each file's sidecar, types and models, and of the models, each from a node of its own.
    private static void ReportProblems(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<SidecarCheckResult> sidecarChecks,
        IncrementalValuesProvider<FileTypes> fileTypes,
        IncrementalValuesProvider<FileModels> fileModels,
        IncrementalValueProvider<ModelSet> models
    )
    {
        context.RegisterSourceOutput(sidecarChecks, static (output, check) => ReportAll(output, check.Diagnostics));

        // Beside the checks' output, so that a change to the types alone reports the checks of no file again.
        context.RegisterSourceOutput(fileTypes, static (output, types) => ReportAll(output, types.Diagnostics));

        // Beside the types' output, so that a change to the models alone reports the types of no file again.
        context.RegisterSourceOutput(fileModels, static (output, models) => ReportAll(output, models.Diagnostics));

        // Apart from the models' sources, so that a change to one model reports nothing again.
        context.RegisterSourceOutput(models.Select(static (set, _) => set.Diagnostics), ReportAll);
    }

    // The file of each model.
    private static void RegisterModelSources(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ModelSet> models
    )
    {
        // One node for each model, compared by position, so that an edit writes again only the models whose record
        // changed, and those after a model that was added or removed.
        var modelOutputs = models
            .SelectMany(static (set, _) => set.Models)
            .Select(static (model, _) => (model.HintName, Source: ModelEmitter.Emit(model)))
            .WithTrackingName(TrackingNames.ModelOutput);

        context.RegisterSourceOutput(
            modelOutputs,
            static (output, model) => output.AddSource(model.HintName, SourceText.From(model.Source, Encoding.UTF8))
        );
    }

    // The file of each type, and its diagnostics.
    private static void RegisterTypeSources(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TypeQueries> typeQueries,
        IncrementalValueProvider<SettingsLevel> projectLevel
    )
    {
        var typeOutputs = typeQueries
            .Combine(projectLevel)
            .Select(static (input, _) => TypeEmitter.Emit(input.Left, input.Right))
            .WithTrackingName(TrackingNames.TypeOutput);

        context.RegisterSourceOutput(
            typeOutputs,
            static (output, type) =>
            {
                ReportAll(output, type.Diagnostics);

                if (type.Source is not null)
                {
                    output.AddSource(type.HintName, SourceText.From(type.Source, Encoding.UTF8));
                }
            }
        );
    }

    private static void ReportAll(SourceProductionContext output, EquatableArray<DiagnosticInfo> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            output.ReportDiagnostic(diagnostic.ToDiagnostic());
        }
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

    // Each setting and value once, in the order of InvalidValues.
    private static IEnumerable<InvalidSetting> FindInvalidSettings(
        ProjectSettings project,
        EquatableArray<FileSettings> files,
        EquatableArray<string> claimedPaths
    )
    {
        var invalid = new InvalidValues();
        invalid.AddSettings(project.Invalid);
        foreach (var file in files.Where(file => SqlPath.Contains(claimedPaths, file.NormalizedPath)))
        {
            invalid.AddSettings(file.Invalid);
        }

        return invalid.Settings;
    }

    // Each value once, in the order of InvalidValues.
    private static IReadOnlyCollection<string> FindInvalidDialects(
        EquatableArray<ParsedSqlFile> files,
        DialectSetting project
    )
    {
        var invalid = new InvalidValues();
        invalid.AddDialect(project.InvalidValue);
        foreach (var file in files)
        {
            invalid.AddDialect(file.InvalidDialect);
        }

        return invalid.Dialects;
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

    // The project's .sql files, each type with the files it claims, every claimed path, and those that keep comments.
    private readonly record struct ClaimedFiles(
        IncrementalValuesProvider<AdditionalText> SqlFiles,
        IncrementalValuesProvider<TypeFiles> ClaimingTypes,
        IncrementalValueProvider<EquatableArray<string>> ClaimedPaths,
        IncrementalValueProvider<EquatableArray<string>> CommentPaths
    );

    // What the project's properties say, and the metadata of each file and of the files that have settings.
    private readonly record struct ProjectAndFileSettings(
        IncrementalValueProvider<DialectSetting> ProjectDialect,
        IncrementalValueProvider<ProjectSettings> ProjectSettings,
        IncrementalValueProvider<SettingsLevel> ProjectLevel,
        IncrementalValuesProvider<FileMetadata> FileMetadata,
        IncrementalValueProvider<EquatableArray<FileSettings>> FilesSettings
    );

    // What every type needs of each file, and the check of each claimed file's sidecar.
    private readonly record struct CheckedSidecars(
        IncrementalValueProvider<EquatableArray<FileNeeds>> Needs,
        IncrementalValuesProvider<SidecarCheckResult> Checks
    );

    // The Npgsql the project references, each claimed file typed, and its types alone.
    private readonly record struct TypedFiles(
        IncrementalValueProvider<NpgsqlProbe> NpgsqlProbe,
        IncrementalValuesProvider<TypedFile> Files,
        IncrementalValuesProvider<FileTypes> FileTypes
    );

    // Each file's models, and the models to emit with the problems of the requests.
    private readonly record struct CollectedModels(
        IncrementalValuesProvider<FileModels> FileModels,
        IncrementalValueProvider<ModelSet> Models
    );
}
