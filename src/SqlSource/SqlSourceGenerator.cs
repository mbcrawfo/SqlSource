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
using SqlSource.Generation.Methods;
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
        var project = ReadProjectSettings(context);
        var claims = ClaimFiles(context, project.Level);
        var settings = ReadSettings(context, project, claims.SqlFiles);
        var parsedFiles = ParseFiles(
            context,
            settings.FileMetadata,
            settings.ProjectDialect,
            settings.ProjectSettings,
            claims.CommentPaths,
            claims.ClaimedPaths
        );
        ReportInvalidSettings(context, settings.ProjectSettings, settings.FilesSettings, claims.ClaimedPaths);
        var typeQueries = SelectTypeQueries(claims.ClaimingTypes, parsedFiles.Sorted, settings.FilesSettings);
        var sidecars = CheckSidecars(
            context,
            claims.ClaimedPaths,
            typeQueries,
            settings.ProjectLevel,
            parsedFiles,
            claims.RepeatedPaths
        );
        var typing = TypeFiles(
            context,
            sidecars.Checks,
            parsedFiles.Sorted,
            settings.FilesSettings,
            settings.ProjectSettings,
            settings.ProjectLevel
        );
        var models = CollectModels(context, typing.Files, typeQueries, settings.ProjectLevel);
        ReportDrivers(context, typing.NpgsqlProbe, typing.SqlClientProbe, sidecars.Needs, parsedFiles.Sorted);
        ReportProblems(context, sidecars.Checks, typing.FileTypes, models.FileModels, models.Models);
        RegisterModelSources(context, models.Models, parsedFiles.EachFile, claims.RepeatedPaths);
        RegisterTypeSources(context, typeQueries, settings.ProjectLevel);
        RegisterMethodSources(
            context,
            typeQueries,
            settings.ProjectLevel,
            models.AllFileModels,
            models.Models,
            models.ModelNames,
            claims.TypeMemberNames,
            typing.NpgsqlProbe,
            typing.SqlClientProbe
        );
    }

    // The attribute's file, which every compilation gets before the pipeline runs.
    private static void RegisterAttribute(IncrementalGeneratorInitializationContext context) =>
        context.RegisterPostInitializationOutput(static output =>
            output.AddSource(AttributeSource.HintName, SourceText.From(AttributeSource.Text, Encoding.UTF8))
        );

    // What the project's properties say about the settings, which is the same value until one of them changes, and
    // their level alone, which never reaches the parse of a file.
    private static ProjectProperties ReadProjectSettings(IncrementalGeneratorInitializationContext context)
    {
        var projectSettings = context
            .AnalyzerConfigOptionsProvider.Select(static (options, _) => ProjectSettings.Read(options.GlobalOptions))
            .WithTrackingName(TrackingNames.ProjectSettings);
        return new ProjectProperties(projectSettings, projectSettings.Select(static (settings, _) => settings.Level));
    }

    // The files each type claims, from the declarations that carry the attribute and the list of paths alone.
    private static ClaimedFiles ClaimFiles(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<SettingsLevel> projectLevel
    )
    {
        var declaredTypes = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                AttributeSource.AttributeMetadataName,
                static (node, _) => TargetTypeReader.IsCandidate(node),
                TargetTypeReader.Read
            )
            .WhereNotNull()
            .WithTrackingName(TrackingNames.TargetTypes);

        // The nested class named once the project's properties are known.  An edit that adds a member to a type
        // changes its declaration and runs this again, and goes no further unless the member takes the name.
        var targetTypes = declaredTypes
            .Combine(projectLevel)
            .Select(static (input, _) => NestedClassCheck.Apply(input.Left, input.Right))
            .WithTrackingName(TrackingNames.NestedClassCheck);

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

        // The paths that the project lists more than once, under one spelling or two.  Almost always none, so this
        // value almost never changes.
        var repeatedPaths = listedPaths
            .Combine(sqlPaths)
            .Select(static (input, _) => SqlPath.FindRepeated(input.Left, input.Right))
            .WithTrackingName(TrackingNames.RepeatedPaths);

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

        // The names that each type whose methods go on itself already has, which its methods cannot take.  Almost
        // always none, since the methods go into an extension class unless the attribute says otherwise.
        var typeMemberNames = declaredTypes
            .Where(static declared => declared.Type.MethodPlacement != MethodPlacement.ExtensionClass)
            .Select(static (declared, _) => new TypeMemberNames(HintName.NameOf(declared.Type), declared.TakenNames))
            .Collect()
            .Select(
                static (names, _) =>
                    new EquatableArray<TypeMemberNames>(
                        names.OrderBy(static names => names.TypeKey, StringComparer.Ordinal).ToImmutableArray()
                    )
            )
            .WithTrackingName(TrackingNames.TypeMemberNames);

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

        return new ClaimedFiles(sqlFiles, typeFiles, claimedPaths, commentPaths, repeatedPaths, typeMemberNames);
    }

    // What the project's properties and each file's metadata say.
    private static ProjectAndFileSettings ReadSettings(
        IncrementalGeneratorInitializationContext context,
        ProjectProperties project,
        IncrementalValuesProvider<AdditionalText> sqlFiles
    )
    {
        // The project's dialect, which is the same value until the property itself changes.
        var projectDialect = context
            .AnalyzerConfigOptionsProvider.Select(
                static (options, _) => DialectSetting.ReadProperty(options.GlobalOptions)
            )
            .WithTrackingName(TrackingNames.ProjectDialect);

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

        return new ProjectAndFileSettings(projectDialect, project.Settings, project.Level, fileMetadata, filesSettings);
    }

    // Each claimed file parsed, by itself and sorted by path, with its parse errors and the dialects that are not valid
    // reported.
    private static ParsedFiles ParseFiles(
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

        // Whether a type claims each file.  The claim is a value of its own, so a change to the claimed paths reads
        // only a file whose answer changed, and not every file that is claimed.
        var fileClaims = fileInputs
            .Combine(claimedPaths)
            .Select(static (input, _) => FileClaim.Of(input.Left, input.Right))
            .WithTrackingName(TrackingNames.FileClaim);

        // A file that no type claims is never read.
        var parsedFile = fileClaims
            .Select(
                static (claim, cancellationToken) =>
                    claim.IsClaimed ? SqlFileReader.Read(claim.Input, cancellationToken) : null
            )
            .WhereNotNull()
            .WithTrackingName(TrackingNames.ParsedFile);

        var parsedFiles = parsedFile
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

        return new ParsedFiles(parsedFile, parsedFiles);
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
                            setting.Name,
                            setting.Help
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
        ParsedFiles parsedFiles,
        IncrementalValueProvider<EquatableArray<string>> repeatedPaths
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

        // Each claimed file from its own ParsedFile node.  The compiler matches the nodes after a SelectMany by their
        // place, and these nodes follow the additional texts, so a file added, removed or renamed moves no other
        // file's check.  A path listed twice has a node for each listing, and the two would report its problems twice.
        // So it is checked from the collected files instead, whose nodes are matched by their place, a cost that a
        // project with no path listed twice never pays.
        var files = new FileNodes<ParsedSqlFile>(
            parsedFiles
                .EachFile.Combine(repeatedPaths)
                .Select(
                    static (input, _) => SqlPath.Contains(input.Right, input.Left.NormalizedPath) ? null : input.Left
                )
                .WhereNotNull(),
            parsedFiles
                .Sorted.Combine(repeatedPaths)
                .SelectMany(static (input, _) => FilesOfRepeatedPaths(input.Left, input.Right))
        );

        // One check for each claimed file, whatever the number of its types, so that each problem is reported once.
        // Each file gathers its own sidecar and needs, so an edit to one file or one sidecar checks that file alone.
        var sidecarChecks = files.Then(each =>
            each.Combine(fileNeeds.Combine(sidecars))
                .Select(static (input, _) => SidecarCheckInput.Gather(input.Left, input.Right.Left, input.Right.Right))
                .WithTrackingName(TrackingNames.SidecarCheckInput)
                .Select(static (input, _) => SidecarChecker.Check(input))
                .WithTrackingName(TrackingNames.SidecarCheck)
        );

        return new CheckedSidecars(fileNeeds, sidecarChecks);
    }

    // The C# types of each claimed file's usable entries, with the drivers and the mapped types they depend on.
    private static TypedFiles TypeFiles(
        IncrementalGeneratorInitializationContext context,
        FileNodes<SidecarCheckResult> sidecarChecks,
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

        // The Microsoft.Data.SqlClient the project references, the same way, which chooses the column of the SQL
        // Server map.
        var sqlClientProbe = context
            .CompilationProvider.Select(static (compilation, _) => SqlClientProbe.Read(compilation))
            .WithTrackingName(TrackingNames.SqlClientProbe);

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
        var typedFiles = sidecarChecks.Then(each =>
            each.Combine(parsedFiles.Combine(filesSettings))
                .Combine(projectTypeOverrides.Combine(npgsqlProbe.Combine(sqlClientProbe.Combine(mappedTypes))))
                .Select(
                    static (input, _) =>
                        FileTypesInput.Gather(
                            input.Left.Left,
                            input.Left.Right.Left,
                            input.Left.Right.Right,
                            input.Right.Left,
                            input.Right.Right.Left,
                            input.Right.Right.Right.Left,
                            input.Right.Right.Right.Right
                        )
                )
                .WithTrackingName(TrackingNames.FileTypesInput)
                .Select(static (input, _) => new TypedFile(input, QueryTyper.Type(input)))
                .WithTrackingName(TrackingNames.TypedFile)
        );

        var fileTypes = typedFiles.Then(each =>
            each.Select(static (file, _) => file.Types).WithTrackingName(TrackingNames.FileTypes)
        );

        return new TypedFiles(npgsqlProbe, sqlClientProbe, typedFiles, fileTypes);
    }

    // Each file's models, and the models every type's requests come to, checked against each other and the project.
    private static CollectedModels CollectModels(
        IncrementalGeneratorInitializationContext context,
        FileNodes<TypedFile> typedFiles,
        IncrementalValuesProvider<TypeQueries> typeQueries,
        IncrementalValueProvider<SettingsLevel> projectLevel
    )
    {
        // The properties of each file's models and the errors of their names, once for each file, whatever the number
        // of types that claim it, since no setting of a type changes them.
        var fileModels = typedFiles.Then(each =>
            each.Select(static (file, _) => ModelReader.Read(file)).WithTrackingName(TrackingNames.FileModels)
        );

        // Every file's models, sorted by path, so that a type finds the models of each of its files with a search.
        var allFileModels = fileModels
            .EachFile.Collect()
            .Combine(fileModels.OfRepeatedPaths.Collect())
            .Select(static (input, _) => ToSorted(input.Left.AddRange(input.Right), static file => file.NormalizedPath))
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

        return new CollectedModels(fileModels, allFileModels, modelNames, models);
    }

    // Whether the Npgsql and the Microsoft.Data.SqlClient the project references are too old or newer than known, each
    // reported if a query needs an entry of the driver's own engine, and independently of the other.
    private static void ReportDrivers(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<NpgsqlProbe> npgsqlProbe,
        IncrementalValueProvider<SqlClientProbe> sqlClientProbe,
        IncrementalValueProvider<EquatableArray<FileNeeds>> fileNeeds,
        IncrementalValueProvider<EquatableArray<ParsedSqlFile>> parsedFiles
    )
    {
        // Whether a query of the project needs an entry of an engine, whose types the driver decides.  Without one, the
        // driver is not reported on.
        var filesWithNeeds = fileNeeds.Combine(parsedFiles);
        var usesPostgresEntries = filesWithNeeds
            .Select(static (input, _) => UsesEntriesOf(input.Left, input.Right, SqlDialect.PostgreSql))
            .WithTrackingName(TrackingNames.UsesPostgresEntries);
        var usesSqlServerEntries = filesWithNeeds
            .Select(static (input, _) => UsesEntriesOf(input.Left, input.Right, SqlDialect.SqlServer))
            .WithTrackingName(TrackingNames.UsesSqlServerEntries);

        // Not located, and once for the project, however many of its queries need the driver.
        context.RegisterSourceOutput(
            npgsqlProbe.Combine(usesPostgresEntries),
            static (output, input) => ReportDriver(output, input.Right, input.Left)
        );
        context.RegisterSourceOutput(
            sqlClientProbe.Combine(usesSqlServerEntries),
            static (output, input) => ReportDriver(output, input.Right, input.Left)
        );
    }

    // The diagnostics of one driver, if a query needs an entry of its engine: whatever its probe finds.
    private static void ReportDriver(SourceProductionContext output, bool usesEntries, IDriverProbe probe)
    {
        if (!usesEntries)
        {
            return;
        }

        foreach (var finding in probe.Findings)
        {
            output.ReportDiagnostic(Diagnostic.Create(finding.Descriptor, Location.None, finding.Argument));
        }
    }

    // The problems of each file's sidecar, types and models, and of the models, each from a node of its own.
    private static void ReportProblems(
        IncrementalGeneratorInitializationContext context,
        FileNodes<SidecarCheckResult> sidecarChecks,
        FileNodes<FileTypes> fileTypes,
        FileNodes<FileModels> fileModels,
        IncrementalValueProvider<ModelSet> models
    )
    {
        context.RegisterSourceOutput(sidecarChecks.EachFile, ReportChecks);
        context.RegisterSourceOutput(sidecarChecks.OfRepeatedPaths, ReportChecks);

        // Beside the checks' output, so that a change to the types alone reports the checks of no file again.
        context.RegisterSourceOutput(fileTypes.EachFile, ReportTypes);
        context.RegisterSourceOutput(fileTypes.OfRepeatedPaths, ReportTypes);

        // Beside the types' output, so that a change to the models alone reports the types of no file again.
        context.RegisterSourceOutput(fileModels.EachFile, ReportModels);
        context.RegisterSourceOutput(fileModels.OfRepeatedPaths, ReportModels);

        // Apart from the models' sources, so that a change to one model reports nothing again.
        context.RegisterSourceOutput(models.Select(static (set, _) => set.Diagnostics), ReportAll);
    }

    // The file of each model, from the node of the file whose query gave it.
    private static void RegisterModelSources(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ModelSet> models,
        IncrementalValuesProvider<ParsedSqlFile> parsedFile,
        IncrementalValueProvider<EquatableArray<string>> repeatedPaths
    )
    {
        var modelsByFile = models
            .Combine(repeatedPaths)
            .Select(static (input, _) => ModelsByFile.Arrange(input.Left, input.Right))
            .WithTrackingName(TrackingNames.ModelsByFile);

        // The compiler matches the nodes after a SelectMany by their place, so each model is emitted from a node that
        // does not move when a model of another file or slot is added or removed.  A file's node stays with its
        // additional text: the compiler appends a text added, drops one removed and keeps the place of one replaced.
        // Inside it, a slot stays with its query and role.  So an edit writes the models whose record changed, and a
        // model added or removed writes itself and the models after it in its slot, those of the types sorted after
        // its own.
        var fileModelOutputs = parsedFile
            .Select(static (file, _) => file.NormalizedPath)
            .Combine(modelsByFile)
            .Select(static (input, _) => ModelsByFile.SlotsOf(input.Left, input.Right))
            .WithTrackingName(TrackingNames.FileModelSlots)
            .SelectMany(static (slots, _) => slots)
            .SelectMany(static (slot, _) => slot)
            .Select(static (model, _) => (model.HintName, Source: ModelEmitter.Emit(model)))
            .WithTrackingName(TrackingNames.ModelOutput);

        // A path the project lists twice has a node for each listing, and the two would add one file twice, which
        // throws and costs every type its code.  Its models are emitted from the arrangement instead, whose nodes are
        // matched by their place in its list, a cost that a project with no path listed twice never pays.
        var repeatedPathOutputs = modelsByFile
            .SelectMany(static (byFile, _) => byFile.OfRepeatedPaths)
            .Select(static (model, _) => (model.HintName, Source: ModelEmitter.Emit(model)))
            .WithTrackingName(TrackingNames.ModelOutput);

        context.RegisterSourceOutput(fileModelOutputs, AddModel);
        context.RegisterSourceOutput(repeatedPathOutputs, AddModel);
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

    // The file of each type's methods, and their diagnostics, with the clashes across types and SQLSRC273 for each
    // engine whose methods have no driver.
    private static void RegisterMethodSources(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<TypeQueries> typeQueries,
        IncrementalValueProvider<SettingsLevel> projectLevel,
        IncrementalValueProvider<EquatableArray<FileModels>> allFileModels,
        IncrementalValueProvider<ModelSet> models,
        IncrementalValueProvider<EquatableArray<string>> modelNames,
        IncrementalValueProvider<EquatableArray<TypeMemberNames>> typeMemberNames,
        IncrementalValueProvider<NpgsqlProbe> npgsqlProbe,
        IncrementalValueProvider<SqlClientProbe> sqlClientProbe
    )
    {
        // Whether each engine's driver allows its methods, which is the same value until a reference changes.
        var drivers = npgsqlProbe
            .Combine(sqlClientProbe)
            .Select(static (input, _) => MethodDrivers.Of(input.Left, input.Right))
            .WithTrackingName(TrackingNames.MethodDrivers);

        var assignments = models.Select(static (set, _) => set.Assignments);

        // Each type's own slice of what its methods need, which is equal until something of the type changes: its
        // queries, its files' models, the models its queries got, its names or the drivers.
        var typeMethods = typeQueries
            .Combine(projectLevel)
            .Combine(allFileModels.Combine(assignments))
            .Combine(typeMemberNames.Combine(drivers))
            .Select(
                static (input, _) =>
                    MethodsInput.Gather(
                        input.Left.Left.Left,
                        input.Left.Left.Right,
                        input.Left.Right.Left,
                        input.Left.Right.Right,
                        input.Right.Left,
                        input.Right.Right
                    )
            )
            .WithTrackingName(TrackingNames.MethodsInput)
            .Select(static (input, _) => MethodPlanner.Plan(input))
            .WithTrackingName(TrackingNames.TypeMethods);

        // Every type's methods as a call sees them, with the place of each query's name: an edit to a query's SQL that
        // moves no query's name leaves them equal, and one that moves a later query's name runs the steps after them
        // again, whose values stay equal when nothing clashes.
        var signatures = typeMethods
            .Select(static (methods, _) => MethodSignature.Of(methods))
            .Collect()
            .Select(static (each, _) => ToSortedSignatures(each))
            .WithTrackingName(TrackingNames.MethodSignatures);

        // The extension classes' full names, which almost never change.
        var extensionClassNames = signatures
            .Select(static (each, _) => ExtensionClassNamesOf(each))
            .WithTrackingName(TrackingNames.ExtensionClassNames);

        // Those names that the project has already, as a type or a namespace.  Like the lookup of the models' names, it
        // runs on every run, since the driver gives a new compilation each time, and its value is equal until a type or
        // a namespace of a name appears or goes.
        var takenExtensionClasses = context
            .CompilationProvider.Combine(extensionClassNames)
            .Select(
                static (input, cancellationToken) =>
                    ModelNameChecker.FindTaken(input.Left, input.Right, cancellationToken)
            )
            .WithTrackingName(TrackingNames.TakenExtensionClasses);

        // A step of its own, since it sees every type's methods at once: where the methods of two types are compared,
        // and each extension class's name with the project's and the models'.
        var conflicts = signatures
            .Combine(takenExtensionClasses)
            .Combine(modelNames)
            .Select(static (input, _) => MethodConflicts.Find(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.MethodConflicts);

        // Each type's file, from its own slice of the conflicts, so that a clash re-emits only the types it touches.
        var methodOutputs = typeMethods
            .Combine(conflicts)
            .Select(
                static (input, _) =>
                    (
                        Methods: input.Left,
                        Dropped: input.Right.DroppedOf(input.Left.TypeKey),
                        Blocked: input.Right.IsBlocked(input.Left.TypeKey)
                    )
            )
            .Select(static (input, _) => MethodEmitter.Emit(input.Methods, input.Dropped, input.Blocked))
            .WithTrackingName(TrackingNames.MethodOutput);

        // The problems of every type's methods together, each once: a file that two types claim gives both the same
        // problems of its queries.
        var methodDiagnostics = typeMethods
            .Select(static (methods, _) => methods.Diagnostics)
            .Collect()
            .Select(static (diagnostics, _) => MethodPlanner.Distinct(diagnostics))
            .WithTrackingName(TrackingNames.MethodDiagnostics);

        context.RegisterSourceOutput(methodDiagnostics, ReportAll);
        context.RegisterSourceOutput(conflicts, ReportConflicts);
        context.RegisterSourceOutput(methodOutputs, AddMethods);

        // The engines of the project's CodeGen queries, which almost never change.
        var codeGenEngines = typeMethods
            .Select(static (methods, _) => methods.CodeGenEngines)
            .Collect()
            .Select(static (engines, _) => engines.Aggregate(CodeGenEngines.None, static (all, engine) => all | engine))
            .WithTrackingName(TrackingNames.CodeGenEngines);

        // Not located, and once for the project and engine, however many of its queries need the driver.
        context.RegisterSourceOutput(codeGenEngines.Combine(npgsqlProbe.Combine(sqlClientProbe)), ReportMissingDrivers);
    }

    private static void ReportChecks(SourceProductionContext output, SidecarCheckResult check) =>
        ReportAll(output, check.Diagnostics);

    private static void ReportTypes(SourceProductionContext output, FileTypes types) =>
        ReportAll(output, types.Diagnostics);

    private static void ReportModels(SourceProductionContext output, FileModels models) =>
        ReportAll(output, models.Diagnostics);

    private static void AddModel(SourceProductionContext output, (string HintName, string Source) model) =>
        output.AddSource(model.HintName, SourceText.From(model.Source, Encoding.UTF8));

    private static void ReportConflicts(SourceProductionContext output, MethodConflicts conflicts) =>
        ReportAll(output, conflicts.Diagnostics);

    private static void AddMethods(SourceProductionContext output, (string HintName, string? Source) methods)
    {
        if (methods.Source is not null)
        {
            output.AddSource(methods.HintName, SourceText.From(methods.Source, Encoding.UTF8));
        }
    }

    // SQLSRC273 for each engine that a CodeGen query has and whose driver the project does not reference.
    private static void ReportMissingDrivers(
        SourceProductionContext output,
        (CodeGenEngines Engines, (NpgsqlProbe Npgsql, SqlClientProbe SqlClient) Probes) input
    )
    {
        if ((input.Engines & CodeGenEngines.Postgres) != 0 && !input.Probes.Npgsql.Referenced)
        {
            output.ReportDiagnostic(
                Diagnostic.Create(SqlDiagnostics.DriverNotReferenced, Location.None, "PostgreSQL", "Npgsql")
            );
        }

        if ((input.Engines & CodeGenEngines.SqlServer) != 0 && !input.Probes.SqlClient.Referenced)
        {
            output.ReportDiagnostic(
                Diagnostic.Create(
                    SqlDiagnostics.DriverNotReferenced,
                    Location.None,
                    "SQL Server",
                    "Microsoft.Data.SqlClient"
                )
            );
        }
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

    // Every type's signatures, by the type's key and then by name, so that the value does not depend on the order of
    // the compilation's types.
    private static EquatableArray<MethodSignature> ToSortedSignatures(
        ImmutableArray<EquatableArray<MethodSignature>> each
    ) =>
        new(
            each.SelectMany(static signatures => signatures)
                .OrderBy(static signature => signature.TypeKey, StringComparer.Ordinal)
                .ThenBy(static signature => signature.Name, StringComparer.Ordinal)
                .ToImmutableArray()
        );

    // The distinct full names of the extension classes, in ordinal order.
    private static EquatableArray<string> ExtensionClassNamesOf(EquatableArray<MethodSignature> signatures) =>
        new(
            signatures
                .Where(static signature => signature.ExtensionClass is not null)
                .Select(static signature => signature.ExtensionClassFullName!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal)
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

    // Whether a file with needs is read as this dialect.  A search for each file with needs, so that the cost does not
    // grow with the files that have none.
    private static bool UsesEntriesOf(
        EquatableArray<FileNeeds> fileNeeds,
        EquatableArray<ParsedSqlFile> files,
        SqlDialect dialect
    )
    {
        foreach (var needs in fileNeeds)
        {
            var index = SqlPath.IndexOf(files, needs.NormalizedPath, static file => file.NormalizedPath);
            if (index >= 0 && files[index].Dialect == dialect)
            {
                return true;
            }
        }

        return false;
    }

    // For each path the project lists more than once, the file of it, if a type claims it.  A search for each path, so
    // that the cost does not grow with the files.
    private static IEnumerable<ParsedSqlFile> FilesOfRepeatedPaths(
        EquatableArray<ParsedSqlFile> files,
        EquatableArray<string> repeatedPaths
    )
    {
        foreach (var path in repeatedPaths)
        {
            var index = SqlPath.IndexOf(files, path, static file => file.NormalizedPath);
            if (index >= 0)
            {
                yield return files[index];
            }
        }
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

    // What the project's properties say, whole and as a level.
    private readonly record struct ProjectProperties(
        IncrementalValueProvider<ProjectSettings> Settings,
        IncrementalValueProvider<SettingsLevel> Level
    );

    // The project's .sql files, each type with the files it claims, every claimed path, those that keep comments, the
    // paths the project lists more than once, and the names of the types whose methods go on themselves.
    private readonly record struct ClaimedFiles(
        IncrementalValuesProvider<AdditionalText> SqlFiles,
        IncrementalValuesProvider<TypeFiles> ClaimingTypes,
        IncrementalValueProvider<EquatableArray<string>> ClaimedPaths,
        IncrementalValueProvider<EquatableArray<string>> CommentPaths,
        IncrementalValueProvider<EquatableArray<string>> RepeatedPaths,
        IncrementalValueProvider<EquatableArray<TypeMemberNames>> TypeMemberNames
    );

    // Each claimed file parsed, a node for each item of the project's list, and the files collected, one for each path
    // and sorted by it.
    private readonly record struct ParsedFiles(
        IncrementalValuesProvider<ParsedSqlFile> EachFile,
        IncrementalValueProvider<EquatableArray<ParsedSqlFile>> Sorted
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
        FileNodes<SidecarCheckResult> Checks
    );

    // The drivers the project references, each claimed file typed, and its types alone.
    private readonly record struct TypedFiles(
        IncrementalValueProvider<NpgsqlProbe> NpgsqlProbe,
        IncrementalValueProvider<SqlClientProbe> SqlClientProbe,
        FileNodes<TypedFile> Files,
        FileNodes<FileTypes> FileTypes
    );

    // Each file's models, every file's together, the models' full names, and the models to emit with the problems of
    // the requests.
    private readonly record struct CollectedModels(
        FileNodes<FileModels> FileModels,
        IncrementalValueProvider<EquatableArray<FileModels>> AllFileModels,
        IncrementalValueProvider<EquatableArray<string>> ModelNames,
        IncrementalValueProvider<ModelSet> Models
    );

    // A step for each claimed file, in two chains: from each file's own ParsedFile node, for a path the project lists
    // once, and from the collected files, for a path it lists more than once.  A step is added to both by Then, under
    // its one tracking name.
    private readonly record struct FileNodes<T>(
        IncrementalValuesProvider<T> EachFile,
        IncrementalValuesProvider<T> OfRepeatedPaths
    )
    {
        public FileNodes<TResult> Then<TResult>(
            Func<IncrementalValuesProvider<T>, IncrementalValuesProvider<TResult>> step
        ) => new(step(EachFile), step(OfRepeatedPaths));
    }
}
