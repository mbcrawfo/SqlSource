using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The settings of the planner that a request of "explain" may carry: a closed list, so that nothing else reaches a
// SET LOCAL.
public class PlannerSettingsTests
{
    [Theory]
    [InlineData("enable_hashjoin")]
    [InlineData("enable_mergejoin")]
    [InlineData("enable_nestloop")]
    [InlineData("enable_memoize")]
    [InlineData("enable_material")]
    public void SetText_SettingOfTheList_IsItsSetLocal(string name)
    {
        PlannerSettings.SetText(name + "=off").ShouldBe($"SET LOCAL {name} = off");
        PlannerSettings.SetText(name + "=on").ShouldBe($"SET LOCAL {name} = on");
    }

    [Theory]
    [InlineData("work_mem=1")]
    [InlineData("enable_seqscan=off")]
    [InlineData("enable_hashjoin=maybe")]
    [InlineData("enable_hashjoin=off; DROP TABLE users")]
    [InlineData("enable_hashjoin")]
    [InlineData("ENABLE_HASHJOIN=off")]
    [InlineData(" enable_hashjoin=off")]
    [InlineData("")]
    public void SetText_AnythingElse_Throws(string setting) =>
        Should.Throw<ArgumentException>(() => PlannerSettings.SetText(setting));

    [Fact]
    public void Checked_SettingsOfTheList_AreGivenBackInTheirOrder() =>
        PlannerSettings
            .Checked(["enable_nestloop=off", "enable_hashjoin=off"])
            .ShouldBe(["enable_nestloop=off", "enable_hashjoin=off"]);

    [Fact]
    public void Checked_NameOutsideTheList_Throws() =>
        Should.Throw<ArgumentException>(() => PlannerSettings.Checked(["enable_nestloop=off", "work_mem=1"]));

    [Fact]
    public void None_IsEmpty() => PlannerSettings.None.ShouldBeEmpty();
}
