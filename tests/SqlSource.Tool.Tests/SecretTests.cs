using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

// No value of a connection is ever printed: not by an error of this sub-phase, not for a command line that is
// wrong, and not when a describer breaks its own rule.
public sealed class SecretTests : IDisposable
{
    private const string Marker = "s3cret-marker";

    private const string Value = "Host=db;Password=" + Marker;

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public SecretTests()
    {
        _scene.Run.Environment.Clear();
        _users = _scene.Project.AddSql("Users.sql", "-- name: Bad\nSELECT 1;\n\n-- name: Good\nSELECT 2;\n");
    }

    public void Dispose() => _scene.Dispose();

    // A project with an error of every kind of this sub-phase but SQLSRC215, when "postgres" has a connection:
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
        _ = Directory.CreateDirectory(locked + ".json");
        var newer = _scene.Project.AddSql("Newer.sql", "-- name: Newer\nSELECT 8;\n");
        File.WriteAllText(
            newer + ".json",
            SidecarWriter.Write(new Sidecar(2, "9.9.9", EquatableArray<SidecarEntry>.Empty))
        );
    }

    private static void ShouldHold(CliResult result, string ids)
    {
        result.ExitCode.ShouldBe(1);
        result.Out.ShouldNotContain(Marker);
        result.Error.ShouldNotContain(Marker);
        foreach (var id in ids.Split(','))
        {
            result.Error.ShouldContain($"error SQLSRC{id}: ");
        }
    }

    [Theory]
    [InlineData("--connection", "postgres=" + Value)]
    [InlineData("--connection=postgres=" + Value)]
    [InlineData("--connection:postgres=" + Value)]
    public async Task Run_ValueOnTheCommandLineAndEveryError_IsNeverPrinted(params string[] args)
    {
        AddEveryError();

        var result = await _scene.DescribeAsync(args);

        ShouldHold(result, "213,214,216,217,218,221,999");
        _scene.Describer.Opened.ShouldAllBe(request => request.Connection == Value);
    }

    [Fact]
    public async Task Run_ValueInTheVariableOfTheNameAndEveryError_IsNeverPrinted()
    {
        AddEveryError();
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Value;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_APP_V2"] = Value;

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "213,214,216,217,218,221,999");
    }

    // With several databases the variable with no name is used for none, which is SQLSRC215.
    [Fact]
    public async Task Run_ValueInTheVariableWithNoNameAndEveryError_IsNeverPrinted()
    {
        AddEveryError();
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "214,215,216,221");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ValueInTheVariableWithNoNameAndOneDatabase_IsUsedAndNeverPrinted()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad");

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "217,999");
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Value);
    }

    // The value was typed where "name=value" stands.  Its first keyword is taken for a name, and only that is said.
    [Fact]
    public async Task Run_ConnectionStringGivenWithoutAName_IsNeverPrinted()
    {
        var result = await _scene.DescribeAsync("--connection", Value);

        ShouldHold(result, "213");
        result.Error.ShouldContain("--connection was given for Host, which is no database of this run");
    }

    [Theory]
    [InlineData("--connection", Marker)]
    [InlineData("--connection=" + Marker)]
    [InlineData("--connection:" + Marker)]
    [InlineData("--connection", "postgres=a", "--connection", "POSTGRES=" + Marker)]
    public async Task Run_ConnectionThatIsWrong_IsAWrongCommandLineThatDoesNotRepeatIt(params string[] args)
    {
        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith("sqlsource: ");
        result.Error.ShouldNotContain(Marker);
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // A connection string that the shell split at a space: the rest stands where the path of the unit stands.
    [Fact]
    public async Task Run_ConnectionStringSplitByTheShell_IsAWrongCommandLineThatDoesNotRepeatIt()
    {
        var result = await _scene.DescribeAsync("--connection", "postgres=Server=x;User", "Id=sa;Password=" + Marker);

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

    // The rule of sub-phase 2.2 for an unknown option: nothing after it is read.
    [Fact]
    public async Task Run_MisspeltConnectionOptionFollowedByTheValue_NamesTheOptionAlone()
    {
        var result = await _scene.DescribeAsync("--conection", "postgres=" + Value);

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

        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;
        _scene.Describer.OpenException = new ArgumentException($"Format of the initialization string: {Value}");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldNotContain(Marker);
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

        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;
        _scene.Describer.Server = new ServerInfo(
            $"16.4 ({Value})",
            "FakeDriver",
            null,
            EquatableArray<ServerSetting>.Empty
        );
        _scene.Describer.Failures["Bad"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>([$"Bad at {Value}"]),
            DescribeStep.DescribeColumns,
            new EquatableArray<string>([$"connection to {Value} was lost"]),
            $"check {Value}"
        );

        var result = await _scene.DescribeAsync();

        result.Out.ShouldNotContain(Marker);
        result.Error.ShouldNotContain(Marker);
        result.Error.ShouldStartWith(
            $"{_users}(1,10): error SQLSRC999: The server rejected the query 'Bad at ***'\n"
                + "    query: Bad, database postgres, postgres 16.4 (***), FakeDriver\n"
                + "    step: describe columns\n"
                + "    server: connection to *** was lost\n"
                + "    help: check ***\n"
        );
    }

    // What is written to a sidecar is not printed, and is the server's own word.
    [Fact]
    public async Task Run_QueriesDescribed_PrintNothingOfTheConnection()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        (await File.ReadAllTextAsync(_users + ".json", TestContext.Current.CancellationToken)).ShouldNotContain(Marker);
    }
}
