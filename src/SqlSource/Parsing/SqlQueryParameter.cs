namespace SqlSource.Parsing;

/// <summary>
/// One parameter of a query.
/// </summary>
/// <param name="Name">
/// The name without its prefix: as the SQL first writes it, or as the <c>-- param:</c> marker writes it when the SQL
/// does not hold the parameter.
/// </param>
/// <param name="Type">The database type a <c>-- param:</c> marker gives, as written, or null.</param>
/// <param name="Nullable">
/// True when a marker says <c>null</c>, false when it says <c>not null</c>, and null when it says neither or there is
/// no marker.  Null and false both mean the parameter is not nullable.
/// </param>
/// <param name="IsDeclared">Whether a <c>-- param:</c> marker names the parameter.</param>
/// <param name="ClrType">
/// The C# type that a <c>-- param:</c> marker gives after <c>-&gt;</c>, as <c>ClrTypeName.Text</c> writes it, or null.
/// It describes nothing to the database, so the hash leaves it out.
/// </param>
internal sealed record SqlQueryParameter(
    string Name,
    string? Type,
    bool? Nullable,
    bool IsDeclared,
    string? ClrType = null
);
