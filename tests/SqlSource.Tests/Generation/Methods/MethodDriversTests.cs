using Shouldly;
using SqlSource.Generation.Methods;
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
        new MethodDrivers(postgres, sqlServer).Allows((DatabaseEngine)engine).ShouldBe(expected);

    [Theory]
    [InlineData((int)DatabaseEngine.PostgreSql, (int)CodeGenEngines.Postgres)]
    [InlineData((int)DatabaseEngine.SqlServer, (int)CodeGenEngines.SqlServer)]
    public void ToCodeGenEngines_EachEngine_IsItsFlag(int engine, int expected) =>
        ((DatabaseEngine)engine).ToCodeGenEngines().ShouldBe((CodeGenEngines)expected);
}
