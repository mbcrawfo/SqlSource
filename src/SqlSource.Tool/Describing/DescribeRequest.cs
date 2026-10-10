using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One query to describe.
/// </summary>
/// <param name="Name">The query's name.</param>
/// <param name="Sql">The sample SQL: the query with each token's default in the token's place.</param>
/// <param name="Parameters">The query's parameter list, each with the type and the nullability it declares.</param>
internal sealed record DescribeRequest(string Name, string Sql, EquatableArray<SqlQueryParameter> Parameters);
