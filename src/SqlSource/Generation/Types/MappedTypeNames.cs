using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Generation.MSBuild;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Generation.Types;

/// <summary>
/// The C# types that the type override and the column types name, at any level, and that the <c>-- param:</c> and
/// <c>-- column:</c> markers of a query give, which the compilation is asked for.
/// </summary>
internal static class MappedTypeNames
{
    /// <summary>
    /// The distinct types of every entry of the property, of each file's level and of the markers of every query, and
    /// of each query's parameters and result's columns, in ordinal order.  A file's level is the chain of its folder
    /// over its metadata.  An entry of <c>default</c> names none.
    /// </summary>
    /// <remarks>
    /// A query's markers are over its file header's, so the header's entries are read through its queries.  The files
    /// of a folder without metadata share their chain's level, and are sorted by path, so a level read for the file
    /// before is not read again.  A list of the names alone, which an edit that leaves them as they were gives again,
    /// equal.
    /// </remarks>
    public static EquatableArray<string> Collect(
        ProjectSettings project,
        EquatableArray<FileLevel> files,
        EquatableArray<ParsedSqlFile> parsed
    )
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        Add(names, project.Level);
        SettingsLevel? previous = null;
        for (var i = 0; i < files.Count; i++)
        {
            var level = files[i].Level;
            if (!ReferenceEquals(level, previous))
            {
                Add(names, level);
            }

            previous = level;
        }

        foreach (var file in parsed)
        {
            foreach (var query in file.Queries)
            {
                Add(names, query.Markers);
                foreach (var parameter in query.Parameters)
                {
                    if (parameter.ClrType is { } type)
                    {
                        _ = names.Add(type);
                    }
                }

                foreach (var marker in query.ColumnMarkers)
                {
                    if (marker.ClrType is { } type)
                    {
                        _ = names.Add(type);
                    }
                }
            }
        }

        return names.Count == 0 ? EquatableArray<string>.Empty : new EquatableArray<string>(names.ToImmutableArray());
    }

    // The types of both engines' entries: an entry of an MSBuild value is in each, and names its type once in the set.
    private static void Add(SortedSet<string> names, SettingsLevel level)
    {
        Add(names, level.TypeOverrides);
        Add(names, level.SqlServerTypeOverrides);
        if (level.ColumnTypes is not { } columnTypes)
        {
            return;
        }

        foreach (var entry in columnTypes)
        {
            if (entry.Type is { } type)
            {
                _ = names.Add(type);
            }
        }
    }

    private static void Add(SortedSet<string> names, EquatableArray<TypeOverride>? overrides)
    {
        if (overrides is not { } entries)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (entry.Type is { } type)
            {
                _ = names.Add(type);
            }
        }
    }
}
