using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// One PostgreSQL with PostGIS, on the PostGIS image built on EngineImage.Postgres.Default, the end-to-end project's
// default image, which ignores the variable that names another: the end-to-end databases "app", "billing" and "gis",
// each with its schema and seed, and beside each a template of it, "<db>_seed", which no connection ever opens, so that
// a scenario that writes gets a copy of the seed of its own.
public sealed class PostgresDatabases() : PostgisContainer(NpgsqlFactory.Instance, EngineImage.Postgres.Default)
{
    public const string AppDatabase = "app";

    public const string BillingDatabase = "billing";

    public const string GisDatabase = "gis";

    // The database of the server itself, on which databases are made and dropped.
    public const string ServerDatabase = "postgres";

    private static readonly string[] Seeded = [AppDatabase, BillingDatabase, GisDatabase];

    // The template that a copy of the database is made from.
    public static string TemplateOf(string database) => database + "_seed";

    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [.. Seeded.Select(static name => (name, EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, name)))];

    // CREATE DATABASE ... TEMPLATE refuses a template that a connection has open, so the pools that built the
    // databases let go of theirs first.
    protected override async Task BuiltAsync(DbDataSource server, CancellationToken token)
    {
        NpgsqlConnection.ClearAllPools();
        foreach (var database in Seeded)
        {
            await TestDatabases.ExecuteAsync(
                server,
                $"CREATE DATABASE {TemplateOf(database)} TEMPLATE {database}",
                token
            );
        }
    }
}
