namespace SqlSource.Parsing;

/// <summary>
/// Whether a string goes on after it closes, and what may stand in the gap before its next part.  A gap always
/// needs a line break.
/// </summary>
internal enum SqlStringContinuation
{
    /// <summary>A string ends at its closing quote.</summary>
    None,

    /// <summary>Only whitespace may stand in the gap, as in CockroachDB.</summary>
    AcrossWhitespace,

    /// <summary>Whitespace and line comments may stand in the gap, as in PostgreSQL.</summary>
    AcrossLineComments,
}
