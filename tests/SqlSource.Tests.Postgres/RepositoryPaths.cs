using System.IO;
using SqlSource.Testing;

namespace SqlSource.Tests.Postgres;

// Where the files of the repository are, found from this assembly.  A describer's fixtures are read and recorded
// in the repository itself and not in an output folder: recording writes there.
internal static class RepositoryPaths
{
    public static string Root { get; } = RepositoryRoot.Folder;

    // The folder with one folder for each engine: its schema.sql and its cases.
    public static string DescriberFixtures { get; } =
        Path.Combine(Root, "tests", "SqlSource.Tool.Tests", "Fixtures", "Describers");

    public static string PostgresSchema { get; } = Path.Combine(DescriberFixtures, "postgres", "schema.sql");

    // The end-to-end project's folder EndToEnd/: the folders of the databases "app" and "billing", and Queries/.
    public static string EndToEnd => EndToEndEngine.Postgres.Folder;

    public static string EndToEndProject { get; } =
        Path.Combine(Root, "tests", "SqlSource.Tests.Postgres", "SqlSource.Tests.Postgres.csproj");
}
