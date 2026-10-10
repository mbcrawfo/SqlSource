namespace SqlSource.Tool.Planning;

/// <summary>
/// Whether the queries of a planned file can be described.
/// </summary>
internal enum PlannedFileState
{
    /// <summary>The file was parsed, and its dialect can be described or no query of it needs an entry.</summary>
    Ready,

    /// <summary>The file has parse errors, which were reported, and so has no queries.</summary>
    HasParseErrors,

    /// <summary>A query of the file needs an entry and its dialect cannot be described (<c>SQLSRC209</c>).</summary>
    NotDescribable,
}
