using System;
using System.Text;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The SQL a database is asked to describe.
/// </summary>
internal static class SampleSql
{
    /// <summary>
    /// The query's SQL without comments, which the hash covers, with each token's resolved default in the place of
    /// its <c>{{name:default}}</c>: nothing, for an empty one.  A list parameter is described as one value of its
    /// element: a use after <c>IN</c> is <c>(@ids)</c>.  The test of the list for <c>NULL</c> asks the server nothing
    /// of the parameter, and is a value that a planner cannot fold, whose condition stays a condition of the plan as
    /// the running query's boolean parameter does: on PostgreSQL <c>(SELECT NULL)</c>, since a folded
    /// <c>NULL IS NOT NULL OR o.total &gt; 0</c> is the strict <c>o.total &gt; 0</c>, which makes a <c>LEFT JOIN</c> on
    /// <c>o</c> inner.  On SQL Server it is <c>NULL</c>, since SQL Server refuses a subquery in an aggregate, a
    /// <c>GROUP BY</c> and an <c>OUTPUT</c> clause, and describes a column's nullability without a plan.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="dialect">The dialect it is described in, one of <see cref="SqlDescribableDialects.All" />.</param>
    /// <exception cref="InvalidOperationException">
    /// A token has no default.  The plan gives such a query a problem, and the run does not describe it.
    /// </exception>
    public static string Build(SqlQuery query, SqlDialect dialect)
    {
        var nullTest = SqlDescribableDialects.EngineOf(dialect) switch
        {
            DatabaseEngine.PostgreSql => "(SELECT NULL)",
            DatabaseEngine.SqlServer => "NULL",
            _ => throw UnexpectedEnumValueException.Of(dialect),
        };
        var sql = new StringBuilder();
        foreach (var segment in query.Segments)
        {
            _ = sql.Append(
                segment.Kind switch
                {
                    SqlSegmentKind.Literal => segment.Text,
                    SqlSegmentKind.List => "(" + segment.Text + ")",
                    SqlSegmentKind.ListNullTest => nullTest,
                    SqlSegmentKind.Token => DefaultOf(query, segment.Text),
                    _ => throw UnexpectedEnumValueException.Of(segment.Kind),
                }
            );
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
