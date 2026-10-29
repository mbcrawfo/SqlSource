using System.Threading.Tasks;
using SqlSource.Testing.Generator;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each case whose models the generator refuses is refused as its case.json says.  No case of the corpus has one
// today, so every case skips, until one comes to need it.  The body is the check of
// tests/SqlSource.Tool.Testing/Realistic/.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticModelRefusalTests(SqlServerDatabases sqlServer)
{
    private readonly RealisticRefusalChecks _checks = new(
        RealisticCases.Describe,
        sqlServer.Major,
        sqlServer.RealisticConnectionString,
        [SqlClientReferences.SqlClient7]
    );

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task Run_CaseWithRefusedModels_GivesItsOneModelError(string name) =>
        _checks.ShouldGiveItsOneModelErrorAsync(name);
}
