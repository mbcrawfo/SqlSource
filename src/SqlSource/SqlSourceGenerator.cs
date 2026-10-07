using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation;

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

        // Each file with the dialect that MSBuild gives it.  The dialect is resolved here, before the parse, so that
        // a change to the property parses only the files that fall back to it.
        var fileDialects = sqlFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(
                static (input, _) =>
                    (File: input.Left, Metadata: DialectSetting.ReadMetadata(input.Right.GetOptions(input.Left)))
            )
            .Combine(projectDialect)
            .Select(static (input, _) => FileDialect.Resolve(input.Left.File, input.Left.Metadata, input.Right))
            .WithTrackingName(TrackingNames.FileDialect);

        // A file that no type claims is never read.
        var parsedFiles = fileDialects
            .Combine(claimedPaths)
            .Select(
                static (input, cancellationToken) =>
                    SqlPath.Normalize(input.Left.File.Path) is { } path && SqlPath.Contains(input.Right, path)
                        ? SqlFileReader.Read(input.Left, path, cancellationToken)
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

        // Two types whose files would have names equal ignoring case.  Almost always none, so this value almost
        // never changes and costs the steps after it nothing.
        var ambiguousHintNames = typeFiles
            .Select(static (type, _) => HintName.Create(type.Type))
            .Collect()
            .Select(static (names, _) => HintName.FindAmbiguous(names))
            .WithTrackingName(TrackingNames.AmbiguousHintNames);

        // The project's setting, which is the same value until the property itself changes.
        var tokenValidation = context
            .AnalyzerConfigOptionsProvider.Select(
                static (options, _) => TokenValidationSetting.Read(options.GlobalOptions)
            )
            .WithTrackingName(TrackingNames.TokenValidation);

        // Not located: the compiler does not say where an MSBuild property was set.
        context.RegisterSourceOutput(
            tokenValidation,
            static (output, setting) =>
            {
                if (setting.InvalidValue is { } value)
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(SqlDiagnostics.InvalidTokenValidation, Location.None, value)
                    );
                }
            }
        );

        // The setting joins after a type's queries are selected, so that it never reaches the parse of a file.
        var typeOutputs = typeFiles
            .Combine(parsedFiles)
            .Combine(ambiguousHintNames)
            .Select(static (input, _) => SelectFiles(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.TypeQueries)
            .Combine(tokenValidation.Select(static (setting, _) => setting.Validate))
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
        EquatableArray<string> ambiguousHintNames
    )
    {
        var files = ImmutableArray.CreateBuilder<ParsedSqlFile>(type.Files.Count);

        // A search for each of the type's files, so that the cost does not grow with the files of other types.
        foreach (var path in type.Files)
        {
            var index = SqlPath.IndexOf(parsedFiles, path, static file => file.NormalizedPath);
            if (index >= 0)
            {
                files.Add(parsedFiles[index]);
            }
        }

        return new TypeQueries(
            type,
            new EquatableArray<ParsedSqlFile>(files.ToImmutable()),
            HintName.MakeUnique(HintName.Create(type.Type), ambiguousHintNames)
        );
    }
}
