namespace SqlSource.Parsing;

/// <summary>
/// The outcome of scanning a block's SQL for tokens.
/// </summary>
/// <param name="Segments">The SQL as literal text and tokens, in order.</param>
/// <param name="Occurrences">
/// Each token of <paramref name="Segments" />, in order, with its place and its default.
/// </param>
/// <param name="Errors">Problems found.  Their spans are offsets into the scanned SQL, not into the file.</param>
internal sealed record TokenScanResult(
    EquatableArray<SqlSegment> Segments,
    EquatableArray<SqlTokenOccurrence> Occurrences,
    EquatableArray<SqlParseError> Errors
);
