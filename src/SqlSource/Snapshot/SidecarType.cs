namespace SqlSource.Snapshot;

/// <summary>
/// A type as an engine describes it.  The entry's engine picks the shape: <see cref="PostgresType" />,
/// <see cref="SqlServerType" />, or <see cref="OtherEngineType" /> for an engine this version does not know.
/// </summary>
/// <param name="Name">The type as the engine spells it, facets included.</param>
internal abstract record SidecarType(string Name);
