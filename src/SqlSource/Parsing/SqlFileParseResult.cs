namespace SqlSource.Parsing;

/// <summary>
/// The outcome of parsing one <c>.sql</c> file.
/// </summary>
/// <param name="Blocks">The file's queries.  Empty when <paramref name="Errors" /> is not.</param>
/// <param name="Errors">The problems found, ordered by position.</param>
/// <param name="Dialect">
/// The dialect the file was read by: the one it was given, or the one its <c>-- dialect:</c> marker names.
/// </param>
internal sealed record SqlFileParseResult(
    EquatableArray<SqlBlock> Blocks,
    EquatableArray<SqlParseError> Errors,
    SqlDialect Dialect
);
