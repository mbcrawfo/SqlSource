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
    /// A <c>-- summary:</c> or <c>-- generator:</c> marker has no SQL after it in its block.  No argument.
    /// </summary>
    MarkerAtEndOfBlock,

    /// <summary>A generator parameter is not recognised.  Argument: the generator parameter as written.</summary>
    UnknownGeneratorParameter,

    /// <summary>A <c>-- generator:</c> marker has no generator parameters.  No argument.</summary>
    EmptyGeneratorLine,

    /// <summary>
    /// A generator parameter lacks a value it needs, has one it does not take, or has one that is not valid.  Argument:
    /// the generator parameter as written.
    /// </summary>
    InvalidMarkerValue,

    /// <summary>
    /// Two generator parameters of one scope contradict each other: both validation generator parameters, or two
    /// dialects.  Argument: the second generator parameter as written.
    /// </summary>
    ConflictingSettings,

    /// <summary>A block has no SQL.  No argument.</summary>
    EmptyBlock,

    /// <summary>A token's name is a reserved C# keyword.  Argument: the name.</summary>
    ReservedTokenName,

    /// <summary>
    /// A <c>dialect=</c> generator parameter is inside a named query or after SQL.  Argument: the generator parameter
    /// as written.
    /// </summary>
    MisplacedDialect,
}
