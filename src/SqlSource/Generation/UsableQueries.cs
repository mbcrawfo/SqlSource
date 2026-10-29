using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// Which files and queries of a type it can use, and the settings in effect for each query.
/// </summary>
internal static class UsableQueries
{
    /// <summary>
    /// Which of the type's files it can use.  A file contributes all of its queries or none: one that has a query
    /// with a name that cannot be used here (<c>SQLSRC008</c>, or <c>SQLSRC009</c> for a query whose output is not
    /// <see cref="OutputKind.CodeGen" />), or a query that keeps its comments
    /// here and has an error in the form with them (<see cref="SqlQuery.KeptErrors" />), is left out, with its
    /// diagnostics when <paramref name="diagnostics" /> is given.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="files">The type's files, in member order.</param>
    /// <param name="fileSettings">
    /// Each file's level, the chain of its folder, one for each of <paramref name="files" />.
    /// </param>
    /// <param name="diagnostics">Where the diagnostics go, or null when the caller does not report them.</param>
    /// <returns>One element for each of <paramref name="files" />: whether the type can use it.</returns>
    public static bool[] FindUsableFiles(
        TargetType type,
        EquatableArray<ParsedSqlFile> files,
        EquatableArray<SettingsLevel> fileSettings,
        ImmutableArray<DiagnosticInfo>.Builder? diagnostics
    )
    {
        var containingTypeName = type.Placement == MemberPlacement.Nested ? type.NestedClass : type.Name;
        var usable = new bool[files.Count];
        var fileOfName = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var isUsable = true;
            foreach (var query in file.Queries)
            {
                // A CodeGen query has no member in the type that contains the others, and the methods' planner judges
                // its method's name.  The settings are resolved only for a query of that name, which is rare.
                if (
                    string.Equals(query.Name, containingTypeName, StringComparison.Ordinal)
                    && QuerySettings.Resolve(query.Markers, type.Settings, fileSettings[index]).Output
                        != OutputKind.CodeGen
                )
                {
                    diagnostics?.Add(
                        DiagnosticInfo.Create(
                            SqlDiagnostics.QueryNamedLikeContainingType,
                            query.NameLocation,
                            type.Name,
                            query.Name
                        )
                    );
                    isUsable = false;
                }
                else if (fileOfName.TryGetValue(query.Name, out var firstFile))
                {
                    diagnostics?.Add(
                        DiagnosticInfo.Create(
                            SqlDiagnostics.DuplicateQueryName,
                            query.NameLocation,
                            query.Name,
                            firstFile,
                            type.Name
                        )
                    );
                    isUsable = false;
                }

                // The settings are resolved only for a query whose kept form has an error, which is rare.
                if (
                    query.KeptErrors.Count > 0
                    && QuerySettings.Resolve(query.Markers, type.Settings, fileSettings[index]).KeepComments
                )
                {
                    diagnostics?.AddRange(query.KeptErrors);
                    isUsable = false;
                }
            }

            if (!isUsable)
            {
                continue;
            }

            usable[index] = true;
            foreach (var query in file.Queries)
            {
                fileOfName.Add(query.Name, file.FileName);
            }
        }

        return usable;
    }

    /// <summary>
    /// Each query of the files the type can use, in file order and then query order, with its indices and the
    /// settings in effect for it.  The files that <see cref="FindUsableFiles" /> leaves out give their diagnostics to
    /// <paramref name="diagnostics" /> when it is given, before this returns.  The sequence resolves each query's
    /// settings as it is walked and holds no list, so it can be walked again at no cost beyond the resolving.
    /// </summary>
    /// <param name="type">The type with its parsed files.</param>
    /// <param name="diagnostics">Where the diagnostics go, or null when the caller does not report them.</param>
    public static UsableQuerySequence Of(TypeQueries type, ImmutableArray<DiagnosticInfo>.Builder? diagnostics) =>
        new(type, FindUsableFiles(type.Type.Type, type.Files, type.FileSettings, diagnostics));
}
