using System.Collections.Immutable;
using System.Linq;
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
    /// <param name="sqlPaths">The project's <c>.sql</c> files as normalised paths, in member order.</param>
    /// <param name="isSupportedFramework">Whether the project targets a framework the generated code runs on.</param>
    public static TypeFiles Resolve(TargetType type, EquatableArray<string> sqlPaths, bool isSupportedFramework)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        diagnostics.AddRange(type.Diagnostics);
        if (!isSupportedFramework)
        {
            diagnostics.Add(DiagnosticInfo.Create(SqlDiagnostics.UnsupportedTargetFramework, type.AttributeLocation));
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

        var isFile = type.Path is not null && SqlPath.IsSqlFile(type.Path);
        return sqlPaths
            .Where(path => SqlPath.Comparer.Equals(isFile ? path : SqlPath.GetFolder(path), target))
            .ToImmutableArray();
    }
}
