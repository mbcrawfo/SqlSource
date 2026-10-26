using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Chains;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

// The settings files, sqlsource.json, as the plan reads them: a file's problems are reported once in a run, at its line
// and column, when the chain of the folder of some .sql file or C# file of a project reaches it.  Of the values that
// are not valid, the tool reports those of the settings it reads, dialect, output and database.
public sealed class RunPlannerSettingsFileTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Plan_SettingsFileWithErrorsOfTheFile_ReportsEachAtItsLineAndColumn()
    {
        var project = Plans.Postgres(_folder);
        var settings = project.AddSettings(
            "sqlsource.json",
            /*lang=json*/
            "{\n  \"Dialect\": \"postgres\",\n  \"output\": 1,\n  \"output\": \"sql\",\n}"
        );

        var result = Plans.Result(project);

        result
            .Errors.Select(Reported.Of)
            .ShouldBe([
                new Reported(
                    "SQLSRC016",
                    settings,
                    new LinePosition(1, 2),
                    "'Dialect' is not a key of a settings file"
                ),
                new Reported("SQLSRC018", settings, new LinePosition(2, 12), "'output' takes a string"),
                new Reported(
                    "SQLSRC017",
                    settings,
                    new LinePosition(3, 2),
                    "'output' is written twice in this settings file, and the first applies"
                ),
            ]);
    }

    [Fact]
    public void Plan_SettingsFileThatIsNotJson_IsSqlsrc015()
    {
        var project = Plans.Postgres(_folder);
        var settings = project.AddSettings("sqlsource.json", "{ \"output\": ");

        var result = Plans.Result(project);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Descriptor.ShouldBe(SqlDiagnostics.SettingsFileNotAnObject);
        error.Path.ShouldBe(settings);
    }

    [Fact]
    public void Plan_InvalidValuesOfSettingsTheToolReads_AreReported()
    {
        var project = Plans.Postgres(_folder);
        var settings = project.AddSettings(
            "sqlsource.json",
            /*lang=json,strict*/
            "{\n  \"dialect\": \"pgsql\",\n  \"output\": \"model\",\n  \"database\": \"a b\"\n}"
        );

        var result = Plans.Result(project);

        result
            .Errors.Select(Reported.Of)
            .ShouldBe([
                new Reported(
                    "SQLSRC019",
                    settings,
                    new LinePosition(1, 13),
                    "'pgsql' is not a valid value of dialect.  'dialect' takes " + SqlDialectName.Accepted + "."
                ),
                new Reported("SQLSRC019", settings, new LinePosition(2, 12), "'model' is not a valid value of output"),
                new Reported("SQLSRC019", settings, new LinePosition(3, 14), "'a b' is not a valid value of database"),
            ]);
    }

    // The build reports these: a value of each setting that is not valid, and a type override's key that repeats
    // another.
    [Fact]
    public void Plan_InvalidValuesOfSettingsTheToolDoesNotRead_AreNotReported()
    {
        var project = Plans.Postgres(_folder);
        var settings = project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            """
            {
                "typeOverrides": { "[dbo].[Email]": "X", "integer": "long", "int4": "int" },
                "columnTypes": { "id": "X" },
                "inputModelSuffix": "-"
            }
            """
        );

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        // A file in which the build found nothing would prove nothing.
        ListedSettingsFile
            .Read(new SqlFileText(settings), SettingsFileSide.Build, TestContext.Current.CancellationToken)
            .ShouldNotBeNull()
            .Diagnostics.Select(diagnostic => diagnostic.Descriptor.Id)
            .ShouldBe(["SQLSRC019", "SQLSRC020", "SQLSRC019", "SQLSRC019"]);
    }

    [Fact]
    public void Plan_SettingsFileAboveTheRootFile_IsNeverReported()
    {
        var project = Plans.Postgres(_folder);
        project.ListSettings(_folder.WriteFile("sqlsource.json", "{ nope"));
        _ = project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            """{"root": true}"""
        );

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_SettingsFileReachedOnlyByACSharpFile_IsReported()
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSource("Code/Repository.cs", "internal sealed class Repository;");
        var settings = project.AddSettings(
            "Code/sqlsource.json", /*lang=json,strict*/
            """{"unknown": 1}"""
        );

        var result = Plans.Result(project);

        result
            .Errors.Select(Reported.Of)
            .ShouldBe([
                new Reported(
                    "SQLSRC016",
                    settings,
                    new LinePosition(0, 1),
                    "'unknown' is not a key of a settings file"
                ),
            ]);
    }

    [Fact]
    public void Plan_SettingsFileReachedOnlyByAnUnclaimedSqlFile_IsReported()
    {
        var project = new TestProject(_folder);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"Queries\""));
        _ = project.AddSql("Other/X.sql", "SELECT 1;");
        var settings = project.AddSettings(
            "Other/sqlsource.json", /*lang=json,strict*/
            """{"unknown": 1}"""
        );

        var result = Plans.Result(project);

        result
            .Errors.Select(Reported.Of)
            .ShouldBe([
                new Reported(
                    "SQLSRC016",
                    settings,
                    new LinePosition(0, 1),
                    "'unknown' is not a key of a settings file"
                ),
            ]);
        result.Plan.Files.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_SettingsFileThatNoFileReaches_IsNotReported()
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSettings("Unused/sqlsource.json", "{ nope");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
    }

    // A file that is not JSON says nothing, so it does not end the chain.
    [Fact]
    public void Plan_ChainThroughAFileThatIsNotJson_ReachesTheFartherFile()
    {
        var project = Plans.Postgres(_folder);
        var farther = project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            """{"unknown": 1}"""
        );
        var nearer = project.AddSettings("Queries/sqlsource.json", "{ nope");
        _ = project.AddSql("Queries/X.sql", "SELECT 1;");

        var result = Plans.Result(project);

        result
            .Errors.Select(error => (error.Descriptor.Id, error.Path))
            .ShouldBe([("SQLSRC016", farther), ("SQLSRC015", nearer)]);
    }

    [Fact]
    public void Plan_CompileFileUnderABuildFolder_ReachesNothing()
    {
        var project = Plans.Postgres(_folder);
        project.Properties["SqlSourceBuildFolders"] = "|" + project.PathOf("obj") + Path.DirectorySeparatorChar;
        _ = project.AddSource("obj/Gen.cs", "internal sealed class Gen;");
        _ = project.AddSettings(
            "obj/sqlsource.json", /*lang=json,strict*/
            """{"unknown": 1}"""
        );

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_SettingsFileOfTwoProjects_IsReportedOnce()
    {
        var shared = _folder.WriteFile(
            "sqlsource.json", /*lang=json,strict*/
            """{"unknown": 1}"""
        );
        var app = Plans.Postgres(_folder);
        _ = app.AddSql("One.sql", "SELECT 1;");
        app.ListSettings(shared);
        var lib = Plans.Postgres(_folder, "Lib");
        _ = lib.AddSql("Two.sql", "SELECT 2;");
        lib.ListSettings(shared);

        var result = Plans.Result(app, lib);

        result
            .Errors.Select(Reported.Of)
            .ShouldBe([
                new Reported("SQLSRC016", shared, new LinePosition(0, 1), "'unknown' is not a key of a settings file"),
            ]);
    }

    [Fact]
    public void Plan_SettingsFileNamedInAnotherCase_IsNotRead()
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSettings("SqlSource.json", "{ nope");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
    }

    // The generator reads a file it cannot read as empty, which is not JSON.
    [Fact]
    public void Plan_SettingsFileThatIsGone_IsSqlsrc015()
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSql("One.sql", "SELECT 1;");
        var gone = project.PathOf("sqlsource.json");
        project.ListSettings(gone);

        var result = Plans.Result(project);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Descriptor.ShouldBe(SqlDiagnostics.SettingsFileNotAnObject);
        error.Path.ShouldBe(gone);
    }

    // The settings files come in the order of their folders, ignoring case, after the values of the project.
    [Fact]
    public void Plan_ErrorsOfAProject_AreInvalidValuesThenSettingsFiles()
    {
        var project = Plans.Postgres(_folder);
        project.Properties["SqlSourceOutput"] = "nothing";
        var lower = project.AddSettings(
            "b/sqlsource.json", /*lang=json,strict*/
            """{"lower": 1}"""
        );
        var upper = project.AddSettings(
            "A/sqlsource.json", /*lang=json,strict*/
            """{"upper": 1}"""
        );
        _ = project.AddSql("b/X.sql", "SELECT 1;");
        _ = project.AddSql("A/X.sql", "SELECT 1;");

        var result = Plans.Result(project);

        result
            .Errors.Select(Reported.Of)
            .ShouldBe([
                Reported.Of(
                    ToolDiagnostic.AtFile(
                        SqlDiagnostics.InvalidSettingValue,
                        project.ProjectPath,
                        "nothing",
                        "SqlSourceOutput",
                        ""
                    )
                ),
                new Reported("SQLSRC016", upper, new LinePosition(0, 1), "'upper' is not a key of a settings file"),
                new Reported("SQLSRC016", lower, new LinePosition(0, 1), "'lower' is not a key of a settings file"),
            ]);
    }
}
