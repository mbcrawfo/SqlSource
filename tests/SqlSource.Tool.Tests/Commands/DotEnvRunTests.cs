using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// A run takes the connection of a database from the .env file of its unit's folder, or the one --env-file names, when
// the environment has none.
public sealed class DotEnvRunTests : IDisposable
{
    private const string Summary = "billing (postgres): 1 described, 0 skipped, 0 failed\n";

    private readonly DescribeScene _scene = new();
    private readonly string _invoices;

    public DotEnvRunTests()
    {
        _scene.Run.Environment.Clear();
        _invoices = _scene.Project.AddSql("Invoices.sql", SqlTexts.GetInvoice);
    }

    public void Dispose() => _scene.Dispose();

    // The help of SQLSRC213 for "billing", up to what follows the file it names.
    private static string NoConnectionHelp(string file) =>
        "    help: pass --connection billing=<connection string>, or set SQLSOURCE_CONNECTION_BILLING in the "
        + $"environment or in {file}";

    // The same, with the sentence that says the run reads that file alone, as when no other .env was found.
    private static string NoConnectionHelpReadingAlone(string file) =>
        NoConnectionHelp(file)
        + ".  The run reads that file alone: --env-file, or SqlSourceEnvFile in a build, names another";

    [Fact]
    public async Task Run_ValueOnlyInTheDotEnvFile_DescribesAndExitsZero()
    {
        _ = _scene.WriteDotEnv(DescribeScene.BillingFromTheFile);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, Summary, ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=from-the-file");
    }

    [Fact]
    public async Task Run_ValueInTheEnvironmentAndTheDotEnvFile_UsesTheEnvironments()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = "Host=from-the-environment";
        _ = _scene.WriteDotEnv(DescribeScene.BillingFromTheFile);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, Summary, ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=from-the-environment");
    }

    // The variable of a database whose name has a letter outside ASCII is a key of the file.
    [Fact]
    public async Task Run_DatabaseWithALetterOutsideAscii_TakesItsConnectionFromTheDotEnvFile()
    {
        _ = _scene.Project.AddSql("Donnees.sql", "-- name: GetDonnee\n-- database: données\nSELECT 3;\n");
        _ = _scene.WriteDotEnv("SQLSOURCE_CONNECTION_DONNÉES=Host=from-the-file\n");

        var result = await _scene.DescribeAsync("--database", "données");

        result.ShouldBe(new CliResult(0, "données (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=from-the-file");
    }

    // No value of a file with an error is used, so the database has no connection.
    [Fact]
    public async Task Run_DotEnvLineOfTheToolThatCannotBeRead_IsSqlsrc286AndTheDatabaseHasNoConnection()
    {
        _ = _scene.Run.Folder.WriteFile(
            ".env",
            "SQLSOURCE_CONNECTION_BILLING=Host=db\nSQLSOURCE_CONNECTION_REPORTS=\"Host=db\n"
        );

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "billing (postgres): 0 described, 0 skipped, 1 failed\n",
                $"{_scene.DotEnvPath}(2,1): error SQLSRC286: The line that sets 'SQLSOURCE_CONNECTION_REPORTS' "
                    + "cannot be read: a quote is not closed\n"
                    + Expected.See("SQLSRC286")
                    + $"{_invoices}(1,10): error SQLSRC213: No connection is given for the database 'billing'\n"
                    + NoConnectionHelpReadingAlone(_scene.DotEnvPath)
                    + "\n"
                    + Expected.See("SQLSRC213")
                    + DescribeScene.RunAgain
            )
        );
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_DotEnvFileThatCannotBeRead_IsSqlsrc285()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = "Host=db";
        _scene.Run.ReadTextFile = path =>
            Path.GetFileName(path) == ".env"
                ? throw new IOException("the disk is gone")
                : Hosts.ReadUnderTheTemporaryFolder(path);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                Summary,
                $"{_scene.DotEnvPath} : error SQLSRC285: '{_scene.DotEnvPath}' cannot be read: the disk is gone\n"
                    + Expected.See("SQLSRC285")
                    + DescribeScene.RunAgain
            )
        );
    }

    // The file is shared with other programs: what is not the tool's is none of its business.
    [Fact]
    public async Task Run_DotEnvFileOfOtherKeysAlone_SaysNothing()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = "Host=db";
        _ = _scene.WriteDotEnv("DATABASE_URL=\"unclosed\nexport OTHER=1\nnot a line at all\n");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, Summary, ""));
    }

    [Fact]
    public async Task Run_NoConnectionAndNoDotEnvFile_HelpSaysTheFileOfTheUnitFolder()
    {
        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("\n" + NoConnectionHelpReadingAlone(_scene.DotEnvPath) + "\n");
    }

    // The unit's own .env is read and has no value for the database: the run still says it reads that file alone.
    [Fact]
    public async Task Run_UnitsDotEnvFileWithoutTheVariable_HelpSaysTheRunReadsThatFileAlone()
    {
        _ = _scene.WriteDotEnv("SQLSOURCE_CONNECTION_REPORTS=Host=db\n");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("\n" + NoConnectionHelpReadingAlone(_scene.DotEnvPath) + "\n");
    }

    // A .env above the unit's folder is not read, though the folder above is the root of the repository.
    [Fact]
    public async Task Run_NoConnectionAndADotEnvFileAboveTheUnit_HelpNamesTheUnitsFile()
    {
        var project = Plans.Postgres(_scene.Run.Folder, directory: "repo/app").AnsweredBy(_scene.Run.Processes);
        var invoices = project.AddSql("Invoices.sql", SqlTexts.GetInvoice);
        _ = _scene.Run.Folder.CreateFolder("repo/.git");
        _ = _scene.Run.Folder.WriteFile("repo/.env", "SQLSOURCE_CONNECTION_BILLING=Host=db\n");

        var result = await _scene.DescribeAsync(project.ProjectPath);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{invoices}(1,10): error SQLSRC213: No connection is given for the database 'billing'\n"
                + NoConnectionHelpReadingAlone(_scene.Run.Folder.PathOf("repo/app/.env"))
                + "\n"
        );
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // A solution's run reads the .env of the solution's folder, not one of a project's own.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Run_SolutionWithDotEnvFilesInItsProjectFolders_HelpNamesTheFilesNotRead(int count)
    {
        var names = Enumerable.Range(1, count).Select(index => $"Lib{index}").ToArray();
        foreach (var name in names)
        {
            var project = Plans.Postgres(_scene.Run.Folder, name, name).AnsweredBy(_scene.Run.Processes);
            _ = project.AddSql("Invoices.sql", SqlTexts.GetInvoice);
            _ = _scene.Run.Folder.WriteFile($"{name}/.env", "SQLSOURCE_CONNECTION_BILLING=Host=db\n");
        }

        var projects = string.Concat(names.Select(name => $"<Project Path=\"{name}/{name}.csproj\" />"));
        _ = _scene.Run.Folder.WriteFile("All.slnx", $"<Solution>{projects}</Solution>");
        var notRead = names.Select(name => _scene.Run.Folder.PathOf($"{name}/.env")).ToArray();
        var listed = count switch
        {
            1 => notRead[0],
            2 => $"{notRead[0]} and {notRead[1]}",
            _ => $"{notRead[0]}, {notRead[1]} and {notRead[2]}",
        };

        var result = await _scene.DescribeAsync("All.slnx");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(
            "\n"
                + NoConnectionHelp(_scene.DotEnvPath)
                + $".  The run did not read {listed}: it reads {_scene.DotEnvPath} alone, and --env-file, or "
                + "SqlSourceEnvFile in a build, names another\n"
        );
        result.Error.ShouldNotContain("reads that file alone");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The help of a database names the files of the projects that have its queries, not those of other databases.
    [Fact]
    public async Task Run_SolutionWithProjectsOfTwoDatabases_HelpNamesOnlyTheFilesOfTheDatabasesOwnProjects()
    {
        var billing = Plans.Postgres(_scene.Run.Folder, "Billing", "Billing").AnsweredBy(_scene.Run.Processes);
        _ = billing.AddSql("Invoices.sql", SqlTexts.GetInvoice);
        var reports = Plans.Postgres(_scene.Run.Folder, "Reports", "Reports").AnsweredBy(_scene.Run.Processes);
        _ = reports.AddSql("Totals.sql", "-- name: GetTotal\n-- database: reports\nSELECT 4;\n");
        _ = _scene.Run.Folder.WriteFile("Billing/.env", "SQLSOURCE_CONNECTION_BILLING=Host=db\n");
        var reportsEnv = _scene.Run.Folder.WriteFile("Reports/.env", "SQLSOURCE_CONNECTION_REPORTS=Host=db\n");
        var projects = "<Project Path=\"Billing/Billing.csproj\" /><Project Path=\"Reports/Reports.csproj\" />";
        _ = _scene.Run.Folder.WriteFile("All.slnx", $"<Solution>{projects}</Solution>");
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = "Host=from-the-environment";

        var result = await _scene.DescribeAsync("All.slnx");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(
            "    help: pass --connection reports=<connection string>, or set SQLSOURCE_CONNECTION_REPORTS in the "
                + $"environment or in {_scene.DotEnvPath}.  The run did not read {reportsEnv}: it reads "
                + $"{_scene.DotEnvPath} alone, and --env-file, or SqlSourceEnvFile in a build, names another\n"
        );
        result.Error.ShouldNotContain(_scene.Run.Folder.PathOf("Billing/.env"));
        result.Error.ShouldNotContain("reads that file alone");
    }

    [Fact]
    public async Task Run_EnvFileNamed_ReadsItAndNotTheUnitsOwn()
    {
        _ = _scene.WriteDotEnv("SQLSOURCE_CONNECTION_BILLING=Host=wrong\n");
        var named = _scene.Run.Folder.WriteFile("other/.env.local", "SQLSOURCE_CONNECTION_BILLING=Host=right\n");

        var result = await _scene.DescribeAsync("--env-file", "other/.env.local", "--verbose");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        result.Out.ShouldContain(
            $"sqlsource: database 'billing' (postgres): connection from SQLSOURCE_CONNECTION_BILLING in {named}\n"
        );
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=right");
    }

    // A relative path is from the folder the tool runs in, not from the unit's.
    [Fact]
    public async Task Run_EnvFileRelative_IsFromTheWorkingFolder()
    {
        var project = Plans.Postgres(_scene.Run.Folder, directory: "app").AnsweredBy(_scene.Run.Processes);
        _ = project.AddSql("Invoices.sql", SqlTexts.GetInvoice);
        _ = _scene.Run.Folder.WriteFile("app/shared.env", "SQLSOURCE_CONNECTION_BILLING=Host=wrong\n");
        _ = _scene.Run.Folder.WriteFile("shared.env", "SQLSOURCE_CONNECTION_BILLING=Host=right\n");

        var result = await _scene.DescribeAsync(project.ProjectPath, "--env-file=shared.env");

        result.ShouldBe(new CliResult(0, Summary, ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=right");
    }

    // "none" is matched ignoring case and the white space around it.
    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    [InlineData(" None ")]
    public async Task Run_EnvFileNone_ReadsNoFileAndHelpSaysSo(string none)
    {
        _ = _scene.WriteDotEnv("SQLSOURCE_CONNECTION_BILLING=Host=db\n");

        var result = await _scene.DescribeAsync("--env-file", none);

        result.ShouldBe(
            new CliResult(
                1,
                "billing (postgres): 0 described, 0 skipped, 1 failed\n",
                $"{_invoices}(1,10): error SQLSRC213: No connection is given for the database 'billing'\n"
                    + "    help: pass --connection billing=<connection string>, or set SQLSOURCE_CONNECTION_BILLING in "
                    + "the environment.  The run reads no .env file\n"
                    + Expected.See("SQLSRC213")
                    + DescribeScene.RunAgain
            )
        );
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The .env files of a solution's projects are named, without saying again that the run reads none.
    [Fact]
    public async Task Run_EnvFileNoneInASolutionWithDotEnvFilesInItsProjectFolders_HelpNamesTheFilesNotRead()
    {
        var project = Plans.Postgres(_scene.Run.Folder, "Lib", "Lib").AnsweredBy(_scene.Run.Processes);
        _ = project.AddSql("Invoices.sql", SqlTexts.GetInvoice);
        var notRead = _scene.Run.Folder.WriteFile("Lib/.env", "SQLSOURCE_CONNECTION_BILLING=Host=db\n");
        _ = _scene.Run.Folder.WriteFile("All.slnx", "<Solution><Project Path=\"Lib/Lib.csproj\" /></Solution>");

        var result = await _scene.DescribeAsync("All.slnx", "--env-file", "none");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(
            "    help: pass --connection billing=<connection string>, or set SQLSOURCE_CONNECTION_BILLING in the "
                + $"environment.  The run reads no .env file.  The run did not read {notRead}: --env-file, or "
                + "SqlSourceEnvFile in a build, names a file to read\n"
        );
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // A file that is not there is no file, as a missing .env is: the help names it, so a misspelt path is seen.
    [Fact]
    public async Task Run_EnvFileMissing_IsNoErrorAndHelpNamesIt()
    {
        var result = await _scene.DescribeAsync("--env-file", "missing/.env");

        result.ShouldBe(
            new CliResult(
                1,
                "billing (postgres): 0 described, 0 skipped, 1 failed\n",
                $"{_invoices}(1,10): error SQLSRC213: No connection is given for the database 'billing'\n"
                    + NoConnectionHelpReadingAlone(_scene.Run.Folder.PathOf("missing/.env"))
                    + "\n"
                    + Expected.See("SQLSRC213")
                    + DescribeScene.RunAgain
            )
        );
    }

    [Fact]
    public async Task Run_EnvFileDotSlashNone_ReadsTheFileNamedNone()
    {
        _ = _scene.Run.Folder.WriteFile("none", "SQLSOURCE_CONNECTION_BILLING=Host=from-none\n");

        var result = await _scene.DescribeAsync("--env-file", "./none");

        result.ShouldBe(new CliResult(0, Summary, ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=from-none");
    }

    // The tool gives "dotnet msbuild" no variable of its own that holds a connection.
    [Fact]
    public async Task Run_ValueInTheDotEnvFile_IsInNoVariableOfMsbuild()
    {
        _ = _scene.WriteDotEnv("SQLSOURCE_CONNECTION_BILLING=" + Secrets.ConnectionWithMarker + "\n");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        _scene.Run.Processes.Requests.ShouldNotBeEmpty();
        _scene.Run.Processes.Requests.ShouldAllBe(request =>
            request.SetVariables.Values.All(value => !value.Contains(Secrets.Marker, StringComparison.Ordinal))
        );
    }
}
