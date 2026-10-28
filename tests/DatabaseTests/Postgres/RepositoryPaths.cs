using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Testing;

namespace SqlSource.DatabaseTests.Postgres;

// Where the files of the repository are, found from this assembly.  A describer's fixtures are read and recorded
// in the repository itself and not in an output folder: recording writes there.
internal static class RepositoryPaths
{
    // The end-to-end project's folder EndToEnd/: the folders of the databases "app" and "billing", and Queries/.
    public static string EndToEnd => EndToEndEngine.Postgres.Folder;

    public static string EndToEndProject { get; } =
        RepositoryRoot.Path("tests", "DatabaseTests", "Postgres", "Postgres.csproj");
}
