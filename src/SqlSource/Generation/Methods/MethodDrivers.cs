using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Whether the project references a driver that a query's method can be generated for, of each engine: one that
/// <see cref="NpgsqlProbe" /> or <see cref="SqlClientProbe" /> finds and that is not below the floor.
/// </summary>
/// <param name="Postgres">Whether the methods of the <c>postgres</c> dialect can be generated.</param>
/// <param name="SqlServer">Whether the methods of the <c>mssql</c> dialect can be generated.</param>
/// <param name="PostgresAtLeast10">
/// Whether the project references Npgsql 10 or later, which decides the target of a PostgreSQL query's converters,
/// <see cref="Converters.ConverterTargets.Npgsql10" /> or <see cref="Converters.ConverterTargets.NpgsqlBefore10" />.
/// </param>
internal readonly record struct MethodDrivers(bool Postgres, bool SqlServer, bool PostgresAtLeast10)
{
    /// <summary>What the two probes say of the drivers.</summary>
    public static MethodDrivers Of(NpgsqlProbe npgsql, SqlClientProbe sqlClient) =>
        new(
            npgsql.Referenced && !npgsql.IsBelowTheFloor,
            sqlClient.Referenced && !sqlClient.IsBelowTheFloor,
            npgsql.AtLeast10
        );

    /// <summary>Whether the methods of the engine's queries can be generated.</summary>
    public bool Allows(DatabaseEngine engine) => engine == DatabaseEngine.SqlServer ? SqlServer : Postgres;
}
