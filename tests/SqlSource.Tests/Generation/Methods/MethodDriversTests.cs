using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodDriversTests
{
    [Theory]
    [InlineData(false, false, (int)DatabaseEngine.PostgreSql, false)]
    [InlineData(false, false, (int)DatabaseEngine.SqlServer, false)]
    [InlineData(false, true, (int)DatabaseEngine.PostgreSql, false)]
    [InlineData(false, true, (int)DatabaseEngine.SqlServer, true)]
    [InlineData(true, false, (int)DatabaseEngine.PostgreSql, true)]
    [InlineData(true, false, (int)DatabaseEngine.SqlServer, false)]
    [InlineData(true, true, (int)DatabaseEngine.PostgreSql, true)]
    [InlineData(true, true, (int)DatabaseEngine.SqlServer, true)]
    public void Allows_EachEngine_IsItsDriversFlag(bool postgres, bool sqlServer, int engine, bool expected) =>
        new MethodDrivers(postgres, sqlServer, false).Allows((DatabaseEngine)engine).ShouldBe(expected);

    // Npgsql 10 is a target of its own for a library's converters.
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Of_Npgsql10_SetsPostgresAtLeast10(bool atLeast10, bool expected) =>
        MethodDrivers
            .Of(
                new NpgsqlProbe(true, true, atLeast10, null),
                new SqlClientProbe(false, false, false, null),
                new LibraryProbe(false, false, null)
            )
            .PostgresAtLeast10.ShouldBe(expected);

    // SQL Server's spatial read path needs NetTopologySuite and its reader and writer of SQL Server's bytes.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Of_NetTopologySuiteAndSqlServerBytes_SetsSqlServerSpatial(
        bool netTopologySuite,
        bool sqlServerBytes,
        bool expected
    ) =>
        MethodDrivers
            .Of(
                new NpgsqlProbe(false, false, false, null),
                new SqlClientProbe(true, true, false, null),
                new LibraryProbe(false, false, null, NetTopologySuite: netTopologySuite, SqlServerBytes: sqlServerBytes)
            )
            .SqlServerSpatial.ShouldBe(expected);
}
