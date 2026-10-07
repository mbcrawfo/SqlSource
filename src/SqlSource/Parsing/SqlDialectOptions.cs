using System;

namespace SqlSource.Parsing;

/// <summary>
/// Settings of a database server that change one rule of its dialect.  Only MySQL and MariaDB have any.
/// </summary>
[Flags]
internal enum SqlDialectOptions
{
    /// <summary>The dialect as the server reads it by default.</summary>
    None = 0,

    /// <summary>
    /// The SQL mode <c>ANSI_QUOTES</c>: <c>"..."</c> is a quoted identifier, and a backslash does not escape in it.
    /// </summary>
    AnsiQuotes = 1,

    /// <summary>
    /// The SQL mode <c>NO_BACKSLASH_ESCAPES</c>: a backslash is an ordinary character in every quoted region.
    /// </summary>
    NoBackslashEscapes = 2,
}
