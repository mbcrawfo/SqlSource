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
    /// A marker has no SQL after it in its block.  No argument.
    /// </summary>
    MarkerAtEndOfBlock,

    /// <summary>A generator parameter is not recognised.  Argument: the generator parameter as written.</summary>
    UnknownGeneratorParameter,

    /// <summary>A <c>-- generator:</c> marker has no generator parameters.  No argument.</summary>
    EmptyGeneratorLine,

    /// <summary>
    /// A generator parameter lacks a value it needs, has one it does not take, or has one that is not valid; or a
    /// <c>-- dialect:</c> marker does not name a dialect.  Argument: the parameter as written, or the marker as
    /// <see cref="SqlDialectMarker.Describe" /> gives it.
    /// </summary>
    InvalidMarkerValue,

    /// <summary>
    /// Two settings contradict each other: both validation parameters in one scope, or two dialects in one file.
    /// Argument: the second one, as for <see cref="InvalidMarkerValue" />.
    /// </summary>
    ConflictingSettings,

    /// <summary>A block has no SQL.  No argument.</summary>
    EmptyBlock,

    /// <summary>A token's name is a reserved C# keyword.  Argument: the name.</summary>
    ReservedTokenName,

    /// <summary>A <c>-- dialect:</c> marker is inside a named query or after SQL.  No argument.</summary>
    MisplacedDialect,

    /// <summary>
    /// A marker stands where it is not allowed: one for a query in the preamble, or one for the file inside a query.
    /// Arguments: the marker's word, and where it is allowed.
    /// </summary>
    MarkerNotAllowedHere,

    /// <summary>
    /// A <c>-- param:</c> marker gives no type for a parameter that the query's SQL does not hold.  Argument: the
    /// parameter, with its prefix.
    /// </summary>
    MissingParameterType,

    /// <summary>
    /// A parameter stands only in the default of a token.  Argument: the parameter, with its prefix.
    /// </summary>
    UndeclaredParameter,

    /// <summary>
    /// A <c>-- input-model:</c> marker is on a query that has no parameters.  No argument.
    /// </summary>
    InputModelWithoutParameters,
}
