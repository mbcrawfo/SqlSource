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

        var expected = found.HasModels ? 1 : 0;
        claims.Count.ShouldBe(expected, RealisticClaims.ClaimedBy(corpus, found, claims.Count));
    }

    public static void ShouldBeItsClass(RealisticCorpus corpus, string name, string methodType) =>
        RealisticClaims.Of(corpus, corpus.ReadNamed(name)).TypeName.ShouldBe(methodType);

    public static void ShouldNameOnlyCasesThatExist(RealisticCorpus corpus) =>
        RealisticClaims.AllOf(corpus).Select(static claim => claim.Case).ShouldBeSubsetOf(corpus.Names);
}
