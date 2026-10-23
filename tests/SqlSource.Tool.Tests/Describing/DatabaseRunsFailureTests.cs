using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Step 3 of the run: a session that cannot be opened and a query that fails, how each is printed, and that the
// connection is not in it.
public sealed class DatabaseRunsFailureTests : IDisposable
{
    private readonly DatabaseRunsScene _scene = new();

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_SessionThatCannotBeOpened_IsReportedOnceAndTheDatabasesQueriesFail()
    {
        _ = _scene.Project.AddSql("More.sql", "-- name: More\nSELECT 3;\n");
        _scene.Describer.OpenFailure = FakeDescriber.Failure(
            "(open)",
            step: null,
            help: null,
            "28P01: password authentication failed"
        );

        var files = await _scene.RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

        DatabaseRunsScene.States(files).ShouldAllBe(state => state == QueryState.Failed);
        _scene.Describer.Described.ShouldBeEmpty();
        var more = _scene.Project.PathOf("More.sql");
        // SQLSRC213 for billing comes after, at its own query.
        _scene
            .Error.ToString()
            .ShouldStartWith(
                $"{more}(1,10): error SQLSRC999: The server rejected the query '(open)'\n"
                    + "    query: More, database postgres, postgres\n"
                    + "    server: 28P01: password authentication failed\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    // A driver that rejects a wrong connection string can quote it, and does so when the session is opened.
    [Fact]
    public async Task Run_SessionThatCannotBeOpenedAndHoldsTheConnection_IsPrintedWithoutIt()
    {
        _scene.Describer.OpenFailure = FakeDescriber.Failure(
            $"open on {Secrets.ConnectionWithWord}",
            step: null,
            $"check {Secrets.ConnectionWithWord}",
            $"invalid connection string: {Secrets.ConnectionWithWord}"
        );

        _ = await _scene.RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

        _scene.Error.ToString().ShouldNotContain(Secrets.Word);
        _scene
            .Error.ToString()
            .ShouldStartWith(
                $"{_scene.UsersPath}(1,10): error SQLSRC999: The server rejected the query 'open on ***'\n"
                    + "    query: GetUser, database postgres, postgres\n"
                    + "    server: invalid connection string: ***\n"
                    + "    help: check ***\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    // A driver's message for a wrong password can quote the password alone.
    [Fact]
    public async Task Run_SessionThatCannotBeOpenedAndHoldsAPasswordTheDescriberGave_IsPrintedWithoutIt()
    {
        _scene.Describer.Secrets = ["hunter2"];
        _scene.Describer.OpenFailure = FakeDescriber.Failure(
            "(open)",
            step: null,
            help: null,
            "28P01: password 'hunter2' was refused"
        );

        _ = await _scene.RunAsync(given: ["postgres=Host=db;Password=hunter2"]);

        _scene.Error.ToString().ShouldContain("    server: 28P01: password '***' was refused\n");
        _scene.Error.ToString().ShouldNotContain("hunter2");
    }

    // The run prints the database's name and the engine's whatever the describer does, so a password equal to them
    // is not removed from them.
    [Fact]
    public async Task Run_QueryThatFailsWithAPasswordThatIsTheDatabasesNameAndTheEngines_PrintsTheQueryLineAsItIs()
    {
        _scene.Describer.Secrets = ["postgres"];
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        _ = await _scene.RunAsync(given: ["postgres=Host=db;Password=postgres"]);

        _scene
            .Error.ToString()
            .ShouldContain("    query: GetUser, database postgres, postgres 16.4, FakeDriver 1.2.3\n");
    }

    // The same for the database's name alone, which is not the engine's.
    [Fact]
    public async Task Run_QueryThatFailsWithAPasswordThatIsTheDatabasesName_PrintsTheQueryLineAsItIs()
    {
        _scene.Describer.Secrets = ["billing"];
        _scene.Describer.Failures["GetInvoice"] = FakeDescriber.Failure("GetInvoice");

        _ = await _scene.RunAsync(given: ["postgres=Host=db;Password=billing", "billing=Host=db;Password=billing"]);

        _scene
            .Error.ToString()
            .ShouldContain("    query: GetInvoice, database billing, postgres 16.4, FakeDriver 1.2.3\n");
    }

    [Fact]
    public async Task Run_QueryThatFails_IsReportedInTheFormatOfADescribersErrorAndTheSessionGoesOn()
    {
        _ = _scene.Project.AddSql("More.sql", "-- name: Bad\nSELECT 3;\n\n-- name: Good\nSELECT 4;\n");
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure(
            "Bad",
            "42P01: relation \"users\" does not exist",
            "LINE 1"
        );

        var files = await _scene.RunAsync();

        DatabaseRunsScene
            .States(files)
            .ShouldBe([QueryState.Failed, QueryState.Described, QueryState.Described, QueryState.Described]);
        _scene.Describer.Closed.ShouldBe(2);
        _scene
            .Error.ToString()
            .ShouldBe(
                $"{_scene.Project.PathOf("More.sql")}(1,10): error SQLSRC999: The server rejected the query 'Bad'\n"
                    + "    query: Bad, database postgres, postgres 16.4, FakeDriver 1.2.3\n"
                    + "    step: describe columns\n"
                    + "    server: 42P01: relation \"users\" does not exist\n"
                    + "    server: LINE 1\n"
                    + "    help: check the SQL\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    [Fact]
    public async Task Run_DriverWithoutAVersion_IsNamedAlone()
    {
        _scene.Describer.Server = _scene.Describer.Server with { DriverVersion = null };
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        _ = await _scene.RunAsync();

        _scene.Error.ToString().ShouldContain("    query: GetUser, database postgres, postgres 16.4, FakeDriver\n");
    }

    // The describer's rule is to put the value in nothing it returns.  The run does not rely on it.
    [Fact]
    public async Task Run_FailureThatHoldsTheConnection_IsPrintedWithoutIt()
    {
        _scene.Describer.Server = new ServerInfo(
            $"16.4 at {Secrets.ConnectionWithWord}",
            Secrets.ConnectionWithWord,
            Secrets.ConnectionWithWord,
            EquatableArray<ServerSetting>.Empty
        );
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure(
            $"GetUser on {Secrets.ConnectionWithWord}",
            DescribeStep.Catalog,
            $"check {Secrets.ConnectionWithWord}",
            $"could not connect to {Secrets.ConnectionWithWord}"
        );
        // A query that is described would put the server's version, which holds the value, into its entry, and that
        // ends the run.
        _scene.Describer.Failures["GetInvoice"] = FakeDescriber.Failure("GetInvoice");

        _ = await _scene.RunAsync();

        _scene.Error.ToString().ShouldNotContain(Secrets.Word);
        _scene
            .Error.ToString()
            .ShouldBe(
                $"{_scene.UsersPath}(1,10): error SQLSRC999: The server rejected the query 'GetUser on ***'\n"
                    + "    query: GetUser, database postgres, postgres 16.4 at ***, *** ***\n"
                    + "    step: catalog\n"
                    + "    server: could not connect to ***\n"
                    + "    help: check ***\n"
                    + "    see: https://example.test/sqlsrc999\n"
                    + $"{_scene.UsersPath}(4,10): error SQLSRC999: The server rejected the query 'GetInvoice'\n"
                    + "    query: GetInvoice, database billing, postgres 16.4 at ***, *** ***\n"
                    + "    step: describe columns\n"
                    + "    help: check the SQL\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }
}
