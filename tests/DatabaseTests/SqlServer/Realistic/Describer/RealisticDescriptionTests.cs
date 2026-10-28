using System.Threading.Tasks;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Describer;

// What the describer gives for each realistic case against a real server, whole: the refusal its case says or none,
// and for a case with models the entry its committed sidecar holds, every field of it.  Whether its nullability is
// safe is RealisticNullabilitySafetyTests'.
// The body is the check of tests/SqlSource.Tool.Testing/Realistic/.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticDescriptionTests(SqlServerDatabases sqlServer)
{
    private readonly RealisticDescriptionChecks _checks = new(
        RealisticCases.Describe,
        sqlServer.Major,
        sqlServer.RealisticConnectionString
    );

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays(string name) =>
        _checks.ShouldBeRefusedOnlyAsItsCaseSaysAsync(name);

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task DescribeAsync_Case_IsItsCommittedEntry(string name) => _checks.ShouldBeItsCommittedEntryAsync(name);
}
