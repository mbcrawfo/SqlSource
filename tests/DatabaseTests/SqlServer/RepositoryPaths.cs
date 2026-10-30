using System.IO;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Testing;

namespace SqlSource.DatabaseTests.SqlServer;

// Where the files of the repository that this project reads are, found from this assembly.  The describer's
// fixtures are DescriberFixtures of the harness.
internal static class RepositoryPaths
{
    // The end-to-end project's folder EndToEnd/: the folders of the databases "app" and "billing", and Queries/, from
    // EndToEndEngine.
    public static string EndToEnd => EndToEndEngine.SqlServer.Folder;

    public static string EndToEndProject { get; } =
        RepositoryRoot.Path("tests", "DatabaseTests", "SqlServer", "SqlServer.csproj");

    // The table of the types that SQL Server 2025 brings, json and vector, applied to "app" on a major of 17 or later.
    public static string NewTypesSchema { get; } = Path.Combine(EndToEnd, "app", "new-types.sql");

    // The folder of the end-to-end queries and their sidecars.
    public static string EndToEndQueries { get; } = Path.Combine(EndToEnd, "Queries");

    // The folder of the end-to-end queries whose folder turns NodaTime on, and their sidecars.
    public static string EndToEndNodaTimeQueries { get; } = Path.Combine(EndToEnd, "NodaTime", "Queries");

    // The file of the queries of those types, NewTypes.sql, which a server before major 17 cannot describe.
    public static string NewTypesQueries { get; } = Path.Combine(EndToEndQueries, "NewTypes.sql");
}
