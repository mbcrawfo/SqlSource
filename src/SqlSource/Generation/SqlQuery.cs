using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// One query of a <c>.sql</c> file that becomes a constant.
/// </summary>
/// <param name="Name">The query's name, a valid C# identifier.</param>
/// <param name="NameLocation">
/// Where the name is in the file.  The start of the file when the name comes from the file name.
/// </param>
/// <param name="Summary">The text of the query's <c>-- summary:</c> markers, or null when it has none.</param>
/// <param name="Sql">The SQL.</param>
internal sealed record SqlQuery(string Name, LocationInfo NameLocation, string? Summary, string Sql);
