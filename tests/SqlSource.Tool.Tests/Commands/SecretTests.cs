using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
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
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure(
            "Bad",
            DescribeStep.Explain,
            "check the SQL",
            "could not explain"
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

    // The describer of PostgreSQL itself, given a connection that nothing answers: the port 1 of this machine
    // refuses at once.  The describer's own text is printed, and neither the value nor the host is anywhere: not
    // printed, not in the log, not in the capture.
    [Fact]
    public async Task Run_PostgresDescriberThatCannotOpen_PrintsSqlsrc224AndNothingOfTheValue()
    {
        _scene.Run.Describers.Clear();
        _scene.Run.Describers.Add(new PostgresDescriber());
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] =
            "Host=127.0.0.1;Port=1;Timeout=5;Password=" + Secrets.Marker;

        var result = await _scene.DescribeAsync("--log", "run.log");

        ShouldHold(result, "224");
        result.Error.ShouldContain("    server: Npgsql could not reach the server: ConnectionRefused");
        result.Error.ShouldNotContain("127.0.0.1");
        var log = await File.ReadAllTextAsync(
            _scene.Run.Folder.PathOf("run.log"),
            TestContext.Current.CancellationToken
        );
        log.ShouldNotContain(Secrets.Marker);
        log.ShouldNotContain("127.0.0.1");
        log.ShouldContain("\"id\":\"SQLSRC224\"");
        CaptureJson.Write(_scene.Run.Capture).ShouldNotContain(Secrets.Marker);
        CaptureJson.Write(_scene.Run.Capture).ShouldNotContain("127.0.0.1");
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

    // A capture is a file that a user attaches to a report: nothing of a connection is in what a run recorded.
    [Fact]
    public async Task Run_ThroughACapturingExchange_LeavesNothingOfTheValueInTheCapture()
    {
        var result = await _scene.DescribeAsync("--connection", "postgres=" + Secrets.ConnectionWithMarker);

        result.ExitCode.ShouldBe(0);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
        _scene.Run.Capture.Calls.Select(call => call.Method).ShouldBe(["open", "describe", "describe"]);
        CaptureJson.Write(_scene.Run.Capture).ShouldNotContain(Secrets.Marker);
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

    // A password typed the way mysql takes one is far from every option, so only its position is printed.
    [Fact]
    public async Task Run_SecretTypedAsAShortOption_IsNamedByItsPosition()
    {
        var result = await _scene.DescribeAsync("-p" + Secrets.Marker);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option at position 2\n"));
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
        _scene.Describer.Server = FakeDescriber.ServerWith($"16.4 ({Secrets.ConnectionWithMarker})");
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure(
            $"Bad at {Secrets.ConnectionWithMarker}",
            DescribeStep.DescribeColumns,
            $"check {Secrets.ConnectionWithMarker}",
            $"connection to {Secrets.ConnectionWithMarker} was lost"
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

    // The descriptor of a failure is a describer's text too, when the describer built it from what a server said.
    [Fact]
    public async Task Run_DescriberWhoseFailureHasADescriptorWithTheValue_PrintsTheMarkInEachPlace()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithMarker;
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad") with
        {
            Descriptor = FakeDescriber.RejectedWith(Secrets.ConnectionWithMarker),
        };

        var result = await _scene.DescribeAsync();

        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldStartWith(
            $"{_users}(1,10): error ***: *** rejected the query 'Bad'\n"
                + "    query: Bad, database postgres, postgres 16.4, FakeDriver 1.2.3\n"
                + "    step: describe columns\n"
                + "    help: check the SQL\n"
                + "    see: https://example.test/***\n"
        );
    }

    // A describer cuts what a server said into lines, and a value that holds a line break is then in two of them.
    // The cut drops the kind of break, so the value is found whichever kind it has.
    [Theory]
    [InlineData("Host=db;\nPassword=s3cret-marker")]
    [InlineData("Host=db;\r\nPassword=s3cret-marker")]
    [InlineData("Host=db;\rPassword=s3cret-marker")]
    public async Task Run_DescriberThatReturnsTheValueOverTwoServerLines_PrintsTheMarkOnOne(string value)
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = value;
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure(
            "Bad",
            "cannot read 'Host=db;",
            "Password=s3cret-marker' as a connection",
            "no session"
        );

        var result = await _scene.DescribeAsync();

        result.Out.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldNotContain(Secrets.Marker);
        result.Error.ShouldStartWith(
            $"{_users}(1,10): error SQLSRC999: The server rejected the query 'Bad'\n"
                + "    query: Bad, database postgres, postgres 16.4, FakeDriver 1.2.3\n"
                + "    step: describe columns\n"
                + "    server: cannot read '***' as a connection\n"
                + "    server: no session\n"
                + "    help: check the SQL\n"
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
        _scene.Describer.Server = FakeDescriber.ServerWith($"16.4 ({Secrets.ConnectionWithMarker})");

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

    // A value that the sidecar holds without the describer, as a word of the format, the engine or the name of the
    // database, is no value the describer put there: such a run describes its queries like any other.
    [Theory]
    [InlineData("postgres")]
    [InlineData("resultKind")]
    [InlineData("Good")]
    public async Task Run_ConnectionThatTheSidecarHoldsWithoutTheDescriber_DescribesAndWritesTheSidecar(string value)
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = value;

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        File.Exists(SidecarFormat.PathFor(_users)).ShouldBeTrue();
    }

    // A description is searched for the whole value alone: a password that equals a column's name is the catalog's.
    [Fact]
    public async Task Run_PasswordTheDescriberGaveThatIsAColumnsName_DescribesAndWritesTheSidecar()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = "Host=db;Password=password";
        _scene.Describer.Secrets = ["password"];
        _scene.Describer.Descriptions["Good"] = FakeDescriber.Rows with
        {
            Columns = new EquatableArray<SidecarColumn>([
                new SidecarColumn(0, "password", new OtherEngineType("text"), null, null, null, null, null),
            ]),
        };

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        (
            await File.ReadAllTextAsync(SidecarFormat.PathFor(_users), TestContext.Current.CancellationToken)
        ).ShouldContain("\"name\": \"password\"");
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
