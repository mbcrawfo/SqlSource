using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// TestProject stands in for a project and for MSBuild's answer about it.  These hold it to the format of a manifest.
public class TestProjectTests
{
    [Fact]
    public void Manifest_ProjectWithFilesAndSettings_IsReadByTheToolsReader()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder) { LangVersion = "12.0" };
        project.Constants.AddRange(["DEBUG", "NET10_0"]);
        project.Properties["SqlSourceDialect"] = "postgres";
        var source = project.AddSource("Queries.cs", "internal static partial class Queries;");
        var sql = project.AddSql("Queries/Users.sql", "SELECT 1;", ("SqlSourceDatabase", "billing"));

        var manifest = project.Manifest();

        manifest.ProjectPath.ShouldBe(folder.PathOf("App/App.csproj"));
        manifest.LangVersion.ShouldBe("12.0");
        manifest.DefineConstants.ShouldBe(["DEBUG", "NET10_0"]);
        manifest.Properties["SqlSourceDialect"].ShouldBe("postgres");
        manifest.CompileFiles.ShouldBe([source]);
        var file = manifest.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(sql);
        file.Metadata["SqlSourceDatabase"].ShouldBe("billing");
    }

    [Fact]
    public void ProjectPath_EmptyDirectory_IsInTheFolderItself()
    {
        using var folder = new TempFolder();

        new TestProject(folder, directory: "").ProjectPath.ShouldBe(folder.PathOf("App.csproj"));
    }

    [Fact]
    public async Task AnsweredBy_Runner_GivesTheRunTheManifestAsItIsWhenAsked()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        // Added after the runner was told of the project.
        _ = project.AddSql("Queries/Users.sql", "SELECT 1;");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
    }
}
