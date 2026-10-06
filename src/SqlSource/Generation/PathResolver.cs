using System.Collections.Immutable;
using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// Finds the <c>.sql</c> files of a type among the project's.
/// </summary>
internal static class PathResolver
{
    /// <summary>
    /// Resolves the type's <c>Path</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="sqlPaths">
    /// The project's <c>.sql</c> files as normalised paths, distinct and in the order of
    /// <see cref="SqlPath.Comparer" />, which is also member order.
    /// </param>
    /// <param name="isSupportedFramework">Whether the project targets a framework the generated code runs on.</param>
    /// <param name="unsupportedLanguageVersion">
    /// The project's language version when it does not compile the generated code, and null otherwise.
    /// </param>
    public static TypeFiles Resolve(
        TargetType type,
        EquatableArray<string> sqlPaths,
        bool isSupportedFramework,
        string? unsupportedLanguageVersion
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        diagnostics.AddRange(type.Diagnostics);
        if (!isSupportedFramework)
        {
            diagnostics.Add(DiagnosticInfo.Create(SqlDiagnostics.UnsupportedTargetFramework, type.AttributeLocation));
        }
        else if (unsupportedLanguageVersion is not null)
        {
            // Only for a supported framework: an older one has an older language by default, and targeting .NET 8
            // fixes both.
            diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.UnsupportedLanguageVersion,
                    type.AttributeLocation,
                    unsupportedLanguageVersion
                )
            );
        }

        var files = FindFiles(type, sqlPaths);
        if (files.IsEmpty)
        {
            diagnostics.Add(
                type.Path is null
                    ? DiagnosticInfo.Create(SqlDiagnostics.FolderHasNoSqlFile, type.AttributeLocation)
                    : DiagnosticInfo.Create(SqlDiagnostics.PathMatchesNothing, type.AttributeLocation, type.Path)
            );
        }

        return new TypeFiles(
            type,
            new EquatableArray<string>(files),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable())
        );
    }

    private static ImmutableArray<string> FindFiles(TargetType type, EquatableArray<string> sqlPaths)
    {
        if (SqlPath.Normalize(type.FilePath) is not { } sourceFile)
        {
            return ImmutableArray<string>.Empty;
        }

        var folder = SqlPath.GetFolder(sourceFile);

        var target = type.Path is null ? folder : SqlPath.Combine(folder, type.Path);
        if (target is null)
        {
            return ImmutableArray<string>.Empty;
        }

        if (type.Path is null || !SqlPath.IsSqlFile(type.Path))
        {
            return SqlPath.FindInFolder(sqlPaths, target);
        }

        // The path as the project lists it, which may differ from the target in case.
        var index = SqlPath.IndexOf(sqlPaths, target, static path => path);
        return index < 0 ? ImmutableArray<string>.Empty : ImmutableArray.Create(sqlPaths[index]);
    }
}
