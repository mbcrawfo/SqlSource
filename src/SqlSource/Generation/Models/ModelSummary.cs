using System.Collections.Generic;
using System.Text;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Paths;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Models;

/// <summary>
/// Writes the XML <c>&lt;summary&gt;</c> of a model, which names each query that joined it, and each table of a table
/// model.
/// </summary>
internal static class ModelSummary
{
    // The first query's -- summary:, escaped, then on a line of its own the sentence that says what the model is and
    // names each query that joined it once, in order:
    // "A row of the queries <c>A</c> from <c>F.sql</c> and <c>B</c> from <c>G.sql</c>."
    // A table model says whose rows they are, naming each distinct table once and only the queries whose request has
    // one, then the other queries that joined it in a sentence of their own:
    // "A row of the table <c>public.users</c>, read by the query <c>A</c> from <c>F.sql</c>. Also a row of the query
    // <c>Users</c> from <c>G.sql</c>."
    // Two types that claim one query ask for its model twice, and the requests of a query are next to each other.
    public static string Write(ModelRole role, List<ModelRequest> requests, StringBuilder text)
    {
        var tables = new List<SidecarTable>();
        var tableQueries = new List<ModelSource>(requests.Count);
        var otherQueries = new List<ModelSource>(requests.Count);
        foreach (var request in requests)
        {
            if (request.Table is { } table)
            {
                if (!tables.Contains(table))
                {
                    tables.Add(table);
                }

                AddQuery(tableQueries, request.Source);
            }
            else
            {
                AddQuery(otherQueries, request.Source);
            }
        }

        _ = text.Clear();
        if (requests[0].Source.Summary is { } summary)
        {
            _ = text.Append(XmlDocWriter.Escape(summary)).Append('\n');
        }

        if (role == ModelRole.Input)
        {
            _ = text.Append("The parameters of ");
            AppendQueries(text, otherQueries);
        }
        else if (tables.Count == 0)
        {
            _ = text.Append("A row of ");
            AppendQueries(text, otherQueries);
        }
        else
        {
            _ = text.Append(tables.Count == 1 ? "A row of the table " : "A row of the tables ");
            for (var index = 0; index < tables.Count; index++)
            {
                AppendSeparator(text, index, tables.Count);
                _ = text.Append("<c>")
                    .Append(XmlDocWriter.Escape(tables[index].Schema ?? ""))
                    .Append('.')
                    .Append(XmlDocWriter.Escape(tables[index].Table ?? ""))
                    .Append("</c>");
            }

            _ = text.Append(", read by ");
            AppendQueries(text, tableQueries);
            if (otherQueries.Count > 0)
            {
                _ = text.Append(" Also a row of ");
                AppendQueries(text, otherQueries);
            }
        }

        return text.ToString();
    }

    // A query once: the requests of a query are next to each other.
    private static void AddQuery(List<ModelSource> queries, ModelSource source)
    {
        if (queries.Count == 0 || !IsSameQuery(queries[queries.Count - 1], source))
        {
            queries.Add(source);
        }
    }

    // "the query <c>A</c> from <c>F.sql</c>." or, for several, "the queries <c>A</c> from <c>F.sql</c> and <c>B</c>
    // from <c>G.sql</c>."
    private static void AppendQueries(StringBuilder text, List<ModelSource> queries)
    {
        _ = text.Append(queries.Count == 1 ? "the query " : "the queries ");
        for (var index = 0; index < queries.Count; index++)
        {
            AppendSeparator(text, index, queries.Count);
            _ = text.Append("<c>")
                .Append(queries[index].QueryName)
                .Append("</c> from <c>")
                .Append(XmlDocWriter.Escape(queries[index].FileName))
                .Append("</c>");
        }

        _ = text.Append('.');
    }

    // Before the item at the index of a list of the count: nothing, " and " or ", ".
    private static void AppendSeparator(StringBuilder text, int index, int count)
    {
        if (index > 0)
        {
            _ = text.Append(index == count - 1 ? " and " : ", ");
        }
    }

    /// <summary>
    /// Whether the two sources are of one query: its index, in one file by <see cref="SqlPath.Comparer" />.
    /// </summary>
    public static bool IsSameQuery(ModelSource left, ModelSource right) =>
        left.QueryIndex == right.QueryIndex && SqlPath.Comparer.Equals(left.NormalizedPath, right.NormalizedPath);
}
