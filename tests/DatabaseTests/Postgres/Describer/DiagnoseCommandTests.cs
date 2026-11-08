using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// The whole "diagnose" command in process, as Cli.Run runs it, with the PostgreSQL describer against the container: one
// postgres project in the run's folder whose settings file names the database "app", and a connection for "app" on the
// command line to the container's database "fixtures".  The query joins two of its tables and holds literals, so that
// the bundle has names and literals to rename.  "replay" then runs the bundle with no database at all.
[Collection(UsesPostgres.Name)]
public sealed class DiagnoseCommandTests(PostgresDatabases postgres) : IDisposable
{
    private const string Sql =
        "-- name: "
        + DiagnoseCommands.Query
        + "\n"
        + "SELECT u.name, o.total, 'zebra-crossing' AS tag\n"
        + "FROM users u JOIN orders o ON o.user_id = u.id\n"
        + "WHERE u.name = 'Quokka Marsupial';\n";

    private readonly DiagnoseCommands _commands = new(Engine(postgres));

    private static DiagnoseEngine Engine(PostgresDatabases postgres) =>
        new(
            "postgres",
            static () => new PostgresDescriber(),
            postgres.ConnectionString,
            Sql,
            static async (commands, renamed) =>
            {
                var schema = await File.ReadAllTextAsync(
                    DescriberFixtures.Postgres.Schema,
                    TestContext.Current.CancellationToken
                );
                PostgresBundleLeaks.Of(renamed, [schema, Sql], PostgresWords.IsEngineWord).ShouldBeEmpty();
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
