using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic.Describer;

// The noise of the describer over the realistic cases: the columns that are not null in truth and that the describer
// answers nullable or unknown.  Noise is safe, since the generated type is wider than it need be, and it is a cost
// the corpus keeps in view: the count is a budget for each major version, held in this file.
[Collection(UsesPostgres.Name)]
public sealed class RealisticNullabilityNoiseTests(PostgresContainer postgres)
{
    // The budget of each recorded major, by the major.  One is lowered in the change that lowers the noise, as the
    // test below demands, and raised only in a change that adds cases, by how much and why in that commit.
    private static readonly Dictionary<int, int> Budgets = new() { [14] = 32, [18] = 30 };

    [Fact]
    public void Budgets_RecordedMajors_EachHoldOneAndNoOtherDoes() =>
        Budgets
            .Keys.Order()
            .ShouldBe(EngineImage.Postgres.RecordedMajors.Order(), "a recorded major and a budget differ");

    [Fact]
    public async Task Noise_OverTheCorpus_IsTheBudget()
    {
        postgres.SkipUnlessRecordedMajor("The budgets");
        var major = postgres.Major;
        var held = Budgets[major];

        var count = 0;
        foreach (var found in RealisticCases.Corpus.All.Where(candidate => candidate.Refusal is null))
        {
            if (!found.RunsOn(major))
            {
                continue;
            }

            var described = await RealisticCases.Describe.DescribeOnceAsync(found, postgres.RealisticConnectionString);
            count += RealisticNoise.CountOf(found, RealisticDescriptions.ColumnsOf(found, described));
        }

        var verdict = RealisticNoise.Verdict(
            count,
            held,
            major,
            "a change made the describer answer nullable or unknown where it answered not null, and the cases that "
                + "changed fail RealisticDescriptionTests or are in the diff of their sidecars, query.sql.json; or, in "
                + $"a change that adds cases, raise the budget of {major} in Budgets to {count} and say why in the "
                + "commit."
        );
        if (verdict is not null)
        {
            Assert.Fail(verdict);
        }
    }
}
