using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// A SQL Server of EngineImage.SqlServer.Default, the end-to-end project's default image, which ignores the variable
// that names another, with the end-to-end database "app": its schema and its seed.  The new types are not loaded, since
// no query linked here reads them.
public sealed class SqlServerApp() : SqlServerContainer(SqlClientFactory.Instance, EngineImage.SqlServer.Default)
{
    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [("app", EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, "app"))];
}
