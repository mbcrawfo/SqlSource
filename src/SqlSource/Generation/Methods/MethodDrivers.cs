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
/// <param name="SqlServerSpatial">
/// Whether the project references NetTopologySuite and NetTopologySuite.IO.SqlServerBytes, so that a SQL Server
/// query's <c>geography</c> or <c>geometry</c> read as one of NetTopologySuite's geometries takes the spatial read
/// path.
/// </param>
internal readonly record struct MethodDrivers(
    bool Postgres,
    bool SqlServer,
    bool PostgresAtLeast10,
    bool SqlServerSpatial = false
)
{
    /// <summary>
    /// What the two probes say of the drivers, and the probe of the libraries, read without its versions, of
    /// NetTopologySuite's packages.
    /// </summary>
    public static MethodDrivers Of(NpgsqlProbe npgsql, SqlClientProbe sqlClient, LibraryProbe libraries) =>
        new(
            npgsql.Referenced && !npgsql.IsBelowTheFloor,
            sqlClient.Referenced && !sqlClient.IsBelowTheFloor,
            npgsql.AtLeast10,
            libraries.NetTopologySuite && libraries.SqlServerBytes
        );

    /// <summary>Whether the methods of the engine's queries can be generated.</summary>
    public bool Allows(DatabaseEngine engine) => engine == DatabaseEngine.SqlServer ? SqlServer : Postgres;
}
