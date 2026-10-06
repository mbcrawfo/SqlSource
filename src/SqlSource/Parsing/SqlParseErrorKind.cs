namespace SqlSource.Parsing;

/// <summary>
/// The problems the parser reports.  Each member says what <see cref="SqlParseError.Arguments" /> holds for it.
/// </summary>
internal enum SqlParseErrorKind
{
    /// <summary>A quoted region is not closed.  No argument.</summary>
    UnterminatedQuote,

    /// <summary>A block comment or hint is not closed.  No argument.</summary>
    UnterminatedBlockComment,

    /// <summary>
    /// A <c>-- name:</c> value is not a usable C# identifier.  Argument: the value, which may be empty.
    /// </summary>
    InvalidName,

    /// <summary>A name is used twice in the file.  Argument: the name.</summary>
    DuplicateName,

    /// <summary>
    /// The file has no name marker and its name is not a usable C# identifier.  Argument: the file name.
    /// </summary>
    InvalidFileName,

    /// <summary>There is SQL before the first name marker.  No argument.</summary>
    SqlBeforeFirstName,

    /// <summary>There is a <c>-- summary:</c> marker before the first name marker.  No argument.</summary>
    SummaryBeforeFirstName,

    /// <summary>
    /// A <c>-- summary:</c> or <c>-- SqlSource:</c> marker has no SQL after it in its block.  No argument.
    /// </summary>
    MarkerAtEndOfBlock,

    /// <summary>A directive is not recognised.  Argument: the directive as written.</summary>
    UnknownDirective,

    /// <summary>A <c>-- SqlSource:</c> marker has no directives.  No argument.</summary>
    EmptyDirectiveLine,

    /// <summary>
    /// A directive lacks a value it needs, has one it does not take, or has one that is not valid.  Argument: the
    /// directive as written.
    /// </summary>
    InvalidDirectiveValue,

    /// <summary>
    /// Two directives of one scope contradict each other: both validation directives, or two dialects.  Argument: the
    /// second directive as written.
    /// </summary>
    ConflictingDirectives,

    /// <summary>A block has no SQL.  No argument.</summary>
    EmptyBlock,

    /// <summary>A token's name is a reserved C# keyword.  Argument: the name.</summary>
    ReservedTokenName,

    /// <summary>
    /// A <c>dialect=</c> directive is inside a named query or after SQL.  Argument: the directive as written.
    /// </summary>
    MisplacedDialect,
}
