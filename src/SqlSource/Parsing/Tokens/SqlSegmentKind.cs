namespace SqlSource.Parsing.Tokens;

/// <summary>
/// What a <see cref="SqlSegment" /> holds.
/// </summary>
internal enum SqlSegmentKind
{
    /// <summary>SQL text that is emitted as written.</summary>
    Literal,

    /// <summary>A <c>{{name}}</c> token.</summary>
    Token,

    /// <summary>
    /// A use of a list parameter directly after <c>IN</c>, as the SQL writes it, prefix included: <c>@ids</c>.  A query
    /// method writes the list's values in its place.  Everything else writes it as literal text.
    /// </summary>
    List,

    /// <summary>
    /// A use of a list parameter before <c>IS NULL</c> or <c>IS NOT NULL</c>, as the SQL writes it, prefix included.
    /// It is literal text everywhere: a query method sends a parameter of that name that says whether the list is null.
    /// </summary>
    ListNullTest,
}
