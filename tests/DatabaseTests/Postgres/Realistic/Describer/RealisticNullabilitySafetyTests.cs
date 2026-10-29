using System.Linq;
using System.Threading.Tasks;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic.Describer;

// That the describer's nullability is safe over each realistic case: it says not null of no column that can be NULL,
// checked against the case's truth and against the seed by RealisticNullabilityOracle.  Neither reads what the
// committed sidecar holds of the case, so a committed wrong not null still fails here.  A case is described once in
// a run, and each theory reads the one description.
[Collection(UsesPostgres.Name)]
public sealed class RealisticNullabilitySafetyTests(PostgresDatabases postgres)
{
    private readonly RealisticNullabilityChecks _checks = new(
        RealisticCases.Describe,
        postgres.Major,
        postgres.RealisticConnectionString,
        (request, parameters, sample) =>
            RealisticNullabilityOracle.RunAsync(
                postgres.RealisticConnectionString,
                RealisticSamples.Substitute(
                    request.Sql,
                    sample,
                    parameters
                        .Where(static parameter => parameter.Type is not null)
                        .ToDictionary(static parameter => parameter.Name, static parameter => parameter.Type!.Name)
                )
            )
    );

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task DescribeAsync_Case_IsNotNullOnlyWhereTheTruthIs(string name) =>
        _checks.ShouldBeNotNullOnlyWhereTheTruthIsAsync(name);

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task Execute_Case_ReturnsNoNullWhereEitherSaysNotNull(string name) =>
        _checks.ShouldReturnNoNullWhereEitherSaysNotNullAsync(name);
}
