using SqlSource.Parsing.Markers;

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
    /// A generator parameter has a value, which none takes; or a <c>-- dialect:</c> or <c>-- name:</c> marker, or
    /// a marker that <see cref="SqlMarkerScope" /> reads, lacks a value it needs or has one that is not valid.
    /// Two arguments: the generator parameter as written, or the marker as <see cref="SqlMarkerReader.Describe" />
    /// gives it; and the help that ends the message, empty but for a type override whose key is written in brackets,
    /// which <see cref="Settings.TypeOverrideGrammar.BracketHelp" /> gives.
    /// </summary>
    InvalidMarkerValue,

    /// <summary>
    /// Two settings of one scope contradict each other: <c>default</c> beside another generator parameter, two
    /// dialects in one file, two values of one setting marker, two declarations of one parameter, or two defaults of
    /// one token.  Argument: the second one, as the first argument of <see cref="InvalidMarkerValue" />, or the token
    /// as written.
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
