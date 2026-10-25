using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

// What is wrong with a plan: each row of its table in docs/superpowers/specs/2026-10-09-query-generation-phase-2-4-
// work-list-design.md, with its id, its place and what it does to the plan.
public sealed class RunPlannerErrorTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    // A project of the given dialect, postgres unless a test names another, with one type that claims the folder "Q".
    private TestProject Project(string name = "App", string dialect = "postgres")
    {
        var project = new TestProject(_folder, name);
        project.Properties["SqlSourceDialect"] = dialect;
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"Q\""));
        return project;
    }

    [Fact]
    public void Plan_FileWithAParseError_ReportsItWhereTheGeneratorDoesAndPlansNoQuery()
    {
        var project = Project();
        var sql = project.AddSql("Q/Broken.sql", "-- name: One\nSELECT 'open\n");

        var result = Plans.Result(project);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Descriptor.Id.ShouldBe("SQLSRC101");
        error.Path.ShouldBe(sql);
        error.Position.ShouldBe(new LinePosition(1, 7));
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.HasParseErrors);
        file.Queries.Count.ShouldBe(0);
    }

    // The tool describes the form without comments, so a keyword in braces in a kept comment, an error only for a
    // type that emits the form with comments, is not one of the plan: the file is planned and its query described.
    [Fact]
    public void Plan_ReservedTokenInAKeptComment_IsNoErrorAndTheFileIsPlanned()
    {
        var project = Project();
        _ = project.AddSql("Q/Kept.sql", "-- name: One\n-- generator: keep-comments\nSELECT 1; -- {{class}}\n");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.Ready);
        var query = file.Queries.ShouldHaveSingleItem();
        query.Query.Name.ShouldBe("One");
        query.NeedsEntry.ShouldBeTrue();
    }

    // The generator parses a file it cannot read as empty.
    [Fact]
    public void Plan_ClaimedFileThatIsNotOnTheDisk_IsReportedAsAFileWithoutSql()
    {
        var project = Project();
        var gone = project.PathOf("Q/Gone.sql");
        project.ListSql(gone);

        var result = Plans.Result(project);

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

        var result = Plans.Result(project);

        result.Errors.ShouldBe([ToolDiagnostic.AtFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "nosuch")]);
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

        var result = Plans.Result(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.AtFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "another"),
            ToolDiagnostic.AtFile(SqlDiagnostics.InvalidDialect, project.ProjectPath, "bad-one"),
        ]);
        result.Plan.Files.Count.ShouldBe(3);
        result.Plan.Files.Select(file => file.Dialect).ShouldAllBe(dialect => dialect == SqlDialect.Ansi);
    }

    [Fact]
    public void Plan_OutputAndDatabaseThatAreNotValid_AreSqlsrc014AtTheProjectAndAreNotSet()
    {
        var project = Project();
        project.Properties["SqlSourceOutput"] = "everything";
        project.Properties["SqlSourceDatabase"] = "not a name";
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var result = Plans.Result(project);

        result.Errors.ShouldBe([
            ToolDiagnostic.AtFile(
                SqlDiagnostics.InvalidSettingValue,
                project.ProjectPath,
                "not a name",
                "SqlSourceDatabase",
                ""
            ),
            ToolDiagnostic.AtFile(
                SqlDiagnostics.InvalidSettingValue,
                project.ProjectPath,
                "everything",
                "SqlSourceOutput",
                ""
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

        Plans
            .Result(project)
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
        project.Properties["SqlSourceTypeOverrides"] = "timestampz=int";
        project.Properties["SqlSourceColumnTypes"] = "id=UserId";
        _ = project.AddSql(
            "Q/One.sql",
            "SELECT 1;",
            ("SqlSourceCollectionType", "bag"),
            ("SqlSourceTypeOverrides", "int4=int?"),
            ("SqlSourceColumnTypes", "users.id=int?")
        );

        Plans.Result(project).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_AttributeArgumentThatIsNoLiteral_IsSqlsrc208AndItsTypeClaimsNothing()
    {
        var project = new TestProject(_folder);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("A.cs", Plans.TypeNamed("A", "Path = Folder"));
        _ = project.AddSource("B.cs", Plans.TypeNamed("B", "Path = \"Other\""));
        _ = project.AddSql("One.sql", "SELECT 1;");
        var other = project.AddSql("Other/Two.sql", "SELECT 1;");

        var result = Plans.Result(project);

        result.Errors.ShouldHaveSingleItem().Descriptor.ShouldBe(ToolDiagnostics.AttributeArgumentNotLiteral);
        result.Plan.Files.Select(file => file.Path).ShouldBe([other]);
    }

    // The errors of a project come in one order, each value once though the property and a file's metadata both
    // give it, and before the errors of its files: the file is read as ansi, which cannot be described.
    [Fact]
    public void Plan_ProjectWithEveryKindOfError_ReportsThemInOrderAndEachValueOnce()
    {
        var project = Project(dialect: "nosuch");
        project.Properties["SqlSourceOutput"] = "everything";
        _ = project.AddSource("Other.cs", Plans.TypeNamed("Other", "Path = Folder"));
        _ = project.AddSql("Q/One.sql", "SELECT 1;", ("SqlSourceDialect", "nosuch"), ("SqlSourceOutput", "everything"));

        var result = Plans.Result(project);

        result
            .Errors.Select(error => error.Descriptor.Id)
            .ShouldBe(["SQLSRC208", "SQLSRC011", "SQLSRC014", "SQLSRC209"]);
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

        var result = Plans.Result(project);

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

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.Ready);
    }

    [Fact]
    public void Plan_FileNotUtf8WithAQueryThatNeedsAnEntry_IsSqlsrc271AtTheFileAndKeepsItsQueries()
    {
        var project = Project();
        var sql = project.AddSqlBytes("Q/One.sql", Encoding.Latin1.GetBytes("-- name: One\nSELECT 'café';\n"));

        var result = Plans.Result(project);

        result.Errors.ShouldBe([ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, sql)]);
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.NotUtf8);
        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBeTrue();
    }

    [Fact]
    public void Plan_FileNotUtf8WhoseQueriesNeedNoEntry_IsReadyWithNoError()
    {
        var project = Project();
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSqlBytes("Q/One.sql", Encoding.Latin1.GetBytes("-- name: One\nSELECT 'café';\n"));

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.Ready);
    }

    [Fact]
    public void Plan_FileNotUtf8WithAParseError_IsSqlsrc271AloneWithNoQuery()
    {
        var project = Project();
        var sql = project.AddSqlBytes("Q/One.sql", Encoding.Latin1.GetBytes("-- name: One\nSELECT 'café\n"));

        var result = Plans.Result(project);

        result.Errors.ShouldBe([ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, sql)]);
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.State.ShouldBe(PlannedFileState.NotUtf8);
        file.Queries.Count.ShouldBe(0);
    }

    [Fact]
    public void Plan_FileNotUtf8UnderADialectThatCannotBeDescribed_IsSqlsrc271Alone()
    {
        var project = Project(dialect: "ansi");
        var sql = project.AddSqlBytes("Q/One.sql", Encoding.Latin1.GetBytes("-- name: One\nSELECT 'café';\n"));

        var result = Plans.Result(project);

        result.Errors.ShouldBe([ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, sql)]);
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.NotUtf8);
    }

    [Fact]
    public void Plan_FileNotUtf8WithTwoQueriesThatNeedAnEntry_IsSqlsrc271Once()
    {
        var project = Project();
        var sql = project.AddSqlBytes(
            "Q/Two.sql",
            Encoding.Latin1.GetBytes("-- name: One\nSELECT 'café';\n\n-- name: Two\nSELECT 'thé';\n")
        );

        var result = Plans.Result(project);

        result.Errors.ShouldBe([ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, sql)]);
        result.Plan.Files.ShouldHaveSingleItem().Queries.Count.ShouldBe(2);
    }

    [Fact]
    public void Plan_FileNotUtf8WithATokenWithoutADefault_IsSqlsrc271ThenSqlsrc210()
    {
        var project = Project();
        var sql = project.AddSqlBytes(
            "Q/Find.sql",
            Encoding.Latin1.GetBytes("-- name: Find\nSELECT 'café' FROM users {{where}};\n")
        );

        var result = Plans.Result(project);

        result
            .Errors.Select(error => (error.Descriptor.Id, error.Path, error.Position))
            .ShouldBe([("SQLSRC271", sql, null), ("SQLSRC210", sql, new LinePosition(0, Name.Length))]);
        result.Plan.Files.ShouldHaveSingleItem().State.ShouldBe(PlannedFileState.NotUtf8);
    }

    [Fact]
    public void Plan_TokensWithoutADefault_AreSqlsrc210ForEachAtTheNameOfTheirQuery()
    {
        var project = Project();
        var sql = project.AddSql(
            "Q/Find.sql",
            "-- name: Find\nSELECT id FROM users {{where}} {{order}} {{limit:LIMIT 10}} {{tail:}};\n"
        );

        var result = Plans.Result(project);

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

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem().Problems.ShouldBe(QueryProblems.None);
    }

    [Fact]
    public void Plan_TokenWithoutADefaultInAFileThatCannotBeDescribed_IsReportedBesideSqlsrc209()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/Find.sql", "-- name: Find\nSELECT id FROM users {{where}};\n");

        Plans.Result(project).Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC209", "SQLSRC210"]);
    }

    // Two types claim the file with two outputs: a message names the greater.
    [Theory]
    [InlineData("Models", "CodeGen", "codegen")]
    [InlineData("Models", "Sql", "models")]
    public void Plan_TwoClaimsWithTwoOutputs_NameTheGreaterInAMessage(string first, string second, string named)
    {
        var project = new TestProject(_folder);
        project.Properties["SqlSourceDialect"] = "ansi";
        _ = project.AddSource(
            "A.cs",
            Plans.TypeNamed("A", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{first}")
        );
        _ = project.AddSource(
            "B.cs",
            Plans.TypeNamed("B", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{second}")
        );
        _ = project.AddSql("Q/Find.sql", "-- name: Find\nSELECT id FROM users {{where}};\n");

        var errors = Plans.Result(project).Errors;

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

        var result = Plans.Result(project);

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

        var error = Plans.Result(first, second).Errors.ShouldHaveSingleItem();

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

        Plans.Result(project).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_FileThatCannotBeDescribed_GivesItsDatabaseNoDialectAndGetsSqlsrc209Alone()
    {
        var project = Project();
        // The first file of the database in the plan's order is the one that cannot be described.
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n", ("SqlSourceDialect", "ansi"));
        _ = project.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2;\n");

        var result = Plans.Result(project);

        result.Errors.Select(error => error.Descriptor.Id).ShouldBe(["SQLSRC209"]);
        result.Plan.Databases.ShouldBe([new PlannedDatabase("main", SqlDialect.PostgreSql)]);
        result.Plan.Files.SelectMany(file => file.Queries).ShouldAllBe(query => query.Problems == QueryProblems.None);
    }

    [Fact]
    public void Plan_DatabaseWhoseFilesCanNoneBeDescribed_HasTheDialectOfItsFirstQuery()
    {
        var project = Project(dialect: "ansi");
        _ = project.AddSql("Q/A.sql", "-- name: One\nSELECT 1;\n");

        Plans.Result(project).Plan.Databases.ShouldBe([new PlannedDatabase("ansi", SqlDialect.Ansi)]);
    }

    [Fact]
    public void Plan_QueryWithATokenWithoutADefaultInADatabaseOfAnotherDialect_HasBothProblems()
    {
        var project = Project();
        _ = project.AddSql("Q/A.sql", InMain + "-- name: One\nSELECT 1;\n");
        _ = project.AddSql("Q/B.sql", InMain + "-- name: Two\nSELECT 2 {{tail}};\n", ("SqlSourceDialect", "mssql"));

        var result = Plans.Result(project);

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

        Plans.Result(project).Errors.Select(error => error.Arguments[0]).ShouldBe(["one", "two"]);
    }
}
