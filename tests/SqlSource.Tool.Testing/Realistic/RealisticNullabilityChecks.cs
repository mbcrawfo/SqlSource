using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// The run of a case's query over the seed with one sample bound, by the engine's oracle: it reads the database through
// the engine's driver, so the engine hands it to the checks.
internal delegate Task<NullabilityRun> RealisticSampleRun(
    DescribeRequest request,
    IReadOnlyList<DescribedParameter> parameters,
    IReadOnlyDictionary<string, string> sample
);

// That the describer's nullability is safe over each realistic case: it says not null of no column that can be NULL,
// checked against the case's truth and against the seed by the engine's oracle, which runs each sample.  Neither reads
// what the committed sidecar holds of the case, so a committed wrong not null still fails here.  A case is described
// once in a run, and each check reads the one description.  Both engines' RealisticNullabilitySafetyTests run it, on
// the server of that major and the connection to its database "realistic".
internal sealed class RealisticNullabilityChecks(
    RealisticDescribe describe,
    int major,
    string connection,
    RealisticSampleRun run
)
{
    public async Task ShouldBeNotNullOnlyWhereTheTruthIsAsync(string name)
    {
        var found = describe.Start(name, major);
        var (columns, _) = await describe.DescribedAsync(found, connection);

        for (var index = 0; index < columns.Count; index++)
        {
            var truth = found.Columns[index];
            if (truth.Nullable && columns[index].Nullable == false)
            {
                Assert.Fail($"{ColumnOf(found, index)} is not null, and can be NULL: {truth.Why}");
            }
        }
    }

    public async Task ShouldReturnNoNullWhereEitherSaysNotNullAsync(string name)
    {
        var found = describe.Start(name, major);
        var (columns, parameters) = await describe.DescribedAsync(found, connection);
        var request = RealisticDescribe.RequestOf(found);
        RealisticSampleChecks.CheckKeys(found, parameters.Select(static parameter => parameter.Name));
        RealisticSampleChecks.CheckNulls(found, request.Parameters);

        var sawNull = new bool[columns.Count];
        foreach (var sample in found.Samples)
        {
            var ran = await run(request, parameters, sample);

            ran.Names.ShouldBe(columns.Select(static column => column.Name), $"{found} names its columns otherwise");
            for (var index = 0; index < sawNull.Length; index++)
            {
                sawNull[index] |= ran.SawNull[index];
            }
        }

        for (var index = 0; index < sawNull.Length; index++)
        {
            var truth = found.Columns[index];
            var column = ColumnOf(found, index);
            if (columns[index].Nullable == false && sawNull[index])
            {
                Assert.Fail($"{column} is not null, and can be NULL: it held a NULL over the seed");
            }

            if (!truth.Nullable && sawNull[index])
            {
                Assert.Fail($"{column} is not null in truth, and held a NULL over the seed: the truth is wrong");
            }

            if (truth.Nullable && truth.Exempt is null && !sawNull[index])
            {
                Assert.Fail($"{column} can be NULL, and the seed shows none: {truth.Why}");
            }
        }
    }

    // How a failure names a column: the case, the position and the truth's name.
    private static string ColumnOf(RealisticCase found, int index) =>
        $"{found} column {index} {found.Columns[index].Name}";
}
