using SqlSource.Parsing.Markers;
using SqlSource.Settings.TypeOverrides;

namespace SqlSource.Parsing;

/// <summary>
/// The problems the parser reports.  Each member says what <see cref="SqlParseError.Arguments" /> holds for it.
/// </summary>
/// <remarks>
/// The parser's diagnostic ids follow the order of the members, so a new member goes at the end.
/// </remarks>
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
    /// which <see cref="TypeOverrideGrammar.BracketHelp" /> gives, and a query's <c>-- column:</c> that names a
    /// column's source, which says to map it in the file header.
    /// </summary>
    InvalidMarkerValue,

    /// <summary>
    /// Two settings of one scope contradict each other: <c>default</c> beside another generator parameter, two dialects
    /// in one file, two values of one setting marker, two declarations of one parameter, two defaults of one token, or
    /// two <c>-- column:</c> markers of one column that say different things.  Argument: the second one, as the first
    /// argument of <see cref="InvalidMarkerValue" />, or the token as written.
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

    /// <summary>
    /// A token and a comment overlap, so the SQL without comments and the SQL with them read it differently: a comment
    /// holds the <c>}}</c> of a default that is open in the SQL without comments, an error of the file; or, errors of
    /// the form with comments alone, a comment opens a default that the SQL after it closes, or stands in a token's
    /// head, between its braces and its colon or closing braces.  Argument: the token's name.
    /// </summary>
    TokenCrossesComment,

    /// <summary>
    /// A parameter of a SQL Server query is written in a spelling other than its first, a warning.  Arguments: this
    /// spelling and the first, each with its prefix.
    /// </summary>
    ParameterInTwoSpellings,
}
