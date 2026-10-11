using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// No value of a connection is ever printed: not by an error of the run, not for a command line that is
// wrong, and not when a describer breaks its own rule.
public sealed class SecretTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public SecretTests()
    {
        _scene.Run.Environment.Clear();
        _users = _scene.Project.AddSql("Users.sql", "-- name: Bad\nSELECT 1;\n\n-- name: Good\nSELECT 2;\n");
    }

    public void Dispose() => _scene.Dispose();

    // A project with every error a describe run reports of its databases and sidecars but SQLSRC215, when "postgres"
    // has a connection:
    // a failure of the describer and SQLSRC217 in Users.sql, SQLSRC213 for "reports", SQLSRC214 for the two "app"
    // databases, SQLSRC216 for "mssql", SQLSRC218 for a sidecar that cannot be written, and SQLSRC221.
    private void AddEveryError()
    {
        _scene.Describer.Failures["Bad"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>(["Bad"]),
            DescribeStep.Explain,
            new EquatableArray<string>(["could not explain"]),
            "check the SQL"
        );
        _ = _scene.Project.AddSql("Reports.sql", "-- name: Report\n-- database: reports\nSELECT 3;\n");
        _ = _scene.Project.AddSql(
            "Shared.sql",
            "-- name: One\n-- database: app-v2\nSELECT 4;\n\n-- name: Two\n-- database: app_v2\nSELECT 5;\n"
        );
        _ = _scene.Project.AddSql("Other.sql", "-- dialect: mssql\n-- name: Other\nSELECT 6;\n");
        var locked = _scene.Project.AddSql("Locked.sql", "-- name: Locked\nSELECT 7;\n");
        _ = Directory.CreateDirectory(SidecarFormat.PathFor(locked));
        var newer = _scene.Project.AddSql("Newer.sql", "-- name: Newer\nSELECT 8;\n");
        File.WriteAllText(SidecarFormat.PathFor(newer), SidecarFiles.Newer);
    }

    private static void ShouldHold(CliResult result, string ids)
    {
        result.ExitCode.ShouldBe(1);
        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        foreach (var id in ids.Split(','))
        {
            result.Error.ShouldContain($"error SQLSRC{id}: ");
        }
    }

    [Theory]
    [InlineData("--connection", "postgres=" + Secrets.ConnectionWithMarker)]
    [InlineData("--connection=postgres=" + Secrets.ConnectionWithMarker)]
    [InlineData("--connection:postgres=" + Secrets.ConnectionWithMarker)]
    public async Task Run_ValueOnTheCommandLineAndEveryError_IsNeverPrinted(params string[] args)
    {
        AddEveryError();

        var result = await _scene.DescribeAsync(args);

        ShouldHold(result, "213,214,216,217,218,221,999");
        _scene.Describer.Opened.ShouldAllBe(request => request.Connection == Secrets.ConnectionWithMarker);
    }

    // The same run with a log: the file holds every error of it, and the marker word nowhere.
    [Fact]
    public async Task Run_ValueOnTheCommandLineAndEveryErrorWithLog_IsNeverLogged()
    {
        AddEveryError();

        var result = await _scene.DescribeAsync(
            "--connection",
            "postgres=" + Secrets.ConnectionWithMarker,
            "--log",
            "run.log"
        );

        ShouldHold(result, "213,214,216,217,218,221,999");
        var log = await File.ReadAllTextAsync(
            _scene.Run.Folder.PathOf("run.log"),
            TestContext.Current.CancellationToken
        );
        log.ShouldNotContain(Secrets.Marker);
        log.ShouldContain("\"id\":\"SQLSRC999\"");
    }

    [Fact]
    public async Task Run_ValueOnTheCommandLineAndEveryErrorWithVerbose_IsNeverSaid()
    {
        AddEveryError();

        var result = await _scene.DescribeAsync(
            "--connection",
            "postgres=" + Secrets.ConnectionWithMarker,
            "--verbose"
        );

        ShouldHold(result, "213,214,216,217,218,221,999");
        result.Out.ShouldContain("sqlsource: database 'postgres' (postgres): connection from --connection\n");
    }

    [Fact]
    public async Task Run_ValueInTheVariableOfTheNameAndEveryError_IsNeverPrinted()
    {
        AddEveryError();
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.ConnectionWithMarker;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_APP_V2"] = Secrets.ConnectionWithMarker;

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "213,214,216,217,218,221,999");
    }

    // With several databases the variable with no name is used for none, which is SQLSRC215.
    [Fact]
    public async Task Run_ValueInTheVariableWithNoNameAndEveryError_IsNeverPrinted()
    {
        AddEveryError();
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "214,215,216,221");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ValueInTheVariableWithNoNameAndOneDatabase_IsUsedAndNeverPrinted()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad");

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "217,999");
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
    }

    // The value was typed where "name=value" stands.  Its first keyword is taken for a name, and only that is said.
    [Fact]
    public async Task Run_ConnectionStringGivenWithoutAName_IsNeverPrinted()
    {
        var result = await _scene.DescribeAsync("--connection", Secrets.ConnectionWithMarker);

        ShouldHold(result, "213");
        result.Error.ShouldContain("--connection was given for Host, which is no database of this run");
    }

    [Theory]
    [InlineData("--connection", Secrets.Marker)]
    [InlineData("--connection=" + Secrets.Marker)]
    [InlineData("--connection:" + Secrets.Marker)]
    [InlineData("--connection", "postgres=a", "--connection", "POSTGRES=" + Secrets.Marker)]
    public async Task Run_ConnectionThatIsWrong_IsAWrongCommandLineThatDoesNotRepeatIt(params string[] args)
    {
        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith("sqlsource: ");
        result.Error.ShouldNotContain(Secrets.Marker);
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // A connection string that the shell split at a space: the rest stands where the path of the unit stands.
    [Fact]
    public async Task Run_ConnectionStringSplitByTheShell_IsAWrongCommandLineThatDoesNotRepeatIt()
    {
        var result = await _scene.DescribeAsync(
            "--connection",
            "postgres=Server=x;User",
            "Id=sa;Password=" + Secrets.Marker
        );

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource: the argument at position 4 looks like a part of a connection string; put the value of "
                    + "'--connection' in quotes\n"
            )
        );
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // The part that is left ends in ".sql", which would make it a file filter that SQLSRC212 prints in full.
    [Fact]
    public async Task Run_ConnectionStringSplitByTheShellWithAPartThatEndsInSql_IsAWrongCommandLineThatDoesNotRepeatIt()
    {
        var result = await _scene.DescribeAsync(
            "--connection",
            "postgres=Server=x;User",
            "Id=sa;Password=" + Secrets.Marker + ".sql"
        );

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource: the argument at position 4 looks like a part of a connection string; put the value of "
                    + "'--connection' in quotes\n"
            )
        );
        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // The rule for an unknown option: nothing after it is read.
    [Fact]
    public async Task Run_MisspeltConnectionOptionFollowedByTheValue_NamesTheOptionAlone()
    {
        var result = await _scene.DescribeAsync("--conection", "postgres=" + Secrets.ConnectionWithMarker);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--conection'\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatThrowsWithTheValue_PrintsTheMarkInItsPlace(string? debug)
    {
        if (debug is not null)
        {
            _scene.Run.Environment[Cli.DebugVariable] = debug;
        }

        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;
        _scene.Describer.OpenException = new ArgumentException(
            $"Format of the initialization string: {Secrets.ConnectionWithMarker}"
        );

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.ArgumentException: Format of the "
                + "initialization string: ***\n"
        );
        result.Error.Contains("    trace: ", StringComparison.Ordinal).ShouldBe(debug is not null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatReturnsTheValue_PrintsTheMarkInEachPlace(string? debug)
    {
        if (debug is not null)
        {
            _scene.Run.Environment[Cli.DebugVariable] = debug;
        }

        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;
        _scene.Describer.Server = new ServerInfo(
            $"16.4 ({Secrets.ConnectionWithMarker})",
            "FakeDriver",
            null,
            EquatableArray<ServerSetting>.Empty
        );
        _scene.Describer.Failures["Bad"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>([$"Bad at {Secrets.ConnectionWithMarker}"]),
            DescribeStep.DescribeColumns,
            new EquatableArray<string>([$"connection to {Secrets.ConnectionWithMarker} was lost"]),
            $"check {Secrets.ConnectionWithMarker}"
        );
        // A query that is described would put the server's version, which holds the value, into its entry, and that
        // ends the run.
        _scene.Describer.Failures["Good"] = FakeDescriber.Failure("Good");

        var result = await _scene.DescribeAsync();

        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldStartWith(
            $"{_users}(1,10): error SQLSRC999: The server rejected the query 'Bad at ***'\n"
                + "    query: Bad, database postgres, postgres 16.4 (***), FakeDriver\n"
                + "    step: describe columns\n"
                + "    server: connection to *** was lost\n"
                + "    help: check ***\n"
        );
    }

    // A description that holds the value is a bug of the describer: the run ends, and nothing is written, since a
    // sidecar is a file that is committed.
    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatPutsTheValueInItsDescription_EndsTheRunAndWritesNoSidecar(string? debug)
    {
        if (debug is not null)
        {
            _scene.Run.Environment[Cli.DebugVariable] = debug;
        }

        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;
        _scene.Describer.Server = new ServerInfo(
            $"16.4 ({Secrets.ConnectionWithMarker})",
            "FakeDriver",
            null,
            EquatableArray<ServerSetting>.Empty
        );

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.InvalidOperationException: The "
                + "describer put the connection's value in its description of the query 'Bad'.\n"
        );
        File.Exists(SidecarFormat.PathFor(_users)).ShouldBeFalse();
    }

    // --check prints what differs, and a text of the describer is among it.  The guard on a description comes first,
    // so the comparison that would print the value is never reached.
    [Fact]
    public async Task Check_DescriberThatPutsTheValueInAColumnOfAQueryThatDiffers_EndsTheRunAndPrintsNothingOfIt()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["Good"] = new QueryDescription(
            SidecarResultKind.Rows,
            EquatableArray<DescribedParameter>.Empty,
            new EquatableArray<SidecarColumn>([
                new SidecarColumn(
                    0,
                    Secrets.ConnectionWithMarker,
                    new OtherEngineType("int4"),
                    null,
                    null,
                    null,
                    null,
                    null
                ),
            ]),
            null,
            null,
            null
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.InvalidOperationException: The "
                + "describer put the connection's value in its description of the query 'Good'.\n"
        );
        result.Error.ShouldNotContain("SQLSRC219");
    }

    // What is written to a sidecar is not printed, and is the server's own word.
    [Fact]
    public async Task Run_QueriesDescribed_PrintNothingOfTheConnection()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        (
            await File.ReadAllTextAsync(SidecarFormat.PathFor(_users), TestContext.Current.CancellationToken)
        ).ShouldNotContain(Secrets.Marker);
    }
}
