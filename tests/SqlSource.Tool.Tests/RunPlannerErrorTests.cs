using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// What is wrong with a plan: each row of the spec's table, with its id, its place and what it does to the plan.
public sealed class RunPlannerErrorTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static RunPlanResult Plan(params TestProject[] projects) =>
        RunPlanner.Plan([.. projects.Select(project => project.Manifest())], TestContext.Current.CancellationToken);

    private static string Type(string name, string arguments = "") =>
        $"[SqlSource.SqlSourceGenerate({arguments})]\ninternal static partial class {name};\n";

    // A project whose dialect can be described, with one type that claims the folder "Q".
    private TestProject Project(string name = "App", string dialect = "postgres")
    {
        var project = new TestProject(_folder, name);
        project.Properties["SqlSourceDialect"] = dialect;
        _ = project.AddSource("Queries.cs", Type("Queries", "Path = \"Q\""));
        return project;
    }

    [Fact]
    public void Plan_FileWithAParseError_ReportsItWhereTheGeneratorDoesAndPlansNoQuery()
    {
        var project = Project();
        var sql = project.AddSql("Q/Broken.sql", "-- name: One\nSELECT 'open\n");

        var result = Plan(project);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Descriptor.Id.ShouldBe("SQLSRC101");
        error.Path.ShouldBe(sql);
        error.Position.ShouldBe(new LinePosition(1, 7));
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.HasParseErrors);
        file.Queries.Count.ShouldBe(0);
    }

    // The generator parses a file it cannot read as empty.
    [Fact]
    public void Plan_ClaimedFileThatIsNotOnTheDisk_IsReportedAsAFileWithoutSql()
    {
        var project = Project();
        var gone = project.PathOf("Q/Gone.sql");
        project.ListSql(gone);

        var result = Plan(project);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Descriptor.Id.ShouldStartWith("SQLSRC1");
        error.Path.ShouldBe(gone);
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.HasParseErrors);
    }

    [Fact]
    public void Plan_PropertyThatIsNoDialect_IsSqlsrc011AtTheProjectAndTheFileIsReadAsAnsi()
    {
        var project = Project(dialect: "nosuch");
        // Nothing needs an entry, so that the dialect is all that is wrong.
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldBe([ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "nosuch")]);
        result.Plan.Files.ShouldHaveSingleItem().Dialect.ShouldBe(SqlDialect.Ansi);
    }

    [Fact]
    public void Plan_MetadataThatIsNoDialect_IsSqlsrc011OnceForEachValueOfAClaimedFile()
    {
        var project = Project();
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSql("Q/A.sql", "SELECT 1;", ("SqlSourceDialect", "bad-one"));
        _ = project.AddSql("Q/B.sql", "SELECT 1;", ("SqlSourceDialect", "bad-one"));
        _ = project.AddSql("Q/C.sql", "SELECT 1;", ("SqlSourceDialect", "another"));
        _ = project.AddSql("Unclaimed/D.sql", "SELECT 1;", ("SqlSourceDialect", "never-read"));

        var result = Plan(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "another"),
            ToolDiagnostic.ForFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "bad-one"),
        ]);
        result.Plan.Files.Select(file => file.Dialect).ShouldAllBe(dialect => dialect == SqlDialect.Ansi);
    }

    [Fact]
    public void Plan_OutputAndDatabaseThatAreNotValid_AreSqlsrc014AtTheProjectAndAreNotSet()
    {
        var project = Project();
        project.Properties["SqlSourceOutput"] = "everything";
        project.Properties["SqlSourceDatabase"] = "not a name";
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.ForFile(
                SqlDiagnostics.InvalidSettingValue,
                project.ProjectPath,
                "not a name",
                "SqlSourceDatabase"
            ),
            ToolDiagnostic.ForFile(
                SqlDiagnostics.InvalidSettingValue,
                project.ProjectPath,
                "everything",
                "SqlSourceOutput"
            ),
        ]);
        var query = result.Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem();
        query.NeedsEntry.ShouldBeTrue();
        query.Database.ShouldBe("postgres");
    }

    [Fact]
    public void Plan_MetadataThatIsNotValid_IsSqlsrc014ForAClaimedFileAlone()
    {
        var project = Project();
        _ = project.AddSql("Q/One.sql", "SELECT 1;", ("SqlSourceOutput", "bad"), ("SqlSourceDatabase", "a b"));
        _ = project.AddSql("Unclaimed/Two.sql", "SELECT 1;", ("SqlSourceOutput", "never-read"));

        Plan(project)
            .Errors.Select(error => (error.Descriptor.Id, error.Arguments[0], error.Arguments[1]))
            .ShouldBe([("SQLSRC014", "a b", "SqlSourceDatabase"), ("SQLSRC014", "bad", "SqlSourceOutput")]);
    }

    // The build reports the settings that the tool does not read.
    [Fact]
    public void Plan_SettingTheToolDoesNotRead_IsNotReportedThoughItIsNotValid()
    {
        var project = Project();
        project.Properties["SqlSourceGeneratorParameters"] = "keep-coments";
        project.Properties["SqlSourceInputModelSuffix"] = "a b";
        _ = project.AddSql("Q/One.sql", "SELECT 1;", ("SqlSourceCollectionType", "bag"));

        Plan(project).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_AttributeArgumentThatIsNoLiteral_IsSqlsrc208AndItsTypeClaimsNothing()
    {
        var project = new TestProject(_folder);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("A.cs", Type("A", "Path = Folder"));
        _ = project.AddSource("B.cs", Type("B", "Path = \"Other\""));
        _ = project.AddSql("One.sql", "SELECT 1;");
        var other = project.AddSql("Other/Two.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldHaveSingleItem().Descriptor.ShouldBe(ToolDiagnostics.AttributeArgumentNotLiteral);
        result.Plan.Files.Select(file => file.Path).ShouldBe([other]);
    }
}
