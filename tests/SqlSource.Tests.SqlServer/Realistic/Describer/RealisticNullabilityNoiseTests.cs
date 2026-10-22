using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Describer;

// The noise of the describer over the realistic cases: the columns that are not null in truth and that the describer
// answers nullable or unknown.  Noise is safe, since the generated type is wider than it need be, and it is a cost
// the corpus keeps in view: the count is a budget for each major version, held in this file.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticNullabilityNoiseTests(SqlServerContainer sqlServer)
{
    // The budget of each recorded major, by the major.  One is lowered in the change that lowers the noise, as the
    // test below demands, and raised only in a change that adds cases, by how much and why in that commit.
    private static readonly Dictionary<int, int> Budgets = new() { [14] = 39, [17] = 44 };

    [Fact]
    public void Budgets_RecordedMajors_EachHoldOneAndNoOtherDoes() =>
        Budgets
            .Keys.Order()
            .ShouldBe(SqlServerContainer.RecordedMajors.Order(), "a recorded major and a budget differ");

    // A count that differs from the budget lists the noisy columns, each as the safety tests name a column, to read
    // against the cases' truths; the cases that changed are in the diff of their sidecars too.
    [Fact]
    public async Task Noise_OverTheCorpus_IsTheBudget()
    {
        sqlServer.SkipUnlessRecordedMajor("The budgets");
        var major = sqlServer.Major;
        var held = Budgets[major];

        var count = 0;
        var noisy = new List<string>();
        foreach (var found in RealisticCases.All.Where(candidate => candidate.Refusal is null))
        {
            if (!found.RunsOn(major))
            {
                continue;
            }

            var described = await RealisticDescribe.DescribeOnceAsync(found, sqlServer.RealisticConnectionString);
            var columns = RealisticDescriptions.ColumnsOf(found, described);
            var noisyOfCase = RealisticNoise.NoisyOf(found, columns);
            count += noisyOfCase.Count;
            noisy.AddRange(noisyOfCase.Select(index => $"{found} column {index} {found.Columns[index].Name}"));
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
            Assert.Fail(verdict + "\nThe noisy columns:\n" + string.Join('\n', noisy));
        }
    }
}
