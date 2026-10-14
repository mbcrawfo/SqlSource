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
using SqlSource.Tool.Tests;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The whole "describe" command in process, as Cli.Run runs it, with the PostgreSQL describer against the container:
// one postgres project in the run's folder whose .sql files are of the database "app", and that database's
// connection on the command line.  The tool's own tests run the same command with a describer of their own.
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

    // The cases of the describer's fixtures that a describer with no settings of the planner describes.  A case
    // with an explain-settings.txt turns join methods off, which a run of the command cannot.
    private static readonly IReadOnlyList<FixtureCase> Cases =
    [
        .. FixtureCases
            .Of("postgres", RepositoryPaths.DescriberFixtures)
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

    // Runs a statement on a connection of the test's own, outside a transaction.
    private async Task<object?> ExecuteAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(_postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

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
        _ = await ExecuteAsync("CREATE TABLE command_widgets (id integer PRIMARY KEY, name text NOT NULL)");
        try
        {
            var widgets = AddSql("widgets.sql", "-- name: GetWidgets\nSELECT * FROM command_widgets;\n");
            var described = await DescribeAsync();
            described.ExitCode.ShouldBe(0, described.Error);
            _ = await ExecuteAsync("ALTER TABLE command_widgets ADD COLUMN note text");

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
            _ = await ExecuteAsync("DROP TABLE IF EXISTS command_widgets");
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

    // The port 1 of the container's host refuses at once.  The diagnostic says what the driver said, host and all,
    // which docs/tech-debt/TD-0034 records; the driver's own events say nothing of it.
    [Fact]
    public async Task Describe_ConnectionRefusedWithLog_IsSqlsrc224AndNoDriverEventHoldsTheHost()
    {
        _ = AddSql("one.sql", "-- name: One\nSELECT 1 AS one;\n");
        var host = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString).Host.ShouldNotBeNull();
        var connection = new NpgsqlConnectionStringBuilder { Host = host, Port = 1 };
        var log = _run.Folder.PathOf("run.log");

        var result = await DescribeWithAsync(connection.ConnectionString, "--log", log);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC224: ");
        var events = LogReader.Read(log);
        events.ShouldContain(logged =>
            NameOf(logged) == "diagnostic"
            && logged.GetProperty("id").GetString() == "SQLSRC224"
            && logged.GetProperty("lines").GetArrayLength() > 0
        );
        foreach (var driver in events.Where(logged => NameOf(logged) == DriverEvent))
        {
            driver.GetRawText().ShouldNotContain(host);
        }
    }

    // CREATE ROLE and CREATE DATABASE have no IF NOT EXISTS, and the container outlives the test.
    private async Task CreateLogRoleAndDatabaseAsync()
    {
        if (await ExecuteAsync($"SELECT count(*) FROM pg_catalog.pg_roles WHERE rolname = '{LogRole}'") is 0L)
        {
            _ = await ExecuteAsync($"CREATE ROLE {LogRole} LOGIN PASSWORD '{Secrets.Marker}'");
        }

        if (await ExecuteAsync($"SELECT count(*) FROM pg_catalog.pg_database WHERE datname = '{LogDatabase}'") is 0L)
        {
            _ = await ExecuteAsync($"CREATE DATABASE {LogDatabase} OWNER {LogRole}");
        }
    }
}
