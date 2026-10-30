using System;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using SqlSource.SettingsFiles;

namespace SqlSource.Generation.Methods;

/// <summary>
/// A library that a settings file turns on for a query method of an engine whose assemblies the project does not
/// reference: <c>SQLSRC281</c> without its position.  The position is found at the end, by <see cref="Locate" />, in
/// the settings file as it is read now, so that an edit that only moves the member types and plans nothing again.
/// </summary>
/// <param name="Path">The settings file's path as the compiler gives it.</param>
/// <param name="Library">The library that is on.</param>
/// <param name="Engine">The engine of the query methods it reaches.</param>
/// <param name="MissingAssemblies">The assemblies that the project would reference for the engine to use it.</param>
internal sealed record SettingsFileLibraryUse(
    string Path,
    SqlLibrary Library,
    DatabaseEngine Engine,
    string MissingAssemblies
)
{
    /// <summary>
    /// Every type's uses, each once, by the path, the library and the engine: two types or two queries that one place
    /// reaches give it alike.
    /// </summary>
    public static EquatableArray<SettingsFileLibraryUse> Distinct(
        ImmutableArray<EquatableArray<SettingsFileLibraryUse>> uses
    ) =>
        new(
            uses.SelectMany(static each => each)
                .Distinct()
                .OrderBy(static use => use.Path, StringComparer.Ordinal)
                .ThenBy(static use => use.Library)
                .ThenBy(static use => use.Engine)
                .ToImmutableArray()
        );

    /// <summary>
    /// <c>SQLSRC281</c> for each use, at the member of its settings file that gives the library's entry.  A use whose
    /// file or member <paramref name="settingsFiles" /> does not have is not reported, though the files it is planned
    /// from are always those of the run.
    /// </summary>
    public static EquatableArray<DiagnosticInfo> Locate(
        EquatableArray<SettingsFileLibraryUse> uses,
        SettingsFileSet settingsFiles
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>(uses.Count);
        foreach (var use in uses)
        {
            if (MemberOf(use, settingsFiles) is { } member)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.LibraryNotReferenced,
                        new LocationInfo(use.Path, member.Span, member.LineSpan),
                        MethodPlanner.LibraryName(use.Library),
                        use.Engine.DatabaseName(),
                        use.MissingAssemblies
                    )
                );
            }
        }

        return new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable());
    }

    // The member of the use's library in its settings file, found by the file's folder, which holds one file.
    private static LibraryMember? MemberOf(SettingsFileLibraryUse use, SettingsFileSet settingsFiles)
    {
        if (SqlPath.Normalize(use.Path) is not { } path)
        {
            return null;
        }

        var index = SqlPath.IndexOf(settingsFiles.Files, SqlPath.GetFolder(path), static file => file.NormalizedFolder);
        return index < 0 ? null : settingsFiles.Files[index].File.LibraryMemberOf(use.Library);
    }
}
