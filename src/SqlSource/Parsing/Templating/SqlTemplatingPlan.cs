namespace SqlSource.Parsing.Templating;

/// <summary>
/// What the sqlfluff templater must replace in one <c>.sql</c> file, read as the parser reads it.
/// </summary>
/// <param name="Dialect">The dialect the file is read by: the one it was given, or the one its marker names.</param>
/// <param name="Lexemes">The file's lexemes.  Empty when the lexer stopped.</param>
/// <param name="Spans">
/// The spans, sorted by start and then by kind.  A parameter inside a token's default comes after the token and lies
/// inside its span.  Empty when <paramref name="Error" /> is set.
/// </param>
/// <param name="Error">
/// The first error by position that stops the plan: the lexer's, that of an invalid <c>-- dialect:</c> marker, or
/// <c>SQLSRC120</c>.  Null when the plan is exact.
/// </param>
/// <param name="HasOtherErrors">
/// Whether the file has another error.  The spans are then the parser's best effort.
/// </param>
internal sealed record SqlTemplatingPlan(
    SqlDialectChoice Dialect,
    EquatableArray<SqlLexeme> Lexemes,
    EquatableArray<SqlTemplatingSpan> Spans,
    SqlParseError? Error,
    bool HasOtherErrors
);
