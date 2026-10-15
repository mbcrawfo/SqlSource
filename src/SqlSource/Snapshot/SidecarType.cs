namespace SqlSource.Snapshot;

/// <summary>
/// A type as an engine describes it.  The entry's engine picks the shape: <see cref="PostgresType" />,
/// <see cref="SqlServerType" />, or <see cref="OtherEngineType" /> for an engine this version does not know.
/// </summary>
/// <param name="Name">The type as the engine spells it, facets included.</param>
internal abstract record SidecarType(string Name)
{
    /// <summary>
    /// The name by which the engine's .NET driver binds a parameter of this type, as the driver takes it, or null.  It
    /// is set only on a parameter's own type, never on a column's nor on a type nested in another, and it never holds
    /// a modifier.  It is a member of the base record and not a positional one, so that each engine's shape keeps its
    /// constructor.  It takes part in equality: a different name binds differently.
    /// </summary>
    public string? BindName { get; init; }
}
