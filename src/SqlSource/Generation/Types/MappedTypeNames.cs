using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Generation.MSBuild;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Generation.Types;

/// <summary>The C# types that the type override names, at any level, which the compilation is asked for.</summary>
internal static class MappedTypeNames
{
    /// <summary>
    /// The distinct types of every entry of the property, of each file's metadata and of the markers of every query,
    /// in ordinal order.  An entry of <c>default</c> names none.
    /// </summary>
    /// <remarks>
    /// A query's markers are over its file header's, so the header's entries are read through its queries.  A list
    /// of the names alone, which an edit that leaves them as they were gives again, equal.
    /// </remarks>
    public static EquatableArray<string> Collect(
        ProjectSettings project,
        EquatableArray<FileSettings> files,
        EquatableArray<ParsedSqlFile> parsed
    )
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        Add(names, project.Level);
        foreach (var file in files)
        {
            Add(names, file.Level);
        }

        foreach (var file in parsed)
        {
            foreach (var query in file.Queries)
            {
                Add(names, query.Markers);
            }
        }

        return names.Count == 0 ? EquatableArray<string>.Empty : new EquatableArray<string>(names.ToImmutableArray());
    }

    // The types of both engines' entries: an entry of an MSBuild value is in each, and names its type once in the set.
    private static void Add(SortedSet<string> names, SettingsLevel level)
    {
        Add(names, level.TypeOverrides);
        Add(names, level.SqlServerTypeOverrides);
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
