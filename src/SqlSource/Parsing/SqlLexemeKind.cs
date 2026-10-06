namespace SqlSource.Parsing;

/// <summary>
/// The kinds of lexeme the <see cref="SqlLexer" /> produces.
/// </summary>
internal enum SqlLexemeKind
{
    /// <summary>Anything that is not one of the other kinds.</summary>
    Text,

    /// <summary>A string literal or a quoted identifier, delimiters included.</summary>
    Quoted,

    /// <summary><c>--</c> to the end of the line, without the line terminator.</summary>
    LineComment,

    /// <summary><c>/* ... */</c>, with any comments nested in it.</summary>
    BlockComment,

    /// <summary>A block comment that starts <c>/*+</c> or <c>/*!</c>.  Never stripped.</summary>
    Hint,
}
