namespace SqlSource.Parsing;

/// <summary>
/// The problems the parser reports.
/// </summary>
internal enum SqlParseErrorKind
{
    UnterminatedQuote,
    UnterminatedBlockComment,
    InvalidName,
    DuplicateName,
    InvalidFileName,
    SqlBeforeFirstName,
    SummaryBeforeFirstName,
    UnknownDirective,
    EmptyDirectiveLine,
    InvalidDirectiveValue,
    ConflictingDirectives,
    EmptyBlock,
    ReservedTokenName,
}
