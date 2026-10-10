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

        var files = FindFiles(type.FilePath, type.Path, sqlPaths);
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

    /// <summary>
    /// The files that a <c>Path</c> names, for an attribute in the file <paramref name="sourceFilePath" />: the
    /// <c>.sql</c> files of the folder of that file when <paramref name="path" /> is null, of the folder it names, or
    /// the one file it names.  Empty when it names nothing.  The <c>sqlsource</c> tool resolves a type's files with
    /// this too, so a change here changes what the tool describes.
    /// </summary>
    /// <param name="sourceFilePath">The path of the C# file that carries the attribute.</param>
    /// <param name="path">The attribute's <c>Path</c>, or null when it is not set.</param>
    /// <param name="sqlPaths">
    /// The project's <c>.sql</c> files, as <see cref="SqlPath.ToSortedSet" /> gives them.
    /// </param>
    public static ImmutableArray<string> FindFiles(string sourceFilePath, string? path, EquatableArray<string> sqlPaths)
    {
        if (SqlPath.Normalize(sourceFilePath) is not { } sourceFile)
        {
            return ImmutableArray<string>.Empty;
        }

        var folder = SqlPath.GetFolder(sourceFile);

        var target = path is null ? folder : SqlPath.Combine(folder, path);
        if (target is null)
        {
            return ImmutableArray<string>.Empty;
        }

        if (path is null || !SqlPath.IsSqlFile(path))
        {
            return SqlPath.FindInFolder(sqlPaths, target);
        }

        // The path as the project lists it, which may differ from the target in case.
        var index = SqlPath.IndexOf(sqlPaths, target, static listed => listed);
        return index < 0 ? ImmutableArray<string>.Empty : ImmutableArray.Create(sqlPaths[index]);
    }
}
