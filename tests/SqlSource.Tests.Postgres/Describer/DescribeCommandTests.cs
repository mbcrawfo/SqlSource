using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The whole "describe" command in process, as Cli.Run runs it, with the PostgreSQL describer against the container:
// one postgres project in the run's folder whose .sql files name the database "app", and a connection for "app" on
// the command line to the container's database "fixtures", the one the describer's fixtures were recorded on.  The
// tool's own tests run the same command with a describer of their own.
[Collection(UsesPostgres.Name)]
public sealed class DescribeCommandTests : IDisposable
{
    private const string Database = "app";

    // The member of an event of the log that names it.
    private const string EventMember = "event";

    // The event of the driver's own log, as the describer's log names it.
    private const string DriverEvent = "postgres.driver";

    // The role, the database and the application of the log test: words that nothing else of a run writes.
    private const string LogRole = "sqlsrc_wombat";
    private const string LogDatabase = "sqlsrc_numbat";
    private const string LogApplication = "sqlsrc_quokka";

    // Not the password that CreateLogRoleAndDatabaseAsync gives the role.
    private const string WrongPassword = "sqlsrc_not_the_password";

    // The cases of the describer's fixtures that a describer with no settings of the planner describes.  A case
    // with an explain-settings.txt turns join methods off, which a run of the command cannot.
    private static readonly IReadOnlyList<FixtureCase> Cases =
    [
        .. FixtureCases
            .Of(SidecarValues.Engine.Postgres, RepositoryPaths.DescriberFixtures)
            .Where(static found => !File.Exists(Path.Combine(found.Folder, FixtureCase.ExplainSettingsFile))),
    ];

    private readonly PostgresContainer _postgres;
    private readonly CliRun _run = new();
    private readonly TestProject _project;

    public DescribeCommandTests(PostgresContainer postgres)
    {
        _postgres = postgres;
        _run.Describers.Add(new PostgresDescriber());
        _project = Plans.Postgres(_run.Folder, directory: "").AnsweredBy(_run.Processes);
    }

    public void Dispose() => _run.Dispose();

    // A .sql file of the project, of the database "app".
    private string AddSql(string relativePath, string text) =>
        _project.AddSql(relativePath, text, ("SqlSourceDatabase", Database));

    // Each case's query as a file of its own, by the case's name.
    private Dictionary<FixtureCase, string> AddCases() =>
        Cases.ToDictionary(static found => found, found => AddSql(found.Name + ".sql", found.Query));

    private Task<CliResult> DescribeAsync(params string[] args) => DescribeWithAsync(_postgres.ConnectionString, args);

    private Task<CliResult> DescribeWithAsync(string connection, params string[] args) =>
        _run.RunAsync(["describe", "--connection", Database + "=" + connection, .. args]);

    // An entry as two runs that describe the same query give it: where its name stands in the text, the version of
    // the server and the name of the database are left out.
    private static SidecarEntry Comparable(SidecarEntry entry) =>
        entry with
        {
            NameSpan = default,
            ServerVersion = "*",
            Database = "*",
        };

    private static string NameOf(JsonElement logged) => logged.GetProperty(EventMember).GetString() ?? "";

    [Fact]
    public async Task Describe_FixtureQueries_WritesTheFixturesEntries()
    {
        var files = AddCases();

        var result = await DescribeAsync();

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe($"app (postgres): {Cases.Count} described, 0 skipped, 0 failed\n");
        foreach (var (fixture, path) in files)
        {
            var written = SidecarFiles.Read(path).Queries.ShouldHaveSingleItem(fixture.ToString());
            var expected = SidecarReader
                .Read(fixture.Expected)
                .Sidecar.ShouldNotBeNull(fixture.ToString())
                .Queries.ShouldHaveSingleItem(fixture.ToString());
            Comparable(written).ShouldBe(Comparable(expected), fixture.ToString());
        }
    }

    [Fact]
    public async Task Describe_Again_SkipsEveryQueryAndWritesNoFile()
    {
        var sidecars = AddCases().Values.Select(SidecarFormat.PathFor).ToArray();
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        foreach (var sidecar in sidecars)
        {
            File.SetLastWriteTimeUtc(sidecar, SidecarFiles.Old);
        }

        var result = await DescribeAsync();

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe($"app (postgres): 0 described, {Cases.Count} skipped, 0 failed\n");
        foreach (var sidecar in sidecars)
        {
            File.GetLastWriteTimeUtc(sidecar).ShouldBe(SidecarFiles.Old, sidecar);
        }
    }

    [Fact]
    public async Task Check_AfterDescribe_IsTheSame()
    {
        _ = AddCases();
        (await DescribeAsync()).ExitCode.ShouldBe(0);

        var result = await DescribeAsync("--check");

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe($"app (postgres): {Cases.Count} same, 0 differ, 0 failed\n");
    }

    // The table is the test's own: the fixture cases describe the schema as schema.sql has it.
    [Fact]
    public async Task Check_ColumnAddedAfterDescribe_DiffersUntilDescribedWithForce()
    {
        await _postgres.ExecuteAsync("CREATE TABLE command_widgets (id integer PRIMARY KEY, name text NOT NULL)");
        try
        {
            var widgets = AddSql("widgets.sql", "-- name: GetWidgets\nSELECT * FROM command_widgets;\n");
            var described = await DescribeAsync();
            described.ExitCode.ShouldBe(0, described.Error);
            await _postgres.ExecuteAsync("ALTER TABLE command_widgets ADD COLUMN note text");

            var check = await DescribeAsync("--check");

            check.ExitCode.ShouldBe(2);
            check.Error.ShouldContain("error SQLSRC219: ");
            check.Error.ShouldContain("'GetWidgets'");

            var forced = await DescribeAsync("--force");

            forced.ExitCode.ShouldBe(0, forced.Error);
            var columns = SidecarFiles.Read(widgets).Queries.ShouldHaveSingleItem().Columns.ShouldNotBeNull();
            columns.Select(static column => column.Name).ShouldBe(["id", "name", "note"]);
        }
        finally
        {
            await _postgres.ExecuteAsync("DROP TABLE IF EXISTS command_widgets");
        }
    }

    [Fact]
    public async Task Describe_RejectedQueryBesideAnAcceptedOne_IsSqlsrc225AndSqlsrc217()
    {
        var users = AddSql(
            "users.sql",
            "-- name: Broken\nSELECT no_such_column FROM users;\n\n-- name: Fine\nSELECT id FROM users;\n"
        );

        var result = await DescribeAsync();

        result.ExitCode.ShouldBe(1);
        // At the name of the query, on its first line.
        result.Error.ShouldContain($"{users}(1,10): error SQLSRC225: ");
        result.Error.ShouldContain($"{users} : error SQLSRC217: ");
        File.Exists(SidecarFormat.PathFor(users)).ShouldBeFalse();
    }

    // The host is the container's own, which a name of the test's would not reach; the role, the database and the
    // application are the test's.
    [Fact]
    public async Task Describe_WithLog_HoldsDriverEventsAndNoConnectionWord()
    {
        await CreateLogRoleAndDatabaseAsync();
        _ = AddSql("one.sql", "-- name: One\nSELECT 1 AS one;\n");
        var connection = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString)
        {
            Username = LogRole,
            Password = Secrets.Marker,
            Database = LogDatabase,
            ApplicationName = LogApplication,
        };
        var log = _run.Folder.PathOf("run.log");

        var result = await DescribeWithAsync(connection.ConnectionString, "--log", log);

        result.ExitCode.ShouldBe(0, result.Error);
        LogReader
            .Read(log)
            .ShouldContain(logged =>
                NameOf(logged) == DriverEvent && logged.GetProperty("category").GetString() == "Npgsql.Command"
            );
        var text = await File.ReadAllTextAsync(log, TestContext.Current.CancellationToken);
        foreach (var word in new[] { LogRole, LogDatabase, LogApplication, connection.Host!, Secrets.Marker })
        {
            text.ShouldNotContain(word);
        }

        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
    }

    // The port 1 of the container's host refuses at once.  The diagnostic holds the describer's own text, which says
    // what failed and names neither the host nor the user, and so does the rest of the log: the driver's own events
    // say nothing of it.
    [Fact]
    public async Task Describe_ConnectionRefusedWithLog_IsSqlsrc224AndNothingPrintedOrLoggedHoldsTheHostOrTheUser()
    {
        _ = AddSql("one.sql", "-- name: One\nSELECT 1 AS one;\n");
        var host = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString).Host.ShouldNotBeNull();
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = 1,
            Username = LogRole,
        };
        var log = _run.Folder.PathOf("run.log");

        var result = await DescribeWithAsync(connection.ConnectionString, "--log", log);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC224: ");
        result.Error.ShouldContain("    server: Npgsql could not reach the server: ConnectionRefused");
        ServerLinesOf(LogReader.Read(log), "SQLSRC224")
            .ShouldBe(["Npgsql could not reach the server: ConnectionRefused"]);
        await ShouldNotHoldAsync(result, log, host, LogRole);
    }

    // The commonest real failure: the server rejects the role's password.  The diagnostic holds the SQLSTATE and the
    // describer's own text, and names neither the role, the host nor the database, nor does the rest of the log.
    [Fact]
    public async Task Describe_WrongPasswordWithLog_IsSqlsrc224AndNothingPrintedOrLoggedHoldsTheUserOrTheHost()
    {
        await CreateLogRoleAndDatabaseAsync();
        _ = AddSql("one.sql", "-- name: One\nSELECT 1 AS one;\n");
        var connection = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString)
        {
            Username = LogRole,
            Password = WrongPassword,
            Database = LogDatabase,
        };
        var log = _run.Folder.PathOf("run.log");

        var result = await DescribeWithAsync(connection.ConnectionString, "--log", log);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC224: ");
        result.Error.ShouldContain("    server: 28P01: the server refused the password");
        ServerLinesOf(LogReader.Read(log), "SQLSRC224").ShouldBe(["28P01: the server refused the password"]);
        await ShouldNotHoldAsync(result, log, connection.Host!, LogRole, LogDatabase);
    }

    // The texts of the "server" lines of the one diagnostic of the log with the id.
    private static IReadOnlyList<string?> ServerLinesOf(IReadOnlyList<JsonElement> events, string id) =>
        [
            .. events
                .Where(logged => NameOf(logged) == "diagnostic" && logged.GetProperty("id").GetString() == id)
                .ShouldHaveSingleItem()
                .GetProperty("lines")
                .EnumerateArray()
                .Where(static line => line.GetProperty("label").GetString() == "server")
                .Select(static line => line.GetProperty("text").GetString()),
        ];

    // Neither what the run printed nor its log holds any of the words.
    private static async Task ShouldNotHoldAsync(CliResult result, string log, params string[] words)
    {
        var text = await File.ReadAllTextAsync(log, TestContext.Current.CancellationToken);
        foreach (var word in words)
        {
            text.ShouldNotContain(word);
            result.Out.ShouldNotContain(word);
            result.Error.ShouldNotContain(word);
        }
    }

    // CREATE ROLE and CREATE DATABASE have no IF NOT EXISTS, and the container outlives the test.
    private async Task CreateLogRoleAndDatabaseAsync()
    {
        if (await _postgres.ScalarAsync($"SELECT count(*) FROM pg_catalog.pg_roles WHERE rolname = '{LogRole}'") is 0L)
        {
            await _postgres.ExecuteAsync($"CREATE ROLE {LogRole} LOGIN PASSWORD '{Secrets.Marker}'");
        }

        if (
            await _postgres.ScalarAsync($"SELECT count(*) FROM pg_catalog.pg_database WHERE datname = '{LogDatabase}'")
            is 0L
        )
        {
            await _postgres.ExecuteAsync($"CREATE DATABASE {LogDatabase} OWNER {LogRole}");
        }
    }
}
