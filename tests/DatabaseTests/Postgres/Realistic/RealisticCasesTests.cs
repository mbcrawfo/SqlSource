using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic;

// The corpus of Realistic/Cases: every folder of Cases is a case, and a folder holds its query, its truth and its
// sidecar when it has models.  The bodies are the checks of tests/SqlSource.Tool.Testing/Realistic.  The reading of a
// folder, and what a case says of itself, are tested in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticCasesTests
{
    private const string KnownCase = "aggregate-cte-left-joined";

    // The cases of the corpus, by the names of their folders alone, so that a case.json that is wrong fails the rows
    // of its own case.  Every theory over the corpus takes them.
    public static TheoryData<string> Cases => [with(RealisticCases.Corpus.Names)];

    [Fact]
    public void All_Cases_HoldAKnownCase() =>
        RealisticFolderChecks.ShouldHoldAKnownCase(RealisticCases.Corpus, KnownCase);

    // Each case is checked apart, so that a broken one fails its own row.
    [Theory]
    [MemberData(nameof(Cases))]
    public void Folder_Case_HoldsItsFilesAlone(string name) =>
        RealisticFolderChecks.ShouldHoldItsFilesAlone(RealisticCases.Corpus, name);
}
