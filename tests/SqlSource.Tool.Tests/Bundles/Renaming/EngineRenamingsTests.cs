using Shouldly;
using SqlSource.Settings;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

public sealed class EngineRenamingsTests
{
    [Fact]
    public void For_SqlServer_IsSqlServersPart() =>
        EngineRenamings.For(DatabaseEngine.SqlServer).ShouldBeOfType<SqlServerRenaming>();

    [Fact]
    public void For_PostgreSql_IsNotBuiltYet() => EngineRenamings.For(DatabaseEngine.PostgreSql).ShouldBeNull();

    [Fact]
    public void For_NoEngine_Throws() =>
        Should.Throw<UnexpectedEnumValueException>(() => EngineRenamings.For((DatabaseEngine)99));
}
