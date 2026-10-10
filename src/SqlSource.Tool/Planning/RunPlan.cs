namespace SqlSource.Tool.Planning;

/// <summary>
/// What a run is on: every file that a type claims, and the databases of the queries that need an entry.
/// </summary>
/// <param name="Files">The files, in the order of their projects and then of their paths.</param>
/// <param name="Databases">The databases, in the order the plan first holds a query of each.</param>
internal sealed record RunPlan(EquatableArray<PlannedFile> Files, EquatableArray<PlannedDatabase> Databases);
