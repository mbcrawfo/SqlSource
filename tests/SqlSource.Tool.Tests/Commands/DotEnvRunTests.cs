using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// A run takes the connection of a database from the .env file nearest to its unit when the environment has none.
public sealed class DotEnvRunTests : IDisposable
{
    private const string Invoices = "-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private const string Summary = "billing (postgres): 1 described, 0 skipped, 0 failed\n";

    private readonly DescribeScene _scene = new();
    private readonly string _invoices;

    public DotEnvRunTests()
    {
        _scene.Run.Environment.Clear();
        _invoices = _scene.Project.AddSql("Invoices.sql", Invoices);
    }

    public void Dispose() => _scene.Dispose();

    private string DotEnvPath => _scene.Run.Folder.PathOf(".env");

    // The help of SQLSRC213 for "billing", up to what follows the file it names.
    private static string NoConnectionHelp(string file) =>
        "    help: pass --connection billing=<connection string>, or set SQLSOURCE_CONNECTION_BILLING in the "
        + $"environment or in {file}";

    [Fact]
    public async Task Run_ValueOnlyInTheDotEnvFile_DescribesAndExitsZero()
    {
        _ = _scene.Run.Folder.WriteFile(".env", "SQLSOURCE_CONNECTION_BILLING=Host=from-the-file\n");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, Summary, ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=from-the-file");
    }

    [Fact]
    public async Task Run_ValueInTheEnvironmentAndTheDotEnvFile_UsesTheEnvironments()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = "Host=from-the-environment";
        _ = _scene.Run.Folder.WriteFile(".env", "SQLSOURCE_CONNECTION_BILLING=Host=from-the-file\n");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, Summary, ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=from-the-environment");
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
                $"{DotEnvPath}(2,1): error SQLSRC286: The line that sets 'SQLSOURCE_CONNECTION_REPORTS' cannot be "
                    + "read: a quote is not closed\n"
                    + Expected.See("SQLSRC286")
                    + $"{_invoices}(1,10): error SQLSRC213: No connection is given for the database 'billing'\n"
                    + NoConnectionHelp(DotEnvPath)
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
                $"{DotEnvPath} : error SQLSRC285: '{DotEnvPath}' cannot be read: the disk is gone\n"
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
        _ = _scene.Run.Folder.WriteFile(".env", "DATABASE_URL=\"unclosed\nexport OTHER=1\nnot a line at all\n");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, Summary, ""));
    }

    [Fact]
    public async Task Run_NoConnectionAndNoDotEnvFile_HelpSaysTheFileOfTheUnitFolder()
    {
        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("\n" + NoConnectionHelp(DotEnvPath) + "\n");
    }

    // The file read is the nearest to the unit, up to the root of the repository.
    [Fact]
    public async Task Run_NoConnectionAndADotEnvFileAboveTheUnit_HelpNamesTheFileRead()
    {
        var project = Plans.Postgres(_scene.Run.Folder, directory: "repo/app").AnsweredBy(_scene.Run.Processes);
        var invoices = project.AddSql("Invoices.sql", Invoices);
        _ = _scene.Run.Folder.CreateFolder("repo/.git");
        var read = _scene.Run.Folder.WriteFile("repo/.env", "OTHER=1\n");

        var result = await _scene.DescribeAsync(project.ProjectPath);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{invoices}(1,10): error SQLSRC213: No connection is given for the database 'billing'\n"
                + NoConnectionHelp(read)
                + "\n"
        );
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
            _ = project.AddSql("Invoices.sql", Invoices);
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
                + NoConnectionHelp(DotEnvPath)
                + $".  The run did not read {listed}: it reads the .env nearest to {_scene.Run.Folder.Path}\n"
        );
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The tool gives "dotnet msbuild" no variable of its own that holds a connection.
    [Fact]
    public async Task Run_ValueInTheDotEnvFile_IsInNoVariableOfMsbuild()
    {
        _ = _scene.Run.Folder.WriteFile(".env", "SQLSOURCE_CONNECTION_BILLING=" + Secrets.ConnectionWithMarker + "\n");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        _scene.Run.Processes.Requests.ShouldNotBeEmpty();
        _scene.Run.Processes.Requests.ShouldAllBe(request =>
            request.SetVariables.Values.All(value => !value.Contains(Secrets.Marker, StringComparison.Ordinal))
        );
    }
}
