using SqlSource.Diagnostics;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// One <c>.sql</c> file that a type claims, parsed.
/// </summary>
/// <param name="NormalizedPath">The path in the form <see cref="SqlPath.Normalize" /> gives.</param>
/// <param name="FileName">The file's name with its extension.</param>
/// <param name="Queries">The file's queries, in file order.  Empty when <paramref name="Errors" /> is not.</param>
/// <param name="Errors">The file's problems, located in the file.</param>
/// <param name="Dialect">The dialect the file was read by.</param>
/// <param name="InvalidDialect">
/// The <c>SqlSourceDialect</c> metadata of the file as written when it is not a dialect, and null otherwise.  It has
/// no position, so it does not travel in <paramref name="Errors" />.
/// </param>
internal sealed record ParsedSqlFile(
    string NormalizedPath,
    string FileName,
    EquatableArray<SqlQuery> Queries,
    EquatableArray<DiagnosticInfo> Errors,
    SqlDialect Dialect,
    string? InvalidDialect = null
);
