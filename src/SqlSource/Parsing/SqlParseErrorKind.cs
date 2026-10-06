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

    /// <summary>A directive is not recognised.  Argument: the directive as written.</summary>
    UnknownDirective,

    /// <summary>A <c>-- SqlSource:</c> marker has no directives.  No argument.</summary>
    EmptyDirectiveLine,

    /// <summary>
    /// A directive lacks a value it needs or has one it does not take.  Argument: the directive as written.
    /// </summary>
    InvalidDirectiveValue,

    /// <summary>Both validation directives appear in one scope.  Argument: the second directive as written.</summary>
    ConflictingDirectives,

    /// <summary>A block has no SQL.  No argument.</summary>
    EmptyBlock,

    /// <summary>A token's name is a reserved C# keyword.  Argument: the name.</summary>
    ReservedTokenName,
}
