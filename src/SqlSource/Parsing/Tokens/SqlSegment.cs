namespace SqlSource.Parsing.Tokens;

/// <summary>
/// One piece of a block's SQL: literal text, a token to be replaced, or a use of a list parameter.
/// </summary>
/// <param name="Kind">What the piece is.</param>
/// <param name="Text">
/// The literal text, the token's name without its braces, or a list's use as the SQL writes it, prefix included.
/// </param>
internal sealed record SqlSegment(SqlSegmentKind Kind, string Text);
