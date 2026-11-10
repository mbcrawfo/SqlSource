using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// The claims of each case by the classes of RealisticMethods.cs: a case with models has exactly one, a case refused by
// the describer or the generator has none, and a claim sets Source alone and names a folder of Cases.  So a case added
// without its class, or a case that comes to be refused or stops being so, fails naming itself.  It reads the source
// and needs no server, and its bodies are the checks of tests/SqlSource.Tool.Testing/Realistic; the reading of a claim
// is tested in tests/SqlSource.Tool.Tests/Realistic.  That the output stays Methods, the project's, is pinned by
// RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim.
public sealed class RealisticClaimsTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void All_Claims_AreTheCasesWithModelsOnce(string name) =>
        RealisticClaimChecks.ShouldBeTheCasesWithModelsOnce(RealisticCases.Corpus, name);

    [Fact]
    public void Of_CaseWithModels_IsItsClass() =>
        RealisticClaimChecks.ShouldBeItsClass(
            RealisticCases.Corpus,
            "category-tree-recursive",
            "CategoryTreeRecursiveMethod"
        );

    [Fact]
    public void All_Claims_NameOnlyCasesThatExist() =>
        RealisticClaimChecks.ShouldNameOnlyCasesThatExist(RealisticCases.Corpus);
}
