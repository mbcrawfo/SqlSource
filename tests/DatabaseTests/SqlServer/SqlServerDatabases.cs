using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SqlSource.DatabaseTests.SqlServer.Realistic;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.SqlServer;

// The SQL Server of the collection UsesSqlServer: the database "fixtures" with the fixture schema, the database
// "realistic" with its schema and seed, and the end-to-end databases "app" and "billing", each made once.
public sealed class SqlServerDatabases() : SqlServerContainer(SqlClientFactory.Instance, EngineImage.SqlServer.Image)
{
    public const string Database = "fixtures";

    // The second database, for the corpus of realistic queries: its own schema and seed, so that the fixture schema
    // and the rows that tests count in it stay as they are.
    public const string RealisticDatabase = "realistic";

    // The logical databases of the end-to-end tests, built from EndToEnd/app/ and EndToEnd/billing/.
    public const string AppDatabase = "app";

    public const string BillingDatabase = "billing";

    // The databases of the end-to-end tests, in the order they are built.
    public static IReadOnlyList<string> EndToEndDatabases { get; } = [AppDatabase, BillingDatabase];

    // Of the database "realistic".
    public string RealisticConnectionString => ConnectionStringOf(RealisticDatabase);

    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [
            (Database, [DescriberFixtures.SqlServer.Schema]),
            // The database of the corpus, built after "fixtures" from its schema, then its seed.
            (RealisticDatabase, [RealisticCases.Corpus.Schema, RealisticCases.Corpus.Seed]),
            // The databases of the end-to-end tests: each is built from its schema and its seed, and "app" then gets
            // the table of the types of SQL Server 2025 on a major of 17 or later, in the order that
            // tools/describe-end-to-end.sh applies them.
            (
                AppDatabase,
                major >= 17
                    ?
                    [
                        .. EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, AppDatabase),
                        RepositoryPaths.NewTypesSchema,
                    ]
                    : EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, AppDatabase)
            ),
            (BillingDatabase, EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, BillingDatabase)),
        ];
}
