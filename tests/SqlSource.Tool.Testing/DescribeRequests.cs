using System;
using System.Collections.Generic;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Testing;

// A DescribeRequest for a query named Q, the way a test of a describer writes one.  Of makes one as the plan of a
// run does, from SQL a test writes; Listed takes the parameters as the test gives them.
internal static class DescribeRequests
{
    public const string Query = "Q";

    // A request from SQL, read by the rules of PostgreSQL.
    public static DescribeRequest Of(string sql, params (string Name, string Type)[] declared) =>
        Of(SqlDialectRules.PostgreSql, sql, declared);

    // A request as the plan of a run makes one: the parameters of the SQL in the order they first appear, then the
    // declared ones that the SQL does not hold, as SqlParameterList orders a query's list.
    public static DescribeRequest Of(SqlDialectRules rules, string sql, params (string Name, string Type)[] declared)
    {
        var parameters = new List<SqlQueryParameter>();
        foreach (var lexeme in SqlLexer.Lex(sql, rules).Lexemes)
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

        return Listed(sql, [.. parameters]);
    }

    // A request whose parameters are those the test gives, in its order, whatever the SQL holds.
    public static DescribeRequest Listed(string sql, params SqlQueryParameter[] parameters) =>
        new(Query, sql, new EquatableArray<SqlQueryParameter>([.. parameters]));

    // A parameter whose type the server infers.
    public static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    // A parameter whose type a marker declares.
    public static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);

    private static void Add(List<SqlQueryParameter> parameters, string name, (string Name, string Type)[] declared)
    {
        if (parameters.Exists(parameter => Same(parameter.Name, name)))
        {
            return;
        }

        var index = Array.FindIndex(declared, found => Same(found.Name, name));
        parameters.Add(index < 0 ? Inferred(name) : Declared(name, declared[index].Type));
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
