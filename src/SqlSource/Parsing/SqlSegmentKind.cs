namespace SqlSource.Parsing;

/// <summary>
/// What a <see cref="SqlSegment" /> holds.
/// </summary>
internal enum SqlSegmentKind
{
    /// <summary>SQL text that is emitted as written.</summary>
    Literal,

    /// <summary>A <c>{{name}}</c> token.</summary>
    Token,
}
