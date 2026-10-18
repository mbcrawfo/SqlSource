using SqlSource.Settings;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// A query that needs an entry in the sidecar of its file.
/// </summary>
/// <param name="Name">The query's name.</param>
/// <param name="Output">Its output: never <see cref="OutputKind.Sql" />.</param>
internal sealed record QueryNeed(string Name, OutputKind Output);
