using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Testing;

namespace SqlSource.DatabaseTests.Postgres;

// Where the files of the repository that this project reads are, found from this assembly.  The describer's
// fixtures are DescriberFixtures of the harness.
internal static class RepositoryPaths
{
    // The end-to-end project's folder EndToEnd/: the folders of the databases "app" and "billing", and Queries/.
    public static string EndToEnd => EndToEndEngine.Postgres.Folder;

    public static string EndToEndProject { get; } =
        RepositoryRoot.Path("tests", "DatabaseTests", "Postgres", "Postgres.csproj");
}
