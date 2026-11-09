using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Settings;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The whole "diagnose" command in process, as Cli.Run runs it, with the SQL Server describer against the container: one
// mssql project in the run's folder whose settings file names the database "app", and a connection for "app" on the
// command line to the container's database "fixtures".  The query joins two of its tables and holds literals, so that
// the bundle has names and literals to rename.  "replay" then runs the bundle with no database at all.
[Collection(UsesSqlServer.Name)]
public sealed class DiagnoseCommandTests(SqlServerDatabases sqlServer) : IDisposable
{
    private const string Sql =
        "-- name: "
        + DiagnoseCommands.Query
        + "\n"
        + "SELECT u.name, o.total, 'zebra-crossing' AS tag\n"
        + "FROM dbo.users AS u JOIN dbo.orders AS o ON o.user_id = u.id\n"
        + "WHERE u.name = 'Quokka Marsupial';\n";

    private readonly DiagnoseCommands _commands = new(Engine(sqlServer));

    private static DiagnoseEngine Engine(SqlServerDatabases sqlServer) =>
        new(
            "mssql",
            static () => new SqlServerDescriber(),
            sqlServer.ConnectionString,
            Sql,
            static async (commands, renamed) =>
            {
                // The names that the engine's own rows hold are known from the raw bundle of the same query.
                var made = await commands.DiagnoseAsync("--out", "raw-for-leaks.zip", "--no-obfuscate");
                made.ExitCode.ShouldBe(0, made.Error);
                var raw = commands.ReadBundle("raw-for-leaks.zip");
                var token = TestContext.Current.CancellationToken;
                var schema = await File.ReadAllTextAsync(DescriberFixtures.SqlServer.Schema, token);
                SqlServerBundleLeaks
                    .Find(renamed, EngineRenamings.For(DatabaseEngine.SqlServer), raw, schema, Sql)
                    .ShouldBeEmpty();
            }
        );

    public void Dispose() => _commands.Dispose();

    [Fact]
    public async Task Diagnose_AQueryOfApp_LeaksNoNameOfTheSchemaAndReproduces() =>
        await _commands.ShouldLeakNothingAndReproduceAsync();

    [Fact]
    public async Task Diagnose_NoObfuscateThenScrub_Reproduces() =>
        await _commands.ShouldReproduceAfterNoObfuscateThenScrubAsync();
}
