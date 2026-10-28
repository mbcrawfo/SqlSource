using System.IO;
using SqlSource.Testing;

namespace SqlSource.DatabaseTests.Postgres;

// Where the files of the repository are, found from this assembly.  A describer's fixtures are read and recorded
// in the repository itself and not in an output folder: recording writes there.
internal static class RepositoryPaths
{
    // The folder of the PostgreSQL describer's fixtures: its schema.sql and its cases.  The other engine's folder
    // beside it is not this project's to read.
    public static string DescriberFixtures { get; } =
        RepositoryRoot.Path("tests", "SqlSource.Tool.Tests", "Fixtures", "Describers", "postgres");

    public static string PostgresSchema { get; } = Path.Combine(DescriberFixtures, "schema.sql");

    // The end-to-end project's folder EndToEnd/: the folders of the databases "app" and "billing", and Queries/.
    public static string EndToEnd => EndToEndEngine.Postgres.Folder;

    public static string EndToEndProject { get; } =
        RepositoryRoot.Path("tests", "DatabaseTests", "Postgres", "Postgres.csproj");
}
