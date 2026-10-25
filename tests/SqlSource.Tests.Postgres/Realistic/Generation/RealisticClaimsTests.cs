using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The claims of each case by the classes of RealisticQueries.cs and RealisticMethods.cs: a case with models has
// exactly one at Models, in the first, and one at CodeGen, in the second, a case refused by the describer or the
// generator has none, and a claim names a folder of Cases.  So a case added to one file and not the other, a case
// added without its classes, or a case that comes to be refused or stops being so, fails naming itself.  It reads
// the source and needs no server; the reading of a claim is tested in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticClaimsTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void All_Claims_AreTheCasesWithModelsOnceAtEachOutput(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);
        var claims = RealisticClaims.AllOf(RealisticCases.Corpus).Where(claim => claim.Case == name).ToList();

        var atModels = claims.Count(claim => claim.Output == RealisticClaims.ModelsOutput);
        var atCodeGen = claims.Count(claim => claim.Output == RealisticClaims.CodeGenOutput);

        var expected = found.HasModels ? 1 : 0;
        atModels.ShouldBe(
            expected,
            RealisticClaims.ClaimedBy(RealisticCases.Corpus, found, RealisticClaims.ModelsOutput, atModels)
        );
        atCodeGen.ShouldBe(
            expected,
            RealisticClaims.ClaimedBy(RealisticCases.Corpus, found, RealisticClaims.CodeGenOutput, atCodeGen)
        );
        claims.Count.ShouldBe(2 * expected, $"{found} is claimed at an output other than Models and CodeGen");
    }

    [Fact]
    public void Queries_EveryClaim_IsAtModels() =>
        RealisticClaims
            .Parse(File.ReadAllText(RealisticCases.Corpus.Queries))
            .ShouldAllBe(claim => claim.Output == RealisticClaims.ModelsOutput);

    [Fact]
    public void Methods_EveryClaim_IsAtCodeGen() =>
        RealisticClaims
            .Parse(File.ReadAllText(RealisticCases.Corpus.Methods))
            .ShouldAllBe(claim => claim.Output == RealisticClaims.CodeGenOutput);

    [Fact]
    public void All_Claims_NameOnlyCasesThatExist() =>
        RealisticClaims
            .AllOf(RealisticCases.Corpus)
            .Select(static claim => claim.Case)
            .ShouldBeSubsetOf(RealisticCases.Corpus.Names);
}
