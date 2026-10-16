using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Tests;

// Describes of a query named Q, and the parameters of its list, for a test of the PostgreSQL describer.
internal static class PostgresRequests
{
    public static DescribeRequest Of(string sql, params SqlQueryParameter[] parameters) =>
        new("Q", sql, new EquatableArray<SqlQueryParameter>([.. parameters]));

    // A parameter whose type the server infers.
    public static SqlQueryParameter Inferred(string name) => new(name, null, null, false);

    // A parameter whose type a marker declares.
    public static SqlQueryParameter Declared(string name, string type) => new(name, type, null, true);
}
