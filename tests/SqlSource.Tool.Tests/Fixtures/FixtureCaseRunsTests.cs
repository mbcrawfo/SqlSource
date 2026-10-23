using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// The text of FixtureCaseRuns, which needs no server: what is compared between a live entry and an expected.json.
public sealed class FixtureCaseRunsTests
{
    [Fact]
    public void WithOneServerVersion_Entry_ReplacesTheVersionWithAStar()
    {
        var entry = /*lang=json,strict*/
            """{"server": {"serverVersion": "14.24", "name": "fixtures"}}""";

        FixtureCaseRuns
            .WithOneServerVersion(entry)
            .ShouldBe( /*lang=json,strict*/
                """{"server": {"serverVersion": "*", "name": "fixtures"}}"""
            );
    }

    [Fact]
    public void WithOneServerVersion_EntryWithoutAVersion_Fails()
    {
        var entry = /*lang=json,strict*/
            """{"name": "fixtures"}""";

        Should
            .Throw<ShouldAssertException>(() => FixtureCaseRuns.WithOneServerVersion(entry))
            .Message.ShouldContain("the entry has no serverVersion");
    }
}
