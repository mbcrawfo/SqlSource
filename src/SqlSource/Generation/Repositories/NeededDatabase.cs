using SqlSource.Settings;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// A database that a repository's queries use, which needs a <see cref="ConnectionSource" />.
/// </summary>
/// <param name="Name">The database's name, as <c>QuerySettings.ResolveDatabase</c> gives it.</param>
/// <param name="Engine">The engine of the dialect of the queries' files.</param>
internal readonly record struct NeededDatabase(string Name, DatabaseEngine Engine);
