namespace SqlSource.Parsing;

/// <summary>
/// The outcome of lexing one <c>.sql</c> file.
/// </summary>
/// <param name="Lexemes">The file's lexemes in order.  Empty when <paramref name="Error" /> is set.</param>
/// <param name="Error">The unterminated quote or comment that stopped the lexer, or null.</param>
internal sealed record SqlLexResult(EquatableArray<SqlLexeme> Lexemes, SqlParseError? Error);
