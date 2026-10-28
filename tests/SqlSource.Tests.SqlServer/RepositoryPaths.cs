using System.IO;
using SqlSource.Testing;

namespace SqlSource.Tests.SqlServer;

// Where the files of the repository are, found from this assembly.  A describer's fixtures are read and recorded
// in the repository itself and not in an output folder: recording writes there.
internal static class RepositoryPaths
{
    // The folder of the SQL Server describer's fixtures: its schema.sql and its cases.  The other engine's folder
    // beside it is not this project's to read.
    public static string DescriberFixtures { get; } =
        RepositoryRoot.Path("tests", "SqlSource.Tool.Tests", "Fixtures", "Describers", "mssql");

    public static string SqlServerSchema { get; } = Path.Combine(DescriberFixtures, "schema.sql");

    // The end-to-end project's folder EndToEnd/: the folders of the databases "app" and "billing", and Queries/, from
    // EndToEndEngine.
    public static string EndToEnd => EndToEndEngine.SqlServer.Folder;

    public static string EndToEndProject { get; } =
        RepositoryRoot.Path("tests", "SqlSource.Tests.SqlServer", "SqlSource.Tests.SqlServer.csproj");

    // The table of the types that SQL Server 2025 brings, json and vector, applied to "app" on a major of 17 or later.
    public static string NewTypesSchema { get; } = Path.Combine(EndToEnd, "app", "new-types.sql");

    // The folder of the end-to-end queries and their sidecars.
    public static string EndToEndQueries { get; } = Path.Combine(EndToEnd, "Queries");

    // The file of the queries of those types, NewTypes.sql, which a server before major 17 cannot describe.
    public static string NewTypesQueries { get; } = Path.Combine(EndToEndQueries, "NewTypes.sql");
}
