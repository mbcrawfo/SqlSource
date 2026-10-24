namespace SqlSource.Tool.Planning;

/// <summary>
/// Whether the queries of a planned file can be described.
/// </summary>
internal enum PlannedFileState
{
    /// <summary>
    /// The file was parsed, and no query of it needs an entry or the file is saved as UTF-8 and its dialect can be
    /// described.
    /// </summary>
    Ready,

    /// <summary>The file has parse errors, which were reported, and so has no queries.</summary>
    HasParseErrors,

    /// <summary>A query of the file needs an entry and its dialect cannot be described (<c>SQLSRC209</c>).</summary>
    NotDescribable,

    /// <summary>A file that is not UTF-8 has a parse error or a query that needs an entry (<c>SQLSRC271</c>).</summary>
    NotUtf8,
}
