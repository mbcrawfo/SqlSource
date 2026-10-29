using System;
using System.IO;
using System.Linq;
using Shouldly;

namespace SqlSource.Tool.Testing.Realistic;

// The checks of a corpus's Cases folder that both engines' RealisticCasesTests run: every folder is a case, and holds
// its files alone.
internal static class RealisticFolderChecks
{
    // macOS writes it into a folder that Finder shows, and .gitignore keeps it out of the repository.
    private const string FinderFile = ".DS_Store";

    // The corpus holds a case that is known to exist, so that a reader that finds no folder fails.
    public static void ShouldHoldAKnownCase(RealisticCorpus corpus, string knownCase) =>
        corpus.All.ShouldContain(found => found.Name == knownCase);

    // A case is checked apart, so that a broken one fails its own row.
    public static void ShouldHoldItsFilesAlone(RealisticCorpus corpus, string name)
    {
        var found = corpus.ReadNamed(name);

        Directory
            .GetFiles(found.Folder)
            .Select(Path.GetFileName)
            .Where(static file => file != FinderFile)
            .Order(StringComparer.Ordinal)
            .ShouldBe(found.Files.Order(StringComparer.Ordinal), $"{found} holds other files");
    }
}
