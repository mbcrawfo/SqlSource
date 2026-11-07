using Shouldly;
using SqlSource.Generation.Types;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Tests.Settings.TestPlaces;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generation.Types;

public sealed class FileTypesInputTests
{
    private static readonly TypeOverride Entry = new("int", "long", SettingsFile);

    // A level whose only type setting is one engine's override must survive, whichever engine reads it.
    [Fact]
    public void TypeSettingsOf_LevelWithOnlyASqlServerTypeOverride_KeepsIt()
    {
        var level = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only(
                DatabaseEngine.SqlServer,
                (EquatableArray<TypeOverride>?)TestSidecars.Of(Entry)
            ),
        };

        var result = FileTypesInput.TypeSettingsOf(level);

        ReferenceEquals(result, SettingsLevel.None).ShouldBeFalse();
        result.TypeOverrides[DatabaseEngine.SqlServer].ShouldNotBeNull().ShouldBe([Entry]);
        result.TypeOverrides[DatabaseEngine.PostgreSql].ShouldBeNull();
    }

    [Fact]
    public void TypeSettingsOf_LevelWithOnlyAPostgreSqlTypeOverride_KeepsIt()
    {
        var level = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only(
                DatabaseEngine.PostgreSql,
                (EquatableArray<TypeOverride>?)TestSidecars.Of(Entry)
            ),
        };

        var result = FileTypesInput.TypeSettingsOf(level);

        ReferenceEquals(result, SettingsLevel.None).ShouldBeFalse();
        result.TypeOverrides[DatabaseEngine.PostgreSql].ShouldNotBeNull().ShouldBe([Entry]);
        result.TypeOverrides[DatabaseEngine.SqlServer].ShouldBeNull();
    }

    [Fact]
    public void TypeSettingsOf_LevelWithNoTypeSetting_IsNone()
    {
        var level = new SettingsLevel { Output = OutputKind.Models };

        var result = FileTypesInput.TypeSettingsOf(level);

        ReferenceEquals(result, SettingsLevel.None).ShouldBeTrue();
    }

    [Fact]
    public void TypeSettingsOf_LevelWithOtherSettings_KeepsOnlyTheTypeSettings()
    {
        var level = new SettingsLevel
        {
            TypeOverrides = PerEngine.Only(
                DatabaseEngine.SqlServer,
                (EquatableArray<TypeOverride>?)TestSidecars.Of(Entry)
            ),
            Output = OutputKind.Models,
            Database = "main",
        };

        var result = FileTypesInput.TypeSettingsOf(level);

        result.TypeOverrides[DatabaseEngine.SqlServer].ShouldNotBeNull().ShouldBe([Entry]);
        result.Output.ShouldBeNull();
        result.Database.ShouldBeNull();
    }
}
