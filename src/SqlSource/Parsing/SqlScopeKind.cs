namespace SqlSource.Parsing;

/// <summary>
/// What a <see cref="SqlMarkerScope" /> is the scope of.  It decides which markers the scope takes, and a scope
/// never changes what it is.
/// </summary>
internal enum SqlScopeKind
{
    /// <summary>What comes before the first <c>-- name:</c> marker of a file that has one.</summary>
    Preamble,

    /// <summary>One query of a file that has a <c>-- name:</c> marker.</summary>
    Query,

    /// <summary>
    /// A file with no <c>-- name:</c> marker, which is one query and its own preamble: it takes what either takes.
    /// </summary>
    WholeFile,
}
