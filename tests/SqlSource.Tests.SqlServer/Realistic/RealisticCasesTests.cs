using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic;

// The corpus of Realistic/Cases: every folder of Cases is a case, and a folder holds its query and its truth.  The
// reading of a folder, and what a case says of itself, are tested in tests/SqlSource.Tool.Tests/Realistic.
public sealed class RealisticCasesTests
{
    // macOS writes it into a folder that Finder shows, and .gitignore keeps it out of the repository.
    private const string FinderFile = ".DS_Store";

    // A case that the corpus holds, so that a reader that finds no folder fails.
    private const string KnownCase = "customers-search-paged";

    // The cases of the corpus, by the names of their folders alone, so that a case.json that is wrong fails the rows
    // of its own case.  Every theory over the corpus takes them.
    public static TheoryData<string> Cases => [with(RealisticCases.Names)];

    [Fact]
    public void All_Cases_HoldAKnownCase() => RealisticCases.All.ShouldContain(found => found.Name == KnownCase);

    // Each case is checked apart, so that a broken one fails its own row.  Phase 6.6 adds the sidecar of a case that
    // has models, and with it a third file here.
    [Theory]
    [MemberData(nameof(Cases))]
    public void Folder_Case_HoldsItsQueryAndItsTruthAlone(string name)
    {
        var found = RealisticCases.ReadNamed(name);

        Directory
            .GetFiles(found.Folder)
            .Select(Path.GetFileName)
            .Where(static file => file != FinderFile)
            .Order(StringComparer.Ordinal)
            .ShouldBe(
                new[] { RealisticCase.CaseFile, RealisticCase.QueryFile }.Order(StringComparer.Ordinal),
                $"{found} holds other files"
            );
    }
}
