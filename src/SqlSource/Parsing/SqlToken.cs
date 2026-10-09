namespace SqlSource.Parsing;

/// <summary>
/// One token of a query, with the default its query gives it.
/// </summary>
/// <param name="Name">The token's name.</param>
/// <param name="Default">
/// The sample that stands in the token's place when the query is described: inline or from a <c>-- token:</c> marker.
/// Empty for a default that is given and holds nothing, null for a token without one.  It reaches no generated code.
/// </param>
internal sealed record SqlToken(string Name, string? Default);
