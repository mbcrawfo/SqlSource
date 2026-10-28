using System.Collections.Generic;
using System.IO;
using Microsoft.Data.SqlClient;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// A SQL Server of EngineImage.SqlServer.Default, the end-to-end project's default image, which ignores the variable
// that names another, with the end-to-end database "app": its schema, its seed and the table of the types of SQL Server
// 2025.
public sealed class AppContainer() : SqlServerContainer(SqlClientFactory.Instance, EngineImage.SqlServer.Default)
{
    private const string Database = "app";

    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [
            (
                Database,
                [
                    .. EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, Database),
                    // The default image is SQL Server 2025, which has json and vector.
                    Path.Combine(EndToEndEngine.SqlServer.Folder, Database, "new-types.sql"),
                ]
            ),
        ];
}
