using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
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
        var project = new TestProject(folder) { LangVersion = "12.0", BuildFolders = "|/work/obj/" };
        project.Constants.AddRange(["DEBUG", "NET10_0"]);
        project.Settings["dialect"] = "postgres";
        var source = project.AddSource("Queries.cs", "internal static partial class Queries;");
        var sql = project.AddSql("Queries/Users.sql", "SELECT 1;");
        var settings = project.AddSettings("Queries/sqlsource.json", "{}");

        var manifest = project.Manifest();

        manifest.ProjectPath.ShouldBe(folder.PathOf("App/App.csproj"));
        manifest.LangVersion.ShouldBe("12.0");
        manifest.DefineConstants.ShouldBe(["DEBUG", "NET10_0"]);
        manifest.BuildFolders.ShouldBe("|/work/obj/");
        manifest.Files.ShouldBe([sql]);
        manifest.SettingsFiles.ShouldBe([project.SettingsPath, settings]);
        manifest.CompileFiles.ShouldBe([source]);
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

    // The project's own settings file is in the folder of the project file, and the tool reads it.
    [Fact]
    public void Manifest_ProjectWithSettings_WritesThemBesideTheProjectFile()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder);
        _ = project.AddSource("Queries.cs", Plans.Type);
        _ = project.AddSql("Q.sql", "SELECT 1;");
        project.Settings["dialect"] = "mssql";

        var file = Plans.Of(project).Files.ShouldHaveSingleItem();

        project.SettingsPath.ShouldBe(folder.PathOf("App/sqlsource.json"));
        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(project.SettingsPath)), project.Settings).ShouldBeTrue();
        file.Dialect.ShouldBe(SqlDialect.SqlServer);
    }

    // As the build lists the files of the farther folders first.
    [Fact]
    public void Manifest_ProjectWithSettingsAndASettingsFileOfAFolder_ListsTheProjectsFileFirst()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder);
        var nearer = project.AddSettings("Queries/sqlsource.json", "{}");
        project.Settings["output"] = "Sql";

        project.Manifest().SettingsFiles.ShouldBe([project.SettingsPath, nearer]);
    }

    [Fact]
    public void ManifestText_SettingsWithNoKey_ListsAndWritesNoSettingsFile()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder);

        project.ManifestText().ShouldNotContain("Settings=");
        File.Exists(project.SettingsPath).ShouldBeFalse();
    }

    // The file is written as the manifest is, so a run answered before a test changes the settings reads the change.
    [Fact]
    public void ManifestText_SettingsChangedAfterAManifest_WritesTheChange()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder);
        project.Settings["dialect"] = "postgres";
        _ = project.ManifestText();
        project.Settings["dialect"] = "ansi";

        _ = project.ManifestText();

        JsonNode.Parse(File.ReadAllText(project.SettingsPath))!["dialect"]!.GetValue<string>().ShouldBe("ansi");
    }

    [Fact]
    public void AddSettings_AtTheProjectsSettingsFileWhileSettingsHasAKey_IsRefused()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder);
        project.Settings["dialect"] = "postgres";

        Should
            .Throw<InvalidOperationException>(() => project.AddSettings("sqlsource.json", "{}"))
            .Message.ShouldContain("Settings");
    }

    // A file the test added there before it gave the settings a key is refused when the manifest is written.
    [Fact]
    public void ManifestText_SettingsWithAKeyBesideAFileAtTheirPath_IsRefused()
    {
        using var folder = new TempFolder();
        var project = new TestProject(folder);
        _ = project.AddSettings("sqlsource.json", "{}");
        project.Settings["dialect"] = "postgres";

        Should.Throw<InvalidOperationException>(project.ManifestText).Message.ShouldContain("Settings");
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
