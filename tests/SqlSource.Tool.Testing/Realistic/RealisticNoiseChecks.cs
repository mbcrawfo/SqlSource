using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// The noise of the describer over the realistic cases, as RealisticNoise counts it, held to a budget for each recorded
// major.  Both engines' RealisticNullabilityNoiseTests run it with their Budgets, on the server of that major and the
// connection to its database "realistic", once the test has skipped a major that is not recorded.
internal sealed class RealisticNoiseChecks(
    RealisticCorpus corpus,
    RealisticDescribe describe,
    int major,
    string connection
)
{
    public static void ShouldHoldOneBudgetForEachRecordedMajor(
        IReadOnlyDictionary<int, int> budgets,
        IEnumerable<int> recordedMajors
    ) => budgets.Keys.Order().ShouldBe(recordedMajors.Order(), "a recorded major and a budget differ");

    // A count that differs from the budget lists the noisy columns, each as the safety checks name a column, to read
    // against the cases' truths; the cases that changed are in the diff of their sidecars too.
    public async Task ShouldBeTheBudgetAsync(IReadOnlyDictionary<int, int> budgets)
    {
        var held = budgets[major];

        var count = 0;
        var noisy = new List<string>();
        foreach (var found in corpus.All.Where(candidate => candidate.Refusal is null))
        {
            if (!found.RunsOn(major))
            {
                continue;
            }

            var described = await describe.DescribeOnceAsync(found, connection);
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
