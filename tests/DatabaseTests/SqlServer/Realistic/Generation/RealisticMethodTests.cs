using System.Threading.Tasks;
using SqlSource.DatabaseTests.SqlServer.Realistic.Describer;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each case with models is run over the seed through its generated method, once for each sample, and every row is
// read into its output model; a NULL read into a property that is not nullable fails naming each column that held one
// over the seed, by a rerun of the sample through RealisticNullabilityOracle.  The body is the check of
// tests/SqlSource.Tool.Testing/Realistic/, given this project's seed and its oracle's run of a sample.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticMethodTests(SqlServerDatabases sqlServer)
{
    private readonly RealisticMethodChecks _checks = new(
        RealisticCases.Corpus,
        RealisticCases.Models,
        RealisticCases.Describe,
        sqlServer.Major,
        sqlServer.RealisticConnectionString,
        () => new RealisticSeed(sqlServer),
        RealisticNullabilityOracle.SampleRunOn(sqlServer.RealisticConnectionString)
    );

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task Method_Case_ReadsEveryRowIntoItsModel(string name) => _checks.ShouldReadEveryRowIntoItsModelAsync(name);
}
