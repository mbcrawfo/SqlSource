using System;
using System.IO;

namespace SqlSource.Testing;

// The folder of the repository, found from the assembly that runs: the one that holds SqlSource.slnx.  For a test
// that reads the repository itself and not its output folder.
internal static class RepositoryRoot
{
    private const string Solution = "SqlSource.slnx";

    public static string Folder { get; } = Find();

    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, Solution)))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No folder above '{AppContext.BaseDirectory}' holds {Solution}.");
    }
}
