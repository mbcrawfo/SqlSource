using System.Threading.Tasks;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each case with models is run over the seed through Dapper into its generated output model, once for each sample, and
// every row is read.  The body is the check of tests/SqlSource.Tool.Testing/Realistic/, given this project's seed.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticDapperTests(SqlServerDatabases sqlServer)
{
    private readonly RealisticDapperChecks _checks = new(
        RealisticCases.Corpus,
        RealisticCases.Models,
        sqlServer.Major,
        () => new RealisticSeed(sqlServer)
    );

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task QueryAsync_Case_ReadsEveryRowIntoItsModel(string name) =>
        _checks.ShouldReadEveryRowIntoItsModelAsync(name);
}
