using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Describer;

// The noise of the describer over the realistic cases: the columns that are not null in truth and that the describer
// answers nullable or unknown.  Noise is safe, since the generated type is wider than it need be, and it is a cost
// the corpus keeps in view: the count is a budget for each major version, held in this file.
[Collection(UsesPostgres.Name)]
public sealed class RealisticNullabilityNoiseTests(PostgresContainer postgres)
{
    // The budget of each recorded major, by the major.  One is lowered in the change that lowers the noise, as the
    // test below demands, and raised only in a change that adds cases, by how much and why in that commit.
    private static readonly Dictionary<int, int> Budgets = new() { [14] = 81, [18] = 82 };

    [Fact]
    public void Budgets_RecordedMajors_EachHoldOneAndNoOtherDoes() =>
        Budgets.Keys.Order().ShouldBe(RealisticCase.RecordedMajors.Order(), "a recorded major and a budget differ");

    [Fact]
    public async Task Noise_OverTheCorpus_IsTheBudget()
    {
        var major = postgres.Major;
        if (!Budgets.TryGetValue(major, out var held))
        {
            Assert.Skip("The budget is held for PostgreSQL 14 and 18 alone.");
        }

        var count = 0;
        foreach (var found in RealisticCases.All.Where(candidate => candidate.Refusal is null))
        {
            if (!found.RunsOn(major))
            {
                continue;
            }

            var described = await RealisticDescribe.DescribeOnceAsync(found, postgres.RealisticConnectionString);
            var columns = RealisticDescribe.ColumnsOf(found, described);
            count += found.Columns.Where((truth, index) => !truth.Nullable && columns[index].Nullable != false).Count();
        }

        if (count > held)
        {
            Assert.Fail(
                $"The noise is {count}, over the budget of {held}: a change made the describer answer nullable or "
                    + "unknown where it answered not null, and the cases that changed are in their "
                    + $"expected-pg{major}.txt; or, in a change that adds cases, raise the budget of {major} in "
                    + $"Budgets to {count} and say why in the commit."
            );
        }

        if (count < held)
        {
            Assert.Fail(
                $"The noise is {count}, under the budget of {held}: lower the budget of {major} in Budgets to "
                    + $"{count} in this change, so the improvement is kept."
            );
        }
    }
}
