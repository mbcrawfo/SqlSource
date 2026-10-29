using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Npgsql;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic.Generation;

// Each case whose models the generator refuses is refused as its case.json says.  The body is the check of
// tests/SqlSource.Tool.Testing/Realistic/.
[Collection(UsesPostgres.Name)]
public sealed class RealisticModelRefusalTests(PostgresDatabases postgres)
{
    private readonly RealisticRefusalChecks _checks = new(
        RealisticCases.Describe,
        postgres.Major,
        postgres.RealisticConnectionString,
        [MetadataReference.CreateFromFile(typeof(NpgsqlConnection).Assembly.Location)]
    );

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public Task Run_CaseWithRefusedModels_GivesItsOneModelError(string name) =>
        _checks.ShouldGiveItsOneModelErrorAsync(name);
}
