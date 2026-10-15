using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// The noise of the describer over the realistic cases: the columns that are not null in truth and that the describer
// answers nullable or unknown.  Noise is safe, since the generated type is wider than it need be, and it is a cost
// the corpus keeps in view: the count is a budget for each major version, held in this file.
[Collection(UsesPostgres.Name)]
public sealed class RealisticNoiseBudgetTests(PostgresContainer postgres)
{
    // The budget of each major version that holds one.  It is lowered in the change that lowers the noise, as the
    // test below demands, and raised only in a change that adds cases, by how much and why in that commit.
    private const int Budget14 = 94;

    private const int Budget18 = 95;

    [Fact]
    public async Task Noise_OverTheCorpus_IsTheBudget()
    {
        var major = await RealisticRun.ServerMajorAsync(postgres.RealisticConnectionString);
        if (!RealisticRun.RecordedMajors.Contains(major))
        {
            Assert.Skip("The budget is held for PostgreSQL 14 and 18 alone.");
        }

        var held = major == 14 ? Budget14 : Budget18;
        var count = 0;
        foreach (var found in RealisticCases.All.Where(candidate => candidate.Refusal is null))
        {
            if (!RealisticRun.RunsOn(found, major))
            {
                continue;
            }

            var described = await RealisticRun.DescribeAsync(found, postgres.RealisticConnectionString);
            var columns = RealisticRun.ColumnsOf(found, described);
            count += found.Columns.Where((truth, index) => !truth.Nullable && columns[index].Nullable != false).Count();
        }

        if (count > held)
        {
            Assert.Fail(
                $"The noise is {count}, over the budget of {held}: a change made the describer answer nullable or "
                    + "unknown where it answered not null, and the cases that changed are in their "
                    + $"expected-pg{major}.txt; or, in a change that adds cases, raise Budget{major} to {count} and "
                    + "say why in the commit."
            );
        }

        if (count < held)
        {
            Assert.Fail(
                $"The noise is {count}, under the budget of {held}: lower Budget{major} to {count} in this change, "
                    + "so the improvement is kept."
            );
        }
    }
}
