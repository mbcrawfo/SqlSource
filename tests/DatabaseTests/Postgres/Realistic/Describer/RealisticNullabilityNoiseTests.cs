using System.Collections.Generic;
using System.Threading.Tasks;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic.Describer;

// The noise of the describer over the realistic cases: the columns that are not null in truth and that the describer
// answers nullable or unknown.  Noise is safe, since the generated type is wider than it need be, and it is a cost
// the corpus keeps in view: the count is a budget for each major version, held in this file.  The bodies are the
// checks of tests/SqlSource.Tool.Testing/Realistic/, which list the noisy columns when the count is not the budget.
[Collection(UsesPostgres.Name)]
public sealed class RealisticNullabilityNoiseTests(PostgresDatabases postgres)
{
    // The budget of each recorded major, by the major.  One is lowered in the change that lowers the noise, as the
    // test below demands, and raised only in a change that adds cases, by how much and why in that commit.
    private static readonly Dictionary<int, int> Budgets = new() { [14] = 32, [18] = 30 };

    private readonly RealisticNoiseChecks _checks = new(
        RealisticCases.Corpus,
        RealisticCases.Describe,
        postgres.Major,
        postgres.RealisticConnectionString
    );

    [Fact]
    public void Budgets_RecordedMajors_EachHoldOneAndNoOtherDoes() =>
        RealisticNoiseChecks.ShouldHoldOneBudgetForEachRecordedMajor(Budgets, EngineImage.Postgres.RecordedMajors);

    [Fact]
    public async Task Noise_OverTheCorpus_IsTheBudget()
    {
        postgres.SkipUnlessRecordedMajor("The budgets");
        await _checks.ShouldBeTheBudgetAsync(Budgets);
    }
}
