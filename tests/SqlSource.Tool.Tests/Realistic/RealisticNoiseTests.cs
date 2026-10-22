using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The noise of a case, the columns that are not null in truth and that the describer answers nullable or unknown,
// and what the count over a corpus says against its budget.
public sealed class RealisticNoiseTests
{
    [Fact]
    public void CountOf_Columns_CountsANotNullTruthDescribedNullableOrUnknown()
    {
        var found = RealisticTestCases.Keyed() with
        {
            Columns =
            [
                new TruthColumn("nullable", false, null, null),
                new TruthColumn("unknown", false, null, null),
                new TruthColumn("not_null", false, null, null),
                new TruthColumn("truly_nullable", true, "w", null),
            ],
        };

        RealisticNoise
            .CountOf(
                found,
                [
                    Column(0, "nullable", true),
                    Column(1, "unknown", null),
                    Column(2, "not_null", false),
                    Column(3, "truly_nullable", true),
                ]
            )
            .ShouldBe(2);
    }

    [Fact]
    public void NoisyOf_Columns_GivesThePositionsOfANotNullTruthDescribedNullableOrUnknown()
    {
        var found = RealisticTestCases.Keyed() with
        {
            Columns =
            [
                new TruthColumn("not_null", false, null, null),
                new TruthColumn("nullable", false, null, null),
                new TruthColumn("truly_nullable", true, "w", null),
                new TruthColumn("unknown", false, null, null),
            ],
        };

        RealisticNoise
            .NoisyOf(
                found,
                [
                    Column(0, "not_null", false),
                    Column(1, "nullable", true),
                    Column(2, "truly_nullable", null),
                    Column(3, "unknown", null),
                ]
            )
            .ShouldBe([1, 3]);
    }

    [Fact]
    public void Verdict_CountOfTheBudget_IsNone() => RealisticNoise.Verdict(5, 5, 14, "h").ShouldBeNull();

    [Fact]
    public void Verdict_CountOverTheBudget_SaysSoAndGivesTheHint() =>
        RealisticNoise
            .Verdict(6, 5, 14, "h")
            .ShouldNotBeNull()
            .ShouldStartWith("The noise is 6, over the budget of 5: h");

    [Fact]
    public void Verdict_CountUnderTheBudget_SaysToLowerTheBudgetOfTheMajor() =>
        RealisticNoise.Verdict(4, 5, 14, "h").ShouldNotBeNull().ShouldContain("lower the budget of 14 in Budgets to 4");

    private static readonly PostgresType Integer = new(
        "integer",
        SidecarValues.Kind.Base,
        "pg_catalog",
        "int4",
        null,
        null,
        null,
        null,
        null,
        null,
        null
    );

    private static SidecarColumn Column(int ordinal, string name, bool? nullable) =>
        new(ordinal, name, Integer, nullable, null, null, null, null);
}
