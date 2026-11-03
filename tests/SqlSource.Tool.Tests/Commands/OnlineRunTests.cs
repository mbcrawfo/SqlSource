using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// What "describe --manifest --online" says: a build reads it, so an error is one line, a run with no connection says
// it is offline and fails nothing, and a run with nothing to describe says so.
public sealed class OnlineRunTests : IDisposable
{
    private const string Offline = "sqlsource: offline: no connection for ";
    private const string Committed = "; the build uses the committed sidecars\n";
    private const string NothingToDescribe = "sqlsource: online: nothing to describe\n";

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public OnlineRunTests()
    {
        _scene.Run.Environment.Clear();
        // GetUser is of "postgres" and GetInvoice of "billing".
        _users = _scene.Project.AddSql("Users.sql", SqlTexts.Users);
    }

    public void Dispose() => _scene.Dispose();

    private string Sidecar => SidecarFormat.PathFor(_users);

    // Where the run looks for its .env file, which none of these tests writes.
    private string DotEnvPath => _scene.Run.Folder.PathOf(".env");

    private Task<CliResult> OnlineAsync(string mode, params string[] more)
    {
        var manifest = _scene.Run.Folder.WriteFile("obj/App.csproj.manifest", _scene.Project.ManifestText());
        return _scene.DescribeAsync(["--manifest", manifest, "--online", mode, .. more]);
    }

    // Writes the sidecar of Users.sql with a run that has both connections, and leaves the environment empty.
    private async Task DescribeEverythingAsync()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Run.Environment.Clear();
    }

    // The first column of a line tells an error from the next, and each line is an error of its own.
    private static void EveryErrorIsOneLine(string error)
    {
        var lines = error.Split('\n');
        lines[^1].ShouldBe("");
        lines[..^1].ShouldAllBe(line => line.Contains(": error SQLSRC", StringComparison.Ordinal));
        lines[..^1].ShouldAllBe(line => !line.StartsWith(' '));
    }

    private async Task EditAQueryAsync() =>
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.WithSecondQueryEdited(SqlTexts.Users),
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task Run_AutoWithNoConnectionAndAQueryToDescribe_SaysItIsOfflineAndChangesNothing()
    {
        var result = await OnlineAsync("auto");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe("");
        result.Out.ShouldStartWith(Offline);
        result.Out.ShouldEndWith(Committed);
        result.Out.Split('\n').Length.ShouldBe(2);
        var databases = result.Out[Offline.Length..^Committed.Length].Split(", ");
        databases.Order().ShouldBe(["billing", "postgres"]);
        File.Exists(Sidecar).ShouldBeFalse();
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The offline line names only the databases that have a query to describe.
    [Fact]
    public async Task Run_AutoOfflineAndOneDatabaseCurrent_NamesTheOtherAlone()
    {
        await DescribeEverythingAsync();
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );

        var result = await OnlineAsync("auto");

        result.ShouldBe(new CliResult(0, Offline + "billing" + Committed, ""));
    }

    // A sidecar that no query needs any more is deleted by a run that may delete, which an offline run does not.
    [Fact]
    public async Task Run_AutoOffline_DeletesNoSidecarAndWritesNone()
    {
        await DescribeEverythingAsync();
        var before = await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken);
        await EditAQueryAsync();
        var plainSidecar = AddAStaleSidecar();

        var result = await OnlineAsync("auto");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldStartWith(Offline);
        (await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(before);
        File.Exists(plainSidecar).ShouldBeTrue();
    }

    [Fact]
    public async Task Run_AutoWithNoConnectionAndEveryEntryCurrent_DeletesTheSidecarNoQueryNeeds()
    {
        await DescribeEverythingAsync();
        var plainSidecar = AddAStaleSidecar();

        var result = await OnlineAsync("auto");

        result.ExitCode.ShouldBe(0);
        File.Exists(plainSidecar).ShouldBeFalse();
    }

    // The sidecar of a file whose queries all resolve to sql.
    private string AddAStaleSidecar()
    {
        var plain = _scene.Project.AddSql("Plain.sql", SqlTexts.Plain);
        var path = SidecarFormat.PathFor(plain);
        File.Copy(Sidecar, path);
        return path;
    }

    [Fact]
    public async Task Run_AutoWithAConnectionForOneDatabase_IsSqlsrc213OnOneLineForTheOther()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;

        var result = await OnlineAsync("auto");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldNotContain("offline");
        result.Error.ShouldStartWith(
            $"{_users}(1,10): error SQLSRC213: No connection is given for the database 'postgres'; "
        );
        result.Error.ShouldContain(
            "; help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES "
                + $"in the environment or in {DotEnvPath}; "
        );
        result.Error.ShouldContain("; see: " + Expected.See("SQLSRC213")[9..]);
        EveryErrorIsOneLine(result.Error);
    }

    [Fact]
    public async Task Run_AutoWithTheBareVariableAndTwoDatabases_IsSqlsrc215AndNotOffline()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Secrets.Connection;

        var result = await OnlineAsync("auto");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldNotContain("offline");
        result.Error.ShouldContain("error SQLSRC215: ");
        EveryErrorIsOneLine(result.Error);
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_TrueWithNoConnection_IsSqlsrc213OnOneLineAndNotOffline()
    {
        var result = await OnlineAsync("true");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldNotContain("offline");
        result.Error.ShouldContain("error SQLSRC213: ");
        EveryErrorIsOneLine(result.Error);
        File.Exists(Sidecar).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_AutoWithNoConnectionAndEveryEntryCurrent_IsANormalRunWithItsSummary()
    {
        await DescribeEverythingAsync();

        var result = await OnlineAsync("auto");

        result.ShouldBe(
            new CliResult(
                0,
                "sqlsource: summary: postgres (postgres): no connection, 1 skipped\n"
                    + "sqlsource: summary: billing (postgres): no connection, 1 skipped\n",
                ""
            )
        );
    }

    // The build's Exec reads a line that starts "error " or "warning " as an error or a warning of the build, and a
    // database may have either name: under --online each line of the summary starts with the prefix.
    [Theory]
    [InlineData("error")]
    [InlineData("warning")]
    [InlineData("Error")]
    public async Task Run_DatabaseNamedAsAnErrorOrAWarning_WritesItsSummaryLineAfterThePrefix(string name)
    {
        _ = _scene.Project.AddSql("Named.sql", $"-- name: GetNamed\n-- database: {name}\nSELECT 3;\n");
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        _scene.Run.Environment[$"SQLSOURCE_CONNECTION_{name.ToUpperInvariant()}"] = Secrets.Connection;

        var result = await OnlineAsync("auto");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe("");
        var lines = result.Out.Split('\n')[..^1];
        lines.Length.ShouldBe(3);
        lines.ShouldAllBe(line => line.StartsWith("sqlsource: summary: ", StringComparison.Ordinal));
        lines.ShouldContain($"sqlsource: summary: {name} (postgres): 1 described, 0 skipped, 0 failed");
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("true")]
    public async Task Run_EveryQueryResolvesToSql_SaysThereIsNothingToDescribe(string mode)
    {
        _ = _scene.Project.AddSql("Plain.sql", SqlTexts.Plain);
        File.Delete(_users);
        var project = new TestProject(_scene.Run.Folder, "Other", "other");
        _ = project.AddSql("Plain.sql", SqlTexts.Plain);
        var manifest = _scene.Run.Folder.WriteFile("obj/Other.csproj.manifest", project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", mode);

        result.ShouldBe(new CliResult(0, NothingToDescribe, ""));
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("true")]
    public async Task Run_NoFileIsClaimed_SaysThereIsNothingToDescribe(string mode)
    {
        var project = new TestProject(_scene.Run.Folder, "Empty", "empty");
        var manifest = _scene.Run.Folder.WriteFile("obj/Empty.csproj.manifest", project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", mode);

        result.ShouldBe(new CliResult(0, NothingToDescribe, ""));
    }

    [Fact]
    public async Task Run_WithoutOnline_NothingToDescribeSaysNothing()
    {
        var project = new TestProject(_scene.Run.Folder, "Empty", "empty");
        var manifest = _scene.Run.Folder.WriteFile("obj/Empty.csproj.manifest", project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.ShouldBe(new CliResult(0, "", ""));
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("true")]
    public async Task Run_ErrorUnderOnline_IsFollowedByNoRunAgainLine(string mode)
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        _scene.Describer.Failures["GetInvoice"] = FakeDescriber.Failure("GetInvoice", "ERROR: no such table");

        var result = await OnlineAsync(mode);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC");
        result.Error.ShouldNotContain("run again");
    }

    [Fact]
    public async Task Run_ErrorWithoutOnline_IsFollowedByTheRunAgainLine()
    {
        var manifest = _scene.Run.Folder.WriteFile("obj/App.csproj.manifest", _scene.Project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.Error.ShouldEndWith(DescribeScene.RunAgain);
    }

    [Fact]
    public async Task Run_LogVariableUnderOnline_OpensNoLog()
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "variable.log";

        var result = await OnlineAsync("auto");

        result.ExitCode.ShouldBe(0);
        File.Exists(_scene.Run.Folder.PathOf("variable.log")).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_LogOptionUnderOnline_WritesTheLog()
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "variable.log";

        var result = await OnlineAsync("auto", "--log", "run.log");

        result.ExitCode.ShouldBe(0);
        LogReader.Names(_scene.Run.Folder.PathOf("run.log"))[0].ShouldBe("run.start");
        File.Exists(_scene.Run.Folder.PathOf("variable.log")).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_LogVariableWithoutOnline_StillOpensTheLog()
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "variable.log";
        var manifest = _scene.Run.Folder.WriteFile("obj/App.csproj.manifest", _scene.Project.ManifestText());

        _ = await _scene.DescribeAsync("--manifest", manifest);

        File.Exists(_scene.Run.Folder.PathOf("variable.log")).ShouldBeTrue();
    }

    [Fact]
    public async Task Run_DatabaseThatCannotBeOpenedUnderOnline_HasHelpThatNamesSqlSourceOnline()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        _scene.Describer.OpenFailure = DescribeFailures.Open(
            "postgres",
            new EquatableArray<string>(["08006: the connection failed"]),
            "check that the database is running"
        );

        var result = await OnlineAsync("true");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC224: ");
        result.Error.ShouldContain(
            "; help: check that the database is running.  To build without the database, remove its connection "
                + "or set SqlSourceOnline to false; see: "
        );
    }

    [Fact]
    public async Task Run_DatabaseThatCannotBeOpenedWithoutOnline_HasTheDescribersHelp()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        _scene.Describer.OpenFailure = DescribeFailures.Open(
            "postgres",
            new EquatableArray<string>(["08006: the connection failed"]),
            "check that the database is running"
        );
        var manifest = _scene.Run.Folder.WriteFile("obj/App.csproj.manifest", _scene.Project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.Error.ShouldContain("    help: check that the database is running\n");
        result.Error.ShouldNotContain("SqlSourceOnline");
    }

    [Fact]
    public async Task Run_ServerErrorWithLineBreaksUnderOnline_IsOneLine()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure(
            "GetUser",
            "ERROR: relation \"users\" does not exist\nDETAIL: gone\r\nHINT: create it"
        );

        var result = await OnlineAsync("true");

        result.ExitCode.ShouldBe(1);
        EveryErrorIsOneLine(result.Error);
        result.Error.ShouldContain("; server: ERROR: relation \"users\" does not exist DETAIL: gone HINT: create it; ");
    }
}
