using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// The claims of each case by the classes of RealisticQueries.cs and RealisticMethods.cs: a case with models has
// exactly one at Models, in the first, and one at CodeGen, in the second, a case refused by the describer or the
// generator has none, and a claim names a folder of Cases.  So a case added to one file and not the other, a case
// added without its classes, or a case that comes to be refused or stops being so, fails naming itself.  It reads
// the source and needs no server, and its bodies are the checks of
// tests/SqlSource.Tool.Testing/Realistic; the reading of a claim is tested in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticClaimsTests
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public void All_Claims_AreTheCasesWithModelsOnceAtEachOutput(string name) =>
        RealisticClaimChecks.ShouldBeTheCasesWithModelsOnceAtEachOutput(RealisticCases.Corpus, name);

    [Fact]
    public void Of_CaseWithModels_IsItsClassAtEachOutput() =>
        RealisticClaimChecks.ShouldBeItsClassAtEachOutput(
            RealisticCases.Corpus,
            "category-tree-recursive",
            "CategoryTreeRecursiveQuery",
            "CategoryTreeRecursiveMethod"
        );

    [Fact]
    public void Queries_EveryClaim_IsAtModels() => RealisticClaimChecks.ShouldAllBeAtModels(RealisticCases.Corpus);

    [Fact]
    public void Methods_EveryClaim_IsAtCodeGen() => RealisticClaimChecks.ShouldAllBeAtCodeGen(RealisticCases.Corpus);

    [Fact]
    public void All_Claims_NameOnlyCasesThatExist() =>
        RealisticClaimChecks.ShouldNameOnlyCasesThatExist(RealisticCases.Corpus);
}
