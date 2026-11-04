using Shouldly;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Types.Drivers;

public sealed class DriverProbesTests
{
    [Fact]
    public void Of_EachEngine_IsItsDriversProbe()
    {
        var npgsql = new NpgsqlProbe(true, true, false, null);
        var sqlClient = new SqlClientProbe(true, true, false, null);

        DriverProbes.Of(DatabaseEngine.PostgreSql, npgsql, sqlClient).ShouldBeSameAs(npgsql);
        DriverProbes.Of(DatabaseEngine.SqlServer, npgsql, sqlClient).ShouldBeSameAs(sqlClient);
    }
}
