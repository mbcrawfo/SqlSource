using System;
using System.Collections.Generic;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tests.Postgres.Describer;

// A request as the plan of a run makes one, from SQL a test writes: the parameters of the SQL in the order they
// first appear, then the declared ones that the SQL does not hold, as SqlParameterList orders a query's list.
internal static class Requests
{
    public const string Query = "Q";

    public static DescribeRequest Of(string sql, params (string Name, string Type)[] declared)
    {
        var parameters = new List<SqlQueryParameter>();
        foreach (var lexeme in SqlLexer.Lex(sql, SqlDialectRules.PostgreSql).Lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                Add(parameters, sql.Substring(lexeme.Span.Start + 1, lexeme.Span.Length - 1), declared);
            }
        }

        foreach (var (name, _) in declared)
        {
            Add(parameters, name, declared);
        }

        return new DescribeRequest(Query, sql, new EquatableArray<SqlQueryParameter>([.. parameters]));
    }

    private static void Add(List<SqlQueryParameter> parameters, string name, (string Name, string Type)[] declared)
    {
        if (parameters.Exists(parameter => Same(parameter.Name, name)))
        {
            return;
        }

        var index = Array.FindIndex(declared, found => Same(found.Name, name));
        parameters.Add(
            index < 0
                ? new SqlQueryParameter(name, null, null, false)
                : new SqlQueryParameter(name, declared[index].Type, null, true)
        );
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
