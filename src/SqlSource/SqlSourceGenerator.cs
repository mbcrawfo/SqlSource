using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Generation;

namespace SqlSource;

/// <summary>
/// Generates C# source for SQL queries.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SqlSourceGenerator : IIncrementalGenerator
{
    // The generated code targets .NET 8 and later.  This method first appeared there, and phase 3 of the SQL queries
    // epic calls it, so its presence is the test.
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
        var sqlPaths = sqlFiles
            .Select(static (file, _) => SqlPath.Normalize(file.Path))
            .Collect()
            .Select(static (paths, _) => ToSortedSet(paths.RemoveAll(static path => path is null)!))
            .WithTrackingName(TrackingNames.SqlPaths);

        var isSupportedFramework = context
            .CompilationProvider.Select(
                static (compilation, _) =>
                    compilation.GetTypeByMetadataName(FloorType)?.GetMembers(FloorMember).IsEmpty == false
            )
            .WithTrackingName(TrackingNames.SupportedFramework);

        var typeFiles = targetTypes
            .Combine(sqlPaths)
            .Combine(isSupportedFramework)
            .Select(static (input, _) => PathResolver.Resolve(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.TypeFiles);

        var claimedPaths = typeFiles
            .SelectMany(static (type, _) => type.Files)
            .Collect()
            .Select(static (paths, _) => ToSortedSet(paths))
            .WithTrackingName(TrackingNames.ClaimedPaths);

        // A file that no type claims is never read.
        var parsedFiles = sqlFiles
            .Combine(claimedPaths)
            .Select(
                static (input, cancellationToken) =>
                    SqlPath.Normalize(input.Left.Path) is { } path && SqlPath.Contains(input.Right, path)
                        ? SqlFileReader.Read(input.Left, path, cancellationToken)
                        : null
            )
            .Where(static file => file is not null)
            .Select(static (file, _) => file!)
            .WithTrackingName(TrackingNames.ParsedFile)
            .Collect()
            .Select(static (files, _) => ToSortedFiles(files))
            .WithTrackingName(TrackingNames.ParsedFiles);

        // Reported from the collected files, so that a file the project lists twice is reported once.
        context.RegisterSourceOutput(
            parsedFiles,
            static (output, files) =>
            {
                foreach (var error in files.SelectMany(static file => file.Errors))
                {
                    output.ReportDiagnostic(error.ToDiagnostic());
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

        var typeOutputs = typeFiles
            .Combine(parsedFiles)
            .Combine(ambiguousHintNames)
            .Select(static (input, _) => SelectFiles(input.Left.Left, input.Left.Right, input.Right))
            .WithTrackingName(TrackingNames.TypeQueries)
            .Select(static (queries, _) => TypeEmitter.Emit(queries, validateTokens: true))
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
    private static EquatableArray<string> ToSortedSet(ImmutableArray<string> paths) =>
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

    private static TypeQueries SelectFiles(
        TypeFiles type,
        EquatableArray<ParsedSqlFile> parsedFiles,
        EquatableArray<string> ambiguousHintNames
    )
    {
        var files = ImmutableArray.CreateBuilder<ParsedSqlFile>(type.Files.Count);
        var index = 0;

        // Both lists are in the same order, so one pass over the parsed files finds each of the type's.
        foreach (var path in type.Files)
        {
            while (index < parsedFiles.Count && SqlPath.Comparer.Compare(parsedFiles[index].NormalizedPath, path) < 0)
            {
                index++;
            }

            if (index < parsedFiles.Count && SqlPath.Comparer.Equals(parsedFiles[index].NormalizedPath, path))
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
