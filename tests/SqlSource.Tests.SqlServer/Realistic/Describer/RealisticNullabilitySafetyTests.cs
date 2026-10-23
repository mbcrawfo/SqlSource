using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Describer;

// That the describer's nullability is safe over each realistic case: it says not null of no column that can be NULL,
// checked against the case's truth and against the seed by RealisticNullabilityOracle.  A case is described once in a
// run, and each theory reads the one description.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticNullabilitySafetyTests(SqlServerContainer sqlServer)
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task DescribeAsync_Case_IsNotNullOnlyWhereTheTruthIs(string name)
    {
        var found = RealisticCases.Describe.Start(name, sqlServer.Major);
        var (columns, _) = await RealisticCases.Describe.DescribedAsync(found, sqlServer.RealisticConnectionString);

        for (var index = 0; index < columns.Count; index++)
        {
            var truth = found.Columns[index];
            if (truth.Nullable && columns[index].Nullable == false)
            {
                Assert.Fail($"{ColumnOf(found, index)} is not null, and can be NULL: {truth.Why}");
            }
        }
    }

    // The query runs once for each sample, with the sample's values bound to its parameters at the types the describer
    // gave them.
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task Execute_Case_ReturnsNoNullWhereEitherSaysNotNull(string name)
    {
        var found = RealisticCases.Describe.Start(name, sqlServer.Major);
        var (columns, parameters) = await RealisticCases.Describe.DescribedAsync(
            found,
            sqlServer.RealisticConnectionString
        );
        var request = RealisticDescribe.RequestOf(found);
        RealisticSampleChecks.CheckKeys(found, parameters.Select(static parameter => parameter.Name));
        RealisticSampleChecks.CheckNulls(found, request.Parameters);

        var sawNull = new bool[columns.Count];
        foreach (var sample in found.Samples)
        {
            var run = await RealisticNullabilityOracle.RunAsync(
                sqlServer.RealisticConnectionString,
                RealisticSamples.Batch(request.Sql, sample, parameters)
            );

            run.Names.ShouldBe(columns.Select(static column => column.Name), $"{found} names its columns otherwise");
            for (var index = 0; index < sawNull.Length; index++)
            {
                sawNull[index] |= run.SawNull[index];
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
