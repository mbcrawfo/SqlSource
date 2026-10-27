using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Generation.Paths;
using SqlSource.Settings;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// Which queries need an entry in their file's sidecar: one whose output, resolved for some type that claims its
/// file, is not <see cref="OutputKind.Sql" />.
/// </summary>
internal static class SidecarNeeds
{
    /// <summary>
    /// What <paramref name="type" /> needs of each of its files, with the output it resolves for each query.  A file
    /// in which it needs nothing is left out.  A type with problems of its own counts as well: the <c>sqlsource</c>
    /// tool describes its files all the same, so their entries are not orphans.
    /// </summary>
    /// <param name="type">The type and its parsed files.</param>
    public static EquatableArray<FileNeeds> OfType(TypeQueries type)
    {
        var attribute = type.Type.Type.Settings;
        var files = ImmutableArray.CreateBuilder<FileNeeds>();
        for (var index = 0; index < type.Files.Count; index++)
        {
            var file = type.Files[index];
            var queries = ImmutableArray.CreateBuilder<QueryNeed>();
            foreach (var query in file.Queries)
            {
                var output = QuerySettings.Resolve(query.Markers, attribute, type.FileSettings[index]).Output;
                if (output != OutputKind.Sql)
                {
                    queries.Add(new QueryNeed(query.Name, output));
                }
            }

            if (queries.Count > 0)
            {
                files.Add(new FileNeeds(file.NormalizedPath, new EquatableArray<QueryNeed>(queries.ToImmutable())));
            }
        }

        return new EquatableArray<FileNeeds>(files.ToImmutable());
    }

    /// <summary>
    /// The needs of every type, one for each file: the union of what each type needs of it, each query with the
    /// greatest of its outputs, in the ordinal order of the queries' names, so that the value does not depend on the
    /// order the types come in.  Sorted by <see cref="SqlPath.Comparer" />, so that a file's needs are found with a
    /// search.
    /// </summary>
    public static EquatableArray<FileNeeds> Merge(ImmutableArray<FileNeeds> files) =>
        new(
            files
                .GroupBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .Select(static group => Union(group.Key, group))
                .OrderBy(static file => file.NormalizedPath, SqlPath.Comparer)
                .ToImmutableArray()
        );

    private static FileNeeds Union(string path, IEnumerable<FileNeeds> needs)
    {
        var outputs = new SortedDictionary<string, OutputKind>(StringComparer.Ordinal);
        foreach (var file in needs)
        {
            foreach (var query in file.Queries)
            {
                if (!outputs.TryGetValue(query.Name, out var output) || query.Output > output)
                {
                    outputs[query.Name] = query.Output;
                }
            }
        }

        return new FileNeeds(
            path,
            new EquatableArray<QueryNeed>(
                outputs.Select(static pair => new QueryNeed(pair.Key, pair.Value)).ToImmutableArray()
            )
        );
    }
}
