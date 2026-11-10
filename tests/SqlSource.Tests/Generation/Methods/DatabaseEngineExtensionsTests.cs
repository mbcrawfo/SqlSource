using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class DatabaseEngineExtensionsTests
{
    [Theory]
    [InlineData((int)DatabaseEngine.PostgreSql, (int)MethodEngines.Postgres)]
    [InlineData((int)DatabaseEngine.SqlServer, (int)MethodEngines.SqlServer)]
    public void ToMethodEngines_EachEngine_IsItsFlag(int engine, int expected) =>
        ((DatabaseEngine)engine).ToMethodEngines().ShouldBe((MethodEngines)expected);

    [Theory]
    [InlineData((int)DatabaseEngine.PostgreSql, "Npgsql")]
    [InlineData((int)DatabaseEngine.SqlServer, "Microsoft.Data.SqlClient")]
    public void DriverName_EachEngine_IsItsDriversPackage(int engine, string expected) =>
        ((DatabaseEngine)engine).DriverName().ShouldBe(expected);

    [Theory]
    [InlineData((int)DatabaseEngine.PostgreSql, "global::Npgsql.NpgsqlConnection")]
    [InlineData((int)DatabaseEngine.SqlServer, "global::Microsoft.Data.SqlClient.SqlConnection")]
    public void ConnectionTypeText_Engine_IsTheDriversConnection(int engine, string expected) =>
        ((DatabaseEngine)engine).ConnectionTypeText().ShouldBe(expected);
}
