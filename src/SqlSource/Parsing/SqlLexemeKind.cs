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

    /// <summary>
    /// <c>--</c> to the end of the line, without the line terminator.  Where the dialect has them, <c>#</c> to the end
    /// of the line too.
    /// </summary>
    LineComment,

    /// <summary><c>/* ... */</c>, with any comments nested in it where the dialect nests them.</summary>
    BlockComment,

    /// <summary>
    /// A block comment that starts <c>/*+</c> or <c>/*!</c>, and where the dialect has them one that starts
    /// <c>/*M!</c> or a line that starts <c>--+</c>.  Never stripped.
    /// </summary>
    Hint,

    /// <summary>
    /// A parameter: the dialect's prefix and a name, as in <c>@id</c>.  SQL content, copied as written.
    /// </summary>
    Parameter,
}
