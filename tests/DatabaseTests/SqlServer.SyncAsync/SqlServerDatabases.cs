using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// One SQL Server, on EngineImage.SqlServer.Default, the end-to-end project's default image, which ignores the variable
// that names another: the end-to-end databases "app", with its schema, its seed and the table of the types of SQL
// Server 2025, and "billing", with its schema and seed, and a backup of each taken once they are built, which no test
// changes, so that a scenario that writes gets a copy of the seed of its own by restoring it.  "app" gets the table
// that the inserts of Queries/Batches.sql write into, by their token, before its backup.
public sealed class SqlServerDatabases() : SqlServerContainer(SqlClientFactory.Instance, EngineImage.SqlServer.Default)
{
    public const string AppDatabase = "app";

    public const string BillingDatabase = "billing";

    // The database of the server itself, on which databases are restored and dropped.
    public const string ServerDatabase = "master";

    // The folder of the server's data files, inside the container, where the backups and the restored copies are.
    public const string DataFolder = "/var/opt/mssql/data/";

    // The table of "app" that the inserts of Queries/Batches.sql are given by their token notes: the columns of
    // dbo.notes they write, as the end-to-end tests' temporary table has them, without the identity of dbo.notes, so
    // that an insert writes the id it is given, and with a primary key named here, since the name the server would
    // choose differs between two copies and is in the message of a duplicate key.
    public const string BatchNotes = "dbo.batch_notes";

    private static readonly string[] Seeded = [AppDatabase, BillingDatabase];

    private readonly Dictionary<string, IReadOnlyList<string>> _logicalFiles = [];

    // The backup that a copy of the database is restored from.
    public static string BackupOf(string database) => DataFolder + database + "_seed.bak";

    // The logical names of the database's files, which a restore under another name moves each to a file of its own.
    public IReadOnlyList<string> LogicalFilesOf(string database) => _logicalFiles[database];

    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [
            (
                AppDatabase,
                [
                    .. EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, AppDatabase),
                    // The default image is SQL Server 2025, which has json and vector.
                    Path.Combine(EndToEndEngine.SqlServer.Folder, AppDatabase, "new-types.sql"),
                ]
            ),
            (BillingDatabase, EndToEndFiles.SchemaAndSeed(EndToEndEngine.SqlServer, BillingDatabase)),
        ];

    protected override async Task BuiltAsync(DbDataSource server, CancellationToken token)
    {
        await TestDatabases.ExecuteAsync(
            server,
            $"CREATE TABLE {AppDatabase}.{BatchNotes} (id int CONSTRAINT batch_notes_key PRIMARY KEY, "
                + "account_id int NOT NULL, kind varchar(10) NOT NULL, body nvarchar(max) NOT NULL, "
                + "written_at datetime2(3) NOT NULL)",
            token
        );
        foreach (var database in Seeded)
        {
            var backup = BackupOf(database);
            await TestDatabases.ExecuteAsync(
                server,
                $"BACKUP DATABASE {database} TO DISK = N'{backup}' WITH INIT",
                token
            );
            await using var list = server.CreateCommand($"RESTORE FILELISTONLY FROM DISK = N'{backup}'");
            await using var reader = await list.ExecuteReaderAsync(token);
            var names = new List<string>();
            var logicalName = reader.GetOrdinal("LogicalName");
            while (await reader.ReadAsync(token))
            {
                names.Add(reader.GetString(logicalName));
            }

            _logicalFiles.Add(database, names);
        }
    }
}
