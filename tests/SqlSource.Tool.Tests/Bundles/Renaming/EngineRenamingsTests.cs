using Shouldly;
using SqlSource.Settings;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

public sealed class EngineRenamingsTests
{
    [Fact]
    public void For_PostgreSql_IsPostgresRenaming() =>
        EngineRenamings.For(DatabaseEngine.PostgreSql).ShouldBeSameAs(PostgresRenaming.Instance);

    [Fact]
    public void For_SqlServer_IsNotBuiltYet() => EngineRenamings.For(DatabaseEngine.SqlServer).ShouldBeNull();

    [Fact]
    public void For_NoEngine_Throws() =>
        Should.Throw<UnexpectedEnumValueException>(() => EngineRenamings.For((DatabaseEngine)99));
}
