using System.IO;
using System.Linq;
using Shouldly;

namespace SqlSource.Tool.Testing.Realistic;

// The checks of a corpus's claims that both engines' RealisticClaimsTests run.  The reading of a claim is tested in
// tests/SqlSource.Tool.Tests/Realistic.
internal static class RealisticClaimChecks
{
    public static void ShouldBeTheCasesWithModelsOnceAtEachOutput(RealisticCorpus corpus, string name)
    {
        var found = corpus.ReadNamed(name);
        var claims = RealisticClaims.AllOf(corpus).Where(claim => claim.Case == name).ToList();

        var atModels = claims.Count(claim => claim.Output == RealisticClaims.ModelsOutput);
        var atCodeGen = claims.Count(claim => claim.Output == RealisticClaims.CodeGenOutput);

        var expected = found.HasModels ? 1 : 0;
        atModels.ShouldBe(expected, RealisticClaims.ClaimedBy(corpus, found, RealisticClaims.ModelsOutput, atModels));
        atCodeGen.ShouldBe(
            expected,
            RealisticClaims.ClaimedBy(corpus, found, RealisticClaims.CodeGenOutput, atCodeGen)
        );
        claims.Count.ShouldBe(2 * expected, $"{found} is claimed at an output other than Models and CodeGen");
    }

    public static void ShouldBeItsClassAtEachOutput(
        RealisticCorpus corpus,
        string name,
        string queryType,
        string methodType
    )
    {
        var found = corpus.ReadNamed(name);

        RealisticClaims.Of(corpus, found, RealisticClaims.ModelsOutput).TypeName.ShouldBe(queryType);
        RealisticClaims.Of(corpus, found, RealisticClaims.CodeGenOutput).TypeName.ShouldBe(methodType);
    }

    public static void ShouldAllBeAtModels(RealisticCorpus corpus) =>
        RealisticClaims
            .Parse(File.ReadAllText(corpus.Queries))
            .ShouldAllBe(claim => claim.Output == RealisticClaims.ModelsOutput);

    public static void ShouldAllBeAtCodeGen(RealisticCorpus corpus) =>
        RealisticClaims
            .Parse(File.ReadAllText(corpus.Methods))
            .ShouldAllBe(claim => claim.Output == RealisticClaims.CodeGenOutput);

    public static void ShouldNameOnlyCasesThatExist(RealisticCorpus corpus) =>
        RealisticClaims.AllOf(corpus).Select(static claim => claim.Case).ShouldBeSubsetOf(corpus.Names);
}
