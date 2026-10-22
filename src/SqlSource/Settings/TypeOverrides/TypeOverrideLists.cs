namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// The entries of one MSBuild value of the type override, read by each engine's rules.  An entry whose key one engine
/// does not read is absent from that engine's list.
/// </summary>
/// <param name="PostgreSql">
/// The entries keyed by PostgreSQL's rules, in the order of <see cref="TypeOverrideMap" />.
/// </param>
/// <param name="SqlServer">The entries keyed by SQL Server's rules, in the same order.</param>
internal readonly record struct TypeOverrideLists(
    EquatableArray<TypeOverride> PostgreSql,
    EquatableArray<TypeOverride> SqlServer
);
