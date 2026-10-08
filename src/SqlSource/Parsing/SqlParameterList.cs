using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Builds the parameter list of one query: the parameters of its SQL in order of first appearance, compared
/// ignoring case.
/// </summary>
internal static class SqlParameterList
{
    public static EquatableArray<SqlQueryParameter> Create(SqlBlockText sql)
    {
        if (sql.Parameters.Length == 0)
        {
            return EquatableArray<SqlQueryParameter>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<SqlQueryParameter>();
        foreach (var span in sql.Parameters)
        {
            if (IndexOf(parameters, sql.Text, span) >= 0)
            {
                continue;
            }

            parameters.Add(new SqlQueryParameter(NameOf(sql.Text, span), null, null, false));
        }

        return new EquatableArray<SqlQueryParameter>(parameters.ToImmutable());
    }

    // The span holds the prefix, which is one character.
    private static string NameOf(string sql, TextSpan span) => sql.Substring(span.Start + 1, span.Length - 1);

    private static int IndexOf(ImmutableArray<SqlQueryParameter>.Builder parameters, string sql, TextSpan span)
    {
        var name = sql.AsSpan(span.Start + 1, span.Length - 1);
        for (var index = 0; index < parameters.Count; index++)
        {
            if (name.Equals(parameters[index].Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
