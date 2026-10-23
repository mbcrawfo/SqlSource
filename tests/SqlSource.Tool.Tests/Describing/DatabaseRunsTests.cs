using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Step 3 of the run: one session for each database, the connection each is given, and a database with nothing to
// describe.
public sealed class DatabaseRunsTests : IDisposable
{
    private readonly DatabaseRunsScene _scene = new();

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_NoDescriberForTheDialect_IsSqlsrc216AtEachQuery()
    {
        var files = await _scene.RunAsync(registered: false);

        DatabaseRunsScene.States(files).ShouldBe([QueryState.Failed, QueryState.Failed]);
        _scene
            .Error.ToString()
            .ShouldBe(
                $"{_scene.UsersPath}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n"
                    + Expected.See("SQLSRC216")
                    + $"{_scene.UsersPath}(4,10): error SQLSRC216: This version of sqlsource cannot describe "
                    + $"'postgres'\n{Expected.See("SQLSRC216")}"
            );
    }

    [Fact]
    public async Task Run_Describer_DescribesEachQueryInOneSessionForItsDatabase()
    {
        var files = await _scene.RunAsync();

        _scene.Error.ToString().ShouldBeEmpty();
        DatabaseRunsScene.States(files).ShouldBe([QueryState.Described, QueryState.Described]);
        _scene.Describer.Opened.Select(request => request.Database).ShouldBe(["postgres", "billing"]);
        _scene.Describer.Opened.ShouldAllBe(request =>
            request.Connection == Secrets.ConnectionWithWord && request.Exchange == _scene.Exchange
        );
        _scene.Describer.Closed.ShouldBe(2);
        _scene.Capture.Calls.Select(call => call.Method).ShouldBe(["open", "describe", "open", "describe"]);

        var getUser = _scene.Describer.Described[0];
        getUser.Name.ShouldBe("GetUser");
        getUser.Sql.ShouldBe("SELECT 1 WHERE @id = 1;");
        getUser.Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");

        var entry = files[0].Queries[1].Entry.ShouldNotBeNull();
        entry.Name.ShouldBe("GetInvoice");
        entry.Hash.ShouldBe(files[0].Queries[1].Planned.Hash);
        entry.Engine.ShouldBe("postgres");
        entry.Database.ShouldBe("billing");
        entry.ServerVersion.ShouldBe("16.4");
    }

    [Fact]
    public async Task Run_DatabaseWithoutAConnection_IsSqlsrc213OnceAndItsQueriesFail()
    {
        _ = _scene.Project.AddSql("More.sql", "-- name: More\nSELECT 3;\n");

        var files = await _scene.RunAsync(given: ["billing=" + Secrets.ConnectionWithWord]);

        DatabaseRunsScene.States(files).ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.Described]);
        _scene.Describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("billing");
        var more = _scene.Project.PathOf("More.sql");
        _scene
            .Error.ToString()
            .ShouldBe(
                $"{more}(1,10): error SQLSRC213: No connection is given for the database 'postgres'\n"
                    + "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES\n"
                    + Expected.See("SQLSRC213")
            );
    }

    [Fact]
    public async Task Run_ConnectionForANameCloseToADatabase_IsListedInTheHelpOfSqlsrc213()
    {
        _ = await _scene.RunAsync(
            given:
            [
                "billing=" + Secrets.ConnectionWithWord,
                "postgress=" + Secrets.ConnectionWithWord,
                "Billng=" + Secrets.ConnectionWithWord,
            ]
        );

        _scene
            .Error.ToString()
            .ShouldContain(
                "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES.  "
                    + "--connection was given for postgress, Billng, which are no databases of this run\n"
            );
    }

    [Fact]
    public async Task Run_ConnectionForANameFarFromEveryDatabase_IsCountedByItsPositionInTheHelpOfSqlsrc213()
    {
        _ = await _scene.RunAsync(
            given:
            [
                "billing=" + Secrets.ConnectionWithWord,
                "reports=" + Secrets.ConnectionWithWord,
                "postgress=" + Secrets.ConnectionWithWord,
                "Host=" + Secrets.ConnectionWithWord,
            ]
        );

        _scene
            .Error.ToString()
            .ShouldContain(
                "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES.  "
                    + "--connection was given for postgress, which is no database of this run.  "
                    + "The values of --connection at positions 5 and 9 name no databases of this run\n"
            );
        _scene.Error.ToString().ShouldNotContain("reports");
        _scene.Error.ToString().ShouldNotContain("Host");
    }

    [Theory]
    [InlineData(new[] { "Host" }, "The value of --connection at position 5 names no database of this run")]
    [InlineData(
        new[] { "Host", "Server" },
        "The values of --connection at positions 5 and 7 name no databases of this run"
    )]
    [InlineData(
        new[] { "Host", "Server", "Uid" },
        "The values of --connection at positions 5, 7 and 9 name no databases of this run"
    )]
    public async Task Run_ConnectionsForNamesFarFromEveryDatabase_AreCountedInTheHelpOfSqlsrc213(
        string[] far,
        string sentence
    )
    {
        _ = await _scene.RunAsync(
            given:
            [
                "billing=" + Secrets.ConnectionWithWord,
                .. far.Select(name => name + "=" + Secrets.ConnectionWithWord),
            ]
        );

        _scene
            .Error.ToString()
            .ShouldContain(
                "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES.  "
                    + sentence
                    + "\n"
            );
    }

    [Fact]
    public async Task Run_TwoDatabasesWithOneVariable_IsSqlsrc214AtEachThatHasAQueryToDescribe()
    {
        var project = Plans.Postgres(_scene.Folder, "Other");
        var sql = project.AddSql(
            "Q.sql",
            "-- name: A\n-- database: app-v2\nSELECT 1;\n\n-- name: B\n-- database: app_v2\nSELECT 2;\n"
        );
        _scene.Environment["SQLSOURCE_CONNECTION_APP_V2"] = Secrets.ConnectionWithWord;

        var files = await _scene.RunAsync(Plans.Of(project), given: []);

        DatabaseRunsScene.States(files).ShouldBe([QueryState.Failed, QueryState.Failed]);
        _scene.Describer.Opened.ShouldBeEmpty();
        _scene
            .Error.ToString()
            .ShouldBe(
                $"{sql}(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from "
                    + $"SQLSOURCE_CONNECTION_APP_V2\n{Expected.See("SQLSRC214")}"
                    + $"{sql}(5,10): error SQLSRC214: The databases 'app_v2' and 'app-v2' both read their connection "
                    + $"from SQLSOURCE_CONNECTION_APP_V2\n{Expected.See("SQLSRC214")}"
            );
    }

    [Fact]
    public async Task Run_VariableWithNoNameAndTwoDatabases_IsSqlsrc215ForTheOneWithoutAConnection()
    {
        _scene.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithWord;

        var files = await _scene.RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

        DatabaseRunsScene.States(files).ShouldBe([QueryState.Described, QueryState.Failed]);
        _scene
            .Error.ToString()
            .ShouldBe(
                $"{_scene.UsersPath}(4,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, "
                    + "and this run has 2: postgres, billing\n"
                    + "    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_BILLING for "
                    + "'billing', or pass --connection <name>=<connection string> for each\n"
                    + Expected.See("SQLSRC215")
            );
    }

    // The database "ansi" is in the run through a file that cannot be described, so the variable is not for it.
    [Fact]
    public async Task Run_VariableWithNoNameAndADatabaseThatCannotBeDescribed_IsSqlsrc215ThatLeavesItOut()
    {
        _ = _scene.Project.AddSql("Old.sql", "-- dialect: ansi\n-- name: Old\nSELECT 1;\n");
        _scene.Environment["SQLSOURCE_CONNECTION"] = Secrets.ConnectionWithWord;

        _ = await _scene.RunAsync(given: ["postgres=" + Secrets.ConnectionWithWord]);

        _scene
            .Error.ToString()
            .ShouldBe(
                $"{_scene.UsersPath}(4,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, "
                    + "and this run has 2: postgres, billing\n"
                    + "    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_BILLING for "
                    + "'billing', or pass --connection <name>=<connection string> for each\n"
                    + Expected.See("SQLSRC215")
            );
    }

    // A database is opened, and needs a connection, only for a query to describe.
    [Fact]
    public async Task Run_DatabaseWithNothingToDescribe_NeedsNoConnectionAndIsNotOpened()
    {
        _ = _scene.Project.AddSql("Tokens.sql", "-- name: Find\n-- database: billing\nSELECT 1 {{where}};\n");
        var plan = Plans.Of(_scene.Project);
        _ = SidecarFiles.WriteCurrent(plan.Files.Single(file => file.Path == _scene.UsersPath));

        var files = await _scene.RunAsync(plan, given: []);

        _scene.Error.ToString().ShouldBeEmpty();
        _scene.Describer.Opened.ShouldBeEmpty();
        DatabaseRunsScene.States(files).ShouldBe([QueryState.Failed, QueryState.Skipped, QueryState.Skipped]);
    }
}
