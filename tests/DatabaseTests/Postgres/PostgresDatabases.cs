using System.Collections.Generic;
using Npgsql;
using SqlSource.DatabaseTests.Postgres.Realistic;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.Postgres;

// The PostgreSQL of the collection UsesPostgres: the database "fixtures" with the fixture schema, and the databases
// "realistic", "app" and "billing" with their schemas and seeds, each made once.
public sealed class PostgresDatabases() : PostgresContainer(NpgsqlFactory.Instance, EngineImage.Postgres.Image)
{
    public const string Database = "fixtures";

    // The second database, for the corpus of realistic queries: its own schema and seed, so that the fixture schema
    // and the rows that tests count in it stay as they are.
    public const string RealisticDatabase = "realistic";

    // The databases of the end-to-end tests, in the folders of tests/DatabaseTests/Postgres/EndToEnd/ that bear their
    // names: "app" holds the tables that the queries read and every type of the type map, "billing" a second database
    // for a query that names its own.
    public const string AppDatabase = "app";

    public const string BillingDatabase = "billing";

    // Why a test of old and new in RETURNING skips before PostgreSQL 18.
    public const string NoOldAndNewInReturning = "PostgreSQL before 18 has no old and new in RETURNING.";

    // The databases that the end-to-end tests are described and run against, each built from its folder of
    // tests/DatabaseTests/Postgres/EndToEnd/.
    public static IReadOnlyList<string> EndToEndDatabases { get; } = [AppDatabase, BillingDatabase];

    // Of the database "realistic".
    public string RealisticConnectionString => ConnectionStringOf(RealisticDatabase);

    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [
            (Database, [DescriberFixtures.Postgres.Schema]),
            // The seed relies on a hole of PostgreSQL: the NOT NULL of a domain is not checked again for a NULL that
            // is already of the domain, which gives invoice 4 its NULL amount.  A major that closes it fails the seed
            // here, and so the start of the container, and the truths of table-statement, cte-materialized,
            // distinct-on-latest-per-account and view-joined-left, which count on that NULL, are to be read again.
            (RealisticDatabase, [RealisticCases.Corpus.Schema, RealisticCases.Corpus.Seed]),
            // The databases of the end-to-end tests, in the order that tools/describe-end-to-end.sh applies them.
            (AppDatabase, EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, AppDatabase)),
            (BillingDatabase, EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, BillingDatabase)),
        ];
}
