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

    private const string Name = "-- name: ";

    [Theory]
    [InlineData("ansi", "ansi")]
    [InlineData("sqlite", "sqlite")]
    [InlineData("cockroach", "cockroachdb")]
    public void Plan_QueryThatNeedsAnEntryUnderADialectThatCannotBeDescribed_IsSqlsrc209OnceForItsFile(
        string dialect,
        string canonical
    )
    {
        var project = Project(dialect: dialect);
        var sql = project.AddSql(
            "Q/Three.sql",
            "-- name: Plain\n-- output: sql\nSELECT 0;\n\n-- name: One\nSELECT 1;\n\n-- name: Two\nSELECT 2;\n"
        );

        var result = Plan(project);

        // At the name of the first query that needs an entry, which is the second of the file.
        result.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.OutputNeedsDescribableDialect,
                sql,
                new LinePosition(4, Name.Length),
                "codegen",
                canonical
            ),
        ]);
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.NotDescribable);
        file.Queries.Select(query => query.NeedsEntry).ShouldBe([false, true, true]);
    }

    [Fact]
    public void Plan_FileUnderADialectThatCannotBeDescribedWhoseQueriesAreAllSql_IsReady()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/One.sql", "-- output: sql\n-- name: One\nSELECT 1;\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.Ready);
    }

    [Fact]
    public void Plan_TokensWithoutADefault_AreSqlsrc210ForEachAtTheNameOfTheirQuery()
    {
        var project = Project();
        var sql = project.AddSql(
            "Q/Find.sql",
            "-- name: Find\nSELECT id FROM users {{where}} {{order}} {{limit:LIMIT 10}} {{tail:}};\n"
        );

        var result = Plan(project);

        result
            .Errors.Select(error => (error.Descriptor.Id, error.Path, error.Position, error.Arguments[0]))
            .ShouldBe([
                ("SQLSRC210", sql, new LinePosition(0, Name.Length), "where"),
                ("SQLSRC210", sql, new LinePosition(0, Name.Length), "order"),
            ]);
        result.Errors[0].Arguments[1].ShouldBe("codegen");
        var help = result.Errors[0].Lines.ShouldHaveSingleItem();
        help.Label.ShouldBe("help");
        help.Text.ShouldContain("{{where:default}}");
        help.Text.ShouldContain("-- token:");
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.Ready);
        file.Queries.ShouldHaveSingleItem().Problems.ShouldBe(QueryProblems.TokenWithoutDefault);
    }

    [Fact]
    public void Plan_TokenWithoutADefaultInAQueryWhoseOutputIsSql_IsNoError()
    {
        var project = Project();
        _ = project.AddSql("Q/Find.sql", "-- name: Find\n-- output: sql\nSELECT id FROM users {{where}};\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem().Problems.ShouldBe(QueryProblems.None);
    }

    [Fact]
    public void Plan_TokenWithoutADefaultInAFileThatCannotBeDescribed_IsReportedBesideSqlsrc209()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/Find.sql", "-- name: Find\nSELECT id FROM users {{where}};\n");

        Plan(project).Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC209", "SQLSRC210"]);
    }

    // Two types claim the file, one for its models and one for everything: a message names the greater.
    [Theory]
    [InlineData("Models", "CodeGen", "codegen")]
    [InlineData("Models", "Sql", "models")]
    public void Plan_TwoClaimsWithTwoOutputs_NameTheGreaterInAMessage(string first, string second, string named)
    {
        var project = new TestProject(_folder);
        project.Properties["SqlSourceDialect"] = "ansi";
        _ = project.AddSource("A.cs", Type("A", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{first}"));
        _ = project.AddSource("B.cs", Type("B", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{second}"));
        _ = project.AddSql("Q/Find.sql", "-- name: Find\nSELECT id FROM users {{where}};\n");

        var errors = Plan(project).Errors;

        // SQLSRC209 and SQLSRC210, and each names the output.
        errors.Count.ShouldBe(2);
        errors.Select(error => error.Arguments).ShouldAllBe(arguments => arguments.Contains(named));
    }

    private const string InMain = "-- database: main\n";

    [Fact]
    public void Plan_DatabaseWithTwoDialectsInTwoFiles_IsSqlsrc211AtTheFirstQueryOfTheSecondFile()
    {
        var project = Project();
        var first = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        var second = project.AddSql(
            "Q/B.sql",
            InMain
                + "-- name: Plain\n-- output: sql\nSELECT 0;\n\n-- name: Two\nSELECT 2;\n\n-- name: Three\nSELECT 3;\n",
            ("SqlSourceDialect", "mssql")
        );

        var result = Plan(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.At(
                ToolDiagnostics.DatabaseHasTwoDialects,
                second,
                new LinePosition(5, Name.Length),
                "main",
                "mssql",
                "postgres",
                first
            ),
        ]);
        result.Plan.Databases.ShouldBe([new PlannedDatabase("main", SqlDialect.PostgreSql)]);
        result.Plan.Files[0].Queries.Select(query => query.Problems).ShouldBe([QueryProblems.None]);
        result.Plan.Files[1].State.ShouldBe(PlannedFileState.Ready);
        result
            .Plan.Files[1]
            .Queries.Select(query => query.Problems)
            .ShouldBe([
                QueryProblems.None,
                QueryProblems.DatabaseDialectConflict,
                QueryProblems.DatabaseDialectConflict,
            ]);
    }

    [Fact]
    public void Plan_DatabaseWithTwoDialectsInTwoProjects_IsSqlsrc211InTheSecond()
    {
        var first = Project("A");
        var firstFile = first.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        var second = Project("B", dialect: "mssql");
        var secondFile = second.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2;\n");

        var error = Plan(first, second).Errors.ShouldHaveSingleItem();

        error.Descriptor.ShouldBe(ToolDiagnostics.DatabaseHasTwoDialects);
        error.Path.ShouldBe(secondFile);
        error.Arguments.ShouldBe(["main", "mssql", "postgres", firstFile]);
    }

    [Fact]
    public void Plan_FileOfAnotherDialectWhoseQueriesAreAllSql_GivesItsDatabaseNoSecondDialect()
    {
        var project = Project();
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        _ = project.AddSql(
            "Q/B.sql",
            InMain + "-- output: sql\n-- name: Two\nSELECT 2;\n",
            ("SqlSourceDialect", "mssql")
        );

        Plan(project).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_FileThatCannotBeDescribed_GivesItsDatabaseNoDialectAndGetsSqlsrc209Alone()
    {
        var project = Project();
        // The first file of the database in the plan's order is the one that cannot be described.
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n", ("SqlSourceDialect", "ansi"));
        _ = project.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2;\n");

        var result = Plan(project);

        result.Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC209"]);
        result.Plan.Databases.ShouldBe([new PlannedDatabase("main", SqlDialect.PostgreSql)]);
        result.Plan.Files.SelectMany(file => file.Queries).ShouldAllBe(query => query.Problems == QueryProblems.None);
    }

    [Fact]
    public void Plan_DatabaseWhoseFilesCanNoneBeDescribed_HasTheDialectOfItsFirstQuery()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/A.sql", "-- name: One\nSELECT 1;\n");

        Plan(project).Plan.Databases.ShouldBe([new PlannedDatabase("ansi", SqlDialect.Ansi)]);
    }

    [Fact]
    public void Plan_QueryWithATokenWithoutADefaultInADatabaseOfAnotherDialect_HasBothProblems()
    {
        var project = Project();
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        _ = project.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2 {{tail}};\n", ("SqlSourceDialect", "mssql"));

        var result = Plan(project);

        result.Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC210", "SQLSRC211"]);
        result
            .Plan.Files[1]
            .Queries.ShouldHaveSingleItem()
            .Problems.ShouldBe(QueryProblems.TokenWithoutDefault | QueryProblems.DatabaseDialectConflict);
    }

    [Fact]
    public void Plan_FileWithQueriesOfTwoDatabasesOfAnotherDialect_IsSqlsrc211ForEachDatabase()
    {
        var project = Project();
        _ = project.AddSql(
            "Q/A.sql",
            "-- name: One\n-- database: one\nSELECT 1;\n\n-- name: Two\n-- database: two\nSELECT 2;\n"
        );
        _ = project.AddSql(
            "Q/B.sql",
            "-- name: Three\n-- database: ONE\nSELECT 3;\n\n-- name: Four\n-- database: two\nSELECT 4;\n\n"
                + "-- name: Five\n-- database: one\nSELECT 5;\n",
            ("SqlSourceDialect", "mssql")
        );

        Plan(project).Errors.Select(error => error.Arguments[0]).ShouldBe(["one", "two"]);
    }
}
