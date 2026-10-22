using System.IO;
using SqlSource.Testing;

namespace SqlSource.Tests.SqlServer;

// Where the files of the repository are, found from this assembly.  A describer's fixtures are read and recorded
// in the repository itself and not in an output folder: recording writes there.
internal static class RepositoryPaths
{
    public static string Root { get; } = RepositoryRoot.Folder;

    // The folder with one folder for each engine: its schema.sql and its cases.
    public static string DescriberFixtures { get; } =
        Path.Combine(Root, "tests", "SqlSource.Tool.Tests", "Fixtures", "Describers");

    public static string SqlServerSchema { get; } = Path.Combine(DescriberFixtures, "mssql", "schema.sql");

    // The database "realistic" of the corpus of realistic queries: its schema, its seed, and the folder of its cases.
    public static string Realistic { get; } = Path.Combine(Root, "tests", "SqlSource.Tests.SqlServer", "Realistic");

    public static string RealisticSchema { get; } = Path.Combine(Realistic, "schema.sql");

    public static string RealisticSeed { get; } = Path.Combine(Realistic, "seed.sql");

    public static string RealisticCases { get; } = Path.Combine(Realistic, "Cases");

    // The file of the classes that claim the realistic cases as Models queries.
    public static string RealisticQueries { get; } = Path.Combine(Realistic, "Generation", "RealisticQueries.cs");

    // The end-to-end project's folder EndToEnd/: the folders of the databases "app" and "billing", and Queries/, from
    // EndToEndEngine.
    public static string EndToEnd => EndToEndEngine.SqlServer.Folder;

    public static string EndToEndProject { get; } =
        Path.Combine(Root, "tests", "SqlSource.Tests.SqlServer", "SqlSource.Tests.SqlServer.csproj");

    // The table of the types that SQL Server 2025 brings, json and vector, applied to "app" on a major of 17 or later.
    public static string NewTypesSchema { get; } = Path.Combine(EndToEnd, "app", "new-types.sql");

    // The folder of the end-to-end queries and their sidecars.
    public static string EndToEndQueries { get; } = Path.Combine(EndToEnd, "Queries");

    // The file of the queries of those types, NewTypes.sql, which a server before major 17 cannot describe.
    public static string NewTypesQueries { get; } = Path.Combine(EndToEndQueries, "NewTypes.sql");
}
