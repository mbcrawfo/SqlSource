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

    // The database "realistic" of the corpus of realistic queries: its schema, its seed, and the folder of its cases.
    public static string Realistic { get; } = Path.Combine(Root, "tests", "SqlSource.Tests.Postgres", "Realistic");

    public static string RealisticSchema { get; } = Path.Combine(Realistic, "schema.sql");

    public static string RealisticSeed { get; } = Path.Combine(Realistic, "seed.sql");

    public static string RealisticCases { get; } = Path.Combine(Realistic, "Cases");
}
