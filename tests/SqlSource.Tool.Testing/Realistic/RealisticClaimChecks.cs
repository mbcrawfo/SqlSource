using System.IO;
using System.Linq;
using Shouldly;

namespace SqlSource.Tool.Testing.Realistic;

// The checks of a corpus's claims that both engines' RealisticClaimsTests run.  The reading of a claim is tested in
// tests/SqlSource.Tool.Tests/Realistic.
internal static class RealisticClaimChecks
{
    public static void ShouldBeTheCasesWithModelsOnce(RealisticCorpus corpus, string name)
    {
        var found = corpus.ReadNamed(name);
        var claims = RealisticClaims.AllOf(corpus).Where(claim => claim.Case == name).ToList();

        var atCodeGen = claims.Count(claim => claim.Output == RealisticClaims.CodeGenOutput);

        var expected = found.HasModels ? 1 : 0;
        atCodeGen.ShouldBe(
            expected,
            RealisticClaims.ClaimedBy(corpus, found, RealisticClaims.CodeGenOutput, atCodeGen)
        );
        claims.Count.ShouldBe(expected, $"{found} is claimed at an output other than CodeGen");
    }

    public static void ShouldBeItsClass(RealisticCorpus corpus, string name, string methodType) =>
        RealisticClaims.Of(corpus, corpus.ReadNamed(name), RealisticClaims.CodeGenOutput).TypeName.ShouldBe(methodType);

    public static void ShouldAllBeAtCodeGen(RealisticCorpus corpus) =>
        RealisticClaims
            .Parse(File.ReadAllText(corpus.Methods))
            .ShouldAllBe(claim => claim.Output == RealisticClaims.CodeGenOutput);

    public static void ShouldNameOnlyCasesThatExist(RealisticCorpus corpus) =>
        RealisticClaims.AllOf(corpus).Select(static claim => claim.Case).ShouldBeSubsetOf(corpus.Names);
}
