using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Tests.Settings.TestPlaces;

namespace SqlSource.Tests.Settings;

public sealed class SettingsLevelTests
{
    // Each engine's slot is keyed by its own rules, so a level over another merges the slots alone.
    [Fact]
    public void Over_TypeOverrides_MergesEachEngineAlone()
    {
        var higher = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only(
                DatabaseEngine.SqlServer,
                (EquatableArray<TypeOverride>?)TestSidecars.Of(new TypeOverride("int", "long", SettingsFile))
            ),
        };
        var lower = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only(
                DatabaseEngine.PostgreSql,
                (EquatableArray<TypeOverride>?)TestSidecars.Of(new TypeOverride("int4", "long", RootSettingsFile))
            ),
        };

        var level = higher.Over(lower);

        level
            .TypeOverrides[DatabaseEngine.PostgreSql]
            .ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "long", RootSettingsFile)]);
        level
            .TypeOverrides[DatabaseEngine.SqlServer]
            .ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int", "long", SettingsFile)]);
    }
}
