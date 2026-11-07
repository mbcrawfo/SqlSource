namespace SqlSource.Parsing.Tokens;

/// <summary>
/// One piece of a block's SQL: literal text, a token to be replaced, or a use of a list parameter.
/// </summary>
/// <param name="Kind">What the piece is.</param>
/// <param name="Text">
/// The literal text, the token's name without its braces, or a list's use as the SQL writes it, prefix included.
/// </param>
/// <param name="EmptyIsNull">
/// For a list's use, whether it is written empty as one <c>NULL</c>, <c>(@ids_0)</c>, rather than as
/// <c>(SELECT @ids_0 WHERE 1 = 0)</c>.  It is no part of the hash, which reads <paramref name="Text" /> alone.
/// </param>
internal sealed record SqlSegment(SqlSegmentKind Kind, string Text, bool EmptyIsNull = false);
