using SqlSource.Generation.Types.Drivers;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Whether the project references a driver that a query's method can be generated for, of each engine: one that
/// <see cref="NpgsqlProbe" /> or <see cref="SqlClientProbe" /> finds and that is not below the floor.
/// </summary>
/// <param name="Postgres">Whether the methods of the <c>postgres</c> dialect can be generated.</param>
/// <param name="SqlServer">Whether the methods of the <c>mssql</c> dialect can be generated.</param>
internal readonly record struct MethodDrivers(bool Postgres, bool SqlServer)
{
    /// <summary>What the two probes say of the drivers.</summary>
    public static MethodDrivers Of(NpgsqlProbe npgsql, SqlClientProbe sqlClient) =>
        new(npgsql.Referenced && !npgsql.IsBelowTheFloor, sqlClient.Referenced && !sqlClient.IsBelowTheFloor);
}
