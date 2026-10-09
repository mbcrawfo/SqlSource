using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;
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
            .Select(static (paths, _) => ToSortedSet(paths.Select(static path => path.NormalizedPath)))
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
            .Select(static (paths, _) => ToSortedSet(paths))
            .WithTrackingName(TrackingNames.ClaimedPaths);

        // The files of the types whose attribute asks for comments.  Almost always none, so this value almost never
        // changes.
        var commentPaths = typeFiles
            .SelectMany(
                static (type, _) => type.Type.Settings.KeepsComments ? type.Files : EquatableArray<string>.Empty
            )
            .Collect()
            .Select(static (paths, _) => ToSortedSet(paths))
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
            .Select(static (files, _) => ToSortedFiles(files))
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

        // Stage two of the settings: the metadata of the files that have any, which are few.  It never reaches the
        // parse of a file.
        var filesSettings = fileMetadata
            .Select(static (metadata, _) => metadata.Settings)
            .Where(static settings => settings is not null)
            .Select(static (settings, _) => settings!)
            .WithTrackingName(TrackingNames.FileSettings)
            .Collect()
            .Select(static (settings, _) => ToSortedSettings(settings))
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

        // The properties join after a type's queries are selected, so that they never reach the parse of a file.
        var typeOutputs = typeFiles
            .Combine(parsedFiles)
            .Combine(ambiguousHintNames.Combine(filesSettings))
            .Select(
                static (input, _) => SelectFiles(input.Left.Left, input.Left.Right, input.Right.Left, input.Right.Right)
            )
            .WithTrackingName(TrackingNames.TypeQueries)
            .Combine(projectSettings.Select(static (settings, _) => settings.Level))
            .Select(static (input, _) => TypeEmitter.Emit(input.Left, input.Right))
            .WithTrackingName(TrackingNames.TypeOutput);

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

    // Distinct ignoring case and in the order of SqlPath.Comparer, which is also the order of a type's members.
    private static EquatableArray<string> ToSortedSet(IEnumerable<string> paths) =>
        new(paths.Distinct(SqlPath.Comparer).OrderBy(static path => path, SqlPath.Comparer).ToImmutableArray());

    // One file for each path, the first the project lists, in the order of SqlPath.Comparer.
    private static EquatableArray<ParsedSqlFile> ToSortedFiles(ImmutableArray<ParsedSqlFile> files) =>
        new(
            files
                .GroupBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .Select(static group => group.First())
                .OrderBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .ToImmutableArray()
        );

    // One entry for each path, the first the project lists, in the order of SqlPath.Comparer.
    private static EquatableArray<FileSettings> ToSortedSettings(ImmutableArray<FileSettings> settings) =>
        new(
            settings
                .GroupBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .Select(static group => group.First())
                .OrderBy(static file => file.NormalizedPath, SqlPath.Comparer)
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
