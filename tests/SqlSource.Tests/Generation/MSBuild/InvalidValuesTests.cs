using Shouldly;
using SqlSource.Generation.MSBuild;
using Xunit;

namespace SqlSource.Tests.Generation.MSBuild;

public sealed class InvalidValuesTests
{
    [Fact]
    public void Dialects_AddedOutOfOrderAndTwice_AreEachOnceInOrdinalOrder()
    {
        var invalid = new InvalidValues();

        invalid.AddDialect("zeta");
        invalid.AddDialect(null);
        invalid.AddDialect("Beta");
        invalid.AddDialect("beta");
        invalid.AddDialect("zeta");

        invalid.Dialects.ShouldBe(["Beta", "beta", "zeta"]);
    }

    [Fact]
    public void Settings_AddedOutOfOrderAndTwice_AreEachOnceByNameThenValue()
    {
        var invalid = new InvalidValues();

        invalid.AddSettings([new InvalidSetting("SqlSourceOutput", "x"), new InvalidSetting("SqlSourceDialect", "b")]);
        invalid.AddSettings([new InvalidSetting("SqlSourceOutput", "a"), new InvalidSetting("SqlSourceOutput", "x")]);

        invalid.Settings.ShouldBe([
            new InvalidSetting("SqlSourceDialect", "b"),
            new InvalidSetting("SqlSourceOutput", "a"),
            new InvalidSetting("SqlSourceOutput", "x"),
        ]);
    }
}
