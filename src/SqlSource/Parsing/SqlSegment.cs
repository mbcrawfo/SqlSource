namespace SqlSource.Parsing;

/// <summary>
/// One piece of a block's SQL: literal text, or a token to be replaced.
/// </summary>
/// <param name="Kind">Whether this is literal text or a token.</param>
/// <param name="Text">The literal text, or the token's name without its braces.</param>
internal sealed record SqlSegment(SqlSegmentKind Kind, string Text);
