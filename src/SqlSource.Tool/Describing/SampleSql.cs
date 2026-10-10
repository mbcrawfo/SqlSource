using System;
using System.Text;
using SqlSource.Generation;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The SQL a database is asked to describe.
/// </summary>
internal static class SampleSql
{
    /// <summary>
    /// The query's SQL without comments, with each token's resolved default in the token's place: nothing, for an
    /// empty one.  This is the text the query's hash is made from, with the defaults written out.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A token has no default.  The plan gives such a query a problem, and the run does not describe it.
    /// </exception>
    public static string Build(SqlQuery query)
    {
        var sql = new StringBuilder();
        foreach (var segment in query.Segments)
        {
            _ = sql.Append(segment.Kind == SqlSegmentKind.Literal ? segment.Text : DefaultOf(query, segment.Text));
        }

        return sql.ToString();
    }

    // Names are compared as SqlQueryHash compares them.
    private static string DefaultOf(SqlQuery query, string name)
    {
        foreach (var token in query.Tokens)
        {
            if (token.Name == name && token.Default is { } text)
            {
                return text;
            }
        }

        throw new InvalidOperationException($"The token '{name}' of the query '{query.Name}' has no default.");
    }
}
