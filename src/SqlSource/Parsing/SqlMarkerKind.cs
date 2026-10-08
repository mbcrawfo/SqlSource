namespace SqlSource.Parsing;

/// <summary>
/// The marker comments the parser gives meaning to.
/// </summary>
internal enum SqlMarkerKind
{
    /// <summary><c>-- name:</c> starts a block.</summary>
    Name,

    /// <summary><c>-- summary:</c> documents a block.</summary>
    Summary,

    /// <summary><c>-- generator:</c> carries generator parameters.</summary>
    GeneratorParameters,

    /// <summary><c>-- dialect:</c> names the dialect of the file.</summary>
    Dialect,

    /// <summary><c>-- token:</c> gives a token of its query a default.</summary>
    Token,

    /// <summary><c>-- token-ignore:</c> names a token of its query that stays literal text.</summary>
    TokenIgnore,

    /// <summary>
    /// <c>-- param:</c> declares a parameter of its query: its type, whether it is nullable, or both.
    /// </summary>
    Param,
}
