using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class DatabaseEngineExtensionsTests
{
    [Theory]
    [InlineData((int)DatabaseEngine.PostgreSql, (int)CodeGenEngines.Postgres)]
    [InlineData((int)DatabaseEngine.SqlServer, (int)CodeGenEngines.SqlServer)]
    public void ToCodeGenEngines_EachEngine_IsItsFlag(int engine, int expected) =>
        ((DatabaseEngine)engine).ToCodeGenEngines().ShouldBe((CodeGenEngines)expected);

    [Theory]
    [InlineData((int)DatabaseEngine.PostgreSql, "Npgsql")]
    [InlineData((int)DatabaseEngine.SqlServer, "Microsoft.Data.SqlClient")]
    public void DriverName_EachEngine_IsItsDriversPackage(int engine, string expected) =>
        ((DatabaseEngine)engine).DriverName().ShouldBe(expected);
}
