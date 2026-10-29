using System.Threading.Tasks;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Describer;

// That the describer's nullability is safe over each realistic case: it says not null of no column that can be NULL,
// checked against the case's truth and against the seed by RealisticNullabilityOracle.  A case is described once in a
// run, and each theory reads the one description.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticNullabilitySafetyTests(SqlServerDatabases sqlServer)
{
    private readonly RealisticNullabilityChecks _checks = new(
        RealisticCases.Describe,
        sqlServer.Major,
        sqlServer.RealisticConnectionString,
        (request, parameters, sample) =>
            RealisticNullabilityOracle.RunAsync(
                sqlServer.RealisticConnectionString,
                RealisticSamples.Batch(request.Sql, sample, parameters)
            )
    );

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task DescribeAsync_Case_IsNotNullOnlyWhereTheTruthIs(string name) =>
        _checks.ShouldBeNotNullOnlyWhereTheTruthIsAsync(name);

    // The query runs once for each sample, with the sample's values bound to its parameters at the types the describer
    // gave them.
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task Execute_Case_ReturnsNoNullWhereEitherSaysNotNull(string name) =>
        _checks.ShouldReturnNoNullWhereEitherSaysNotNullAsync(name);
}
