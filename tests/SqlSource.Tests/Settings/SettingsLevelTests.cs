using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class SettingsLevelTests
{
    private static readonly SettingPlace Near = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);
    private static readonly SettingPlace Far = SettingPlace.OfSettingsFile("/sqlsource.json", 0);

    // Each engine's slot is keyed by its own rules, so a level over another merges the slots alone.
    [Fact]
    public void Over_TypeOverrides_MergesEachEngineAlone()
    {
        var higher = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only(
                DatabaseEngine.SqlServer,
                (EquatableArray<TypeOverride>?)TestSidecars.Of(new TypeOverride("int", "long", Near))
            ),
        };
        var lower = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only(
                DatabaseEngine.PostgreSql,
                (EquatableArray<TypeOverride>?)TestSidecars.Of(new TypeOverride("int4", "long", Far))
            ),
        };

        var level = higher.Over(lower);

        level
            .TypeOverrides[DatabaseEngine.PostgreSql]
            .ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "long", Far)]);
        level
            .TypeOverrides[DatabaseEngine.SqlServer]
            .ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int", "long", Near)]);
    }
}
