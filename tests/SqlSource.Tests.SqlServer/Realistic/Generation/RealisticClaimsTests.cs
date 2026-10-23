using System.Linq;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// The claim of each case by a class of RealisticQueries.cs: a case with models has exactly one, a case refused by the
// describer or the generator has none, and a claim names a folder of Cases.  So a case added without its class, or
// a case that comes to be refused or stops being so, fails naming itself.  It reads the source and needs no server;
// the reading of a claim is tested in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticClaimsTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void All_Claims_AreTheCasesWithModelsEachOnce(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);

        var claims = RealisticClaims.AllOf(RealisticCases.Corpus).Count(claim => claim.Case == name);

        claims.ShouldBe(found.HasModels ? 1 : 0, RealisticClaims.ClaimedBy(RealisticCases.Corpus, found, claims));
    }

    [Fact]
    public void All_Claims_NameOnlyCasesThatExist() =>
        RealisticClaims
            .AllOf(RealisticCases.Corpus)
            .Select(static claim => claim.Case)
            .ShouldBeSubsetOf(RealisticCases.Corpus.Names);
}
