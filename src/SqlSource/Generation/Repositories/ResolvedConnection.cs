namespace SqlSource.Generation.Repositories;

/// <summary>
/// The connection of one database of a repository.
/// </summary>
/// <param name="Database">The database's name, as the first of its queries spells it.</param>
/// <param name="Source">The member that gives its connection.</param>
internal sealed record ResolvedConnection(string Database, ConnectionSource Source);
