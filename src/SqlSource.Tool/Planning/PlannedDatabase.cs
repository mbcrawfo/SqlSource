using SqlSource.Parsing;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One logical database of a run.
/// </summary>
/// <param name="Name">The name, in the spelling the plan first holds.  Names are compared ignoring case.</param>
/// <param name="Dialect">The dialect of its queries, which picks the driver of its connection.</param>
internal sealed record PlannedDatabase(string Name, SqlDialect Dialect);
