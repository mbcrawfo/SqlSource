using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// TestProject stands in for a project and for MSBuild's answer about it.  These hold it to the format of a manifest.
public sealed class TestProjectTests
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

    // The package's target writes the settings files after the .sql files and before the C# files.
    [Fact]
    public void ManifestText_ProjectWithASettingsFile_ListsItBetweenTheSqlAndCompileFiles()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder);
        var source = project.AddSource("Queries.cs", "internal static partial class Queries;");
        var sql = project.AddSql("Queries/Users.sql", "SELECT 1;");
        var settings = project.AddSettings("sqlsource.json", "{}");

        var text = project.ManifestText();

        text.ShouldContain($"File={sql}\nSettings={settings}\nCompile={source}\n");
        project.Manifest().SettingsFiles.ShouldBe([settings]);
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
        // Added after the runner was told of the project: the run sees them only when the manifest is written as
        // the target runs.
        _ = project.AddSource("Queries.cs", Plans.Type);
        var sql = project.AddSql("Broken.sql", "-- name: One\nSELECT 'open\n");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{sql}(2,8): error SQLSRC101: ");
    }
}
