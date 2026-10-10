using System;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// The plan of a run, from manifests that a test builds: which files are claimed, and what each query of them needs.
public sealed class RunPlannerTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static RunPlanResult Plan(params TestProject[] projects) =>
        RunPlanner.Plan([.. projects.Select(project => project.Manifest())], TestContext.Current.CancellationToken);

    // A type with the attribute, in a file of its own.
    private static string Type(string name, string arguments = "") =>
        $"[SqlSource.SqlSourceGenerate({arguments})]\ninternal static partial class {name};\n";

    private static (string Name, string Value)[] Metadata(string name, string? value) =>
        value is null ? [] : [(name, value)];

    private TestProject Postgres(string name = "App")
    {
        var project = new TestProject(_folder, name);
        project.Properties["SqlSourceDialect"] = "postgres";
        return project;
    }

    [Fact]
    public void Plan_ExampleOfTheSpec_HasThreeQueriesWithTheirValues()
    {
        var project = Postgres();
        _ = project.AddSource(
            "Queries.cs",
            Type("Queries", "Path = \"Queries\", Output = SqlSource.GeneratorOutput.Models")
        );
        var sql = project.AddSql(
            "Queries/Users.sql",
            """
            -- name: GetUser
            SELECT id, name FROM users WHERE id = @id;

            -- name: CountUsers
            -- output: sql
            SELECT count(*) FROM users;

            -- name: GetInvoice
            -- database: billing
            SELECT id FROM invoices WHERE id = @id;
            """
        );

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(sql);
        file.ProjectPath.ShouldBe(project.ProjectPath);
        file.Dialect.ShouldBe(SqlDialect.PostgreSql);
        file.State.ShouldBe(PlannedFileState.Ready);
        file.Queries.Select(query => (query.Query.Name, query.NeedsEntry, query.Database))
            .ShouldBe([("GetUser", true, "postgres"), ("CountUsers", false, null), ("GetInvoice", true, "billing")]);

        var getUser = file.Queries[0];
        getUser.Hash.ShouldBe(
            SqlQueryHash.Compute(
                SqlDialect.PostgreSql,
                getUser.Query.Segments,
                getUser.Query.Tokens,
                getUser.Query.Parameters
            )
        );
        getUser.Hash!.Length.ShouldBe(64);
        file.Queries[1].Hash.ShouldBeNull();
    }

    [Fact]
    public void Plan_NoPath_ClaimsTheFilesBesideTheSourceFileAndNotThoseBelow()
    {
        var project = Postgres();
        _ = project.AddSource("Repo/Repository.cs", Type("Repository"));
        var beside = project.AddSql("Repo/A.sql", "SELECT 1;");
        _ = project.AddSql("Repo/Sub/B.sql", "SELECT 1;");

        Plan(project).Plan.Files.Select(file => file.Path).ShouldBe([beside]);
    }

    [Fact]
    public void Plan_PathOfAFile_ClaimsThatFile()
    {
        var project = Postgres();
        _ = project.AddSource("Repository.cs", Type("Repository", "Path = \"Queries/b.SQL\""));
        _ = project.AddSql("Queries/A.sql", "SELECT 1;");
        var named = project.AddSql("Queries/B.sql", "SELECT 1;");

        Plan(project).Plan.Files.Select(file => file.Path).ShouldBe([named]);
    }

    [Fact]
    public void Plan_Files_AreInTheOrderOfTheirPaths()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        var b = project.AddSql("b.sql", "SELECT 1;");
        var a = project.AddSql("A.sql", "SELECT 1;");

        Plan(project).Plan.Files.Select(file => file.Path).ShouldBe([a, b]);
    }

    [Theory]
    [InlineData("Sql", "Models", true)]
    [InlineData("Sql", "Sql", false)]
    public void Plan_TwoTypesThatClaimOneFile_NeedWhatEitherNeeds(string first, string second, bool needsEntry)
    {
        var project = Postgres();
        _ = project.AddSource("A.cs", Type("A", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{first}"));
        _ = project.AddSource("B.cs", Type("B", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{second}"));
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var file = Plan(project).Plan.Files.ShouldHaveSingleItem();

        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBe(needsEntry);
    }

    [Fact]
    public void Plan_FileThatNoTypeClaims_IsNotReadThoughItDoesNotParse()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries", "Path = \"Queries\""));
        _ = project.AddSql("Queries/One.sql", "SELECT 1;");
        _ = project.AddSql("Migrations/Broken.sql", "-- name: Broken\nSELECT 'open\n");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.Count.ShouldBe(1);
    }

    // Review focus 3.  The build reports a Path that matches nothing.
    [Theory]
    [InlineData("Nowhere")]
    [InlineData("Nowhere.sql")]
    [InlineData("../../../../../../../../../../../../../../../../../../../../../../../../..")]
    public void Plan_PathThatNamesNothing_PlansNothingAndReportsNothing(string path)
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries", $"Path = \"{path}\""));
        _ = project.AddSql("Q.sql", "SELECT 1;");

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(null, null, null, null, null, true)]
    [InlineData("sql", null, null, null, null, false)]
    [InlineData("sql", "models", null, null, null, true)]
    [InlineData("models", "models", "Sql", null, null, false)]
    [InlineData("sql", "sql", "Sql", "models", null, true)]
    [InlineData("models", "models", "Models", "models", "sql", false)]
    public void Plan_OutputFromEachLevel_IsTakenFromTheMostSpecific(
        string? property,
        string? metadata,
        string? attribute,
        string? preamble,
        string? query,
        bool needsEntry
    )
    {
        var project = Postgres();
        if (property is not null)
        {
            project.Properties["SqlSourceOutput"] = property;
        }

        _ = project.AddSource(
            "Queries.cs",
            Type("Queries", attribute is null ? "" : $"Output = SqlSource.GeneratorOutput.{attribute}")
        );
        _ = project.AddSql(
            "Q.sql",
            (preamble is null ? "" : $"-- output: {preamble}\n")
                + "-- name: One\n"
                + (query is null ? "" : $"-- output: {query}\n")
                + "SELECT 1;\n",
            Metadata("SqlSourceOutput", metadata)
        );

        var planned = Plan(project).Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem();

        planned.NeedsEntry.ShouldBe(needsEntry);
        (planned.Hash is not null).ShouldBe(needsEntry);
        (planned.Database is not null).ShouldBe(needsEntry);
    }

    [Theory]
    [InlineData(null, null, null, null, "postgres")]
    [InlineData("main", null, null, null, "main")]
    [InlineData("main", "files", null, null, "files")]
    [InlineData("main", "files", "pre", null, "pre")]
    [InlineData("main", "files", "pre", "own", "own")]
    public void Plan_DatabaseFromEachLevel_IsTakenFromTheMostSpecificAndElseIsTheDialectsName(
        string? property,
        string? metadata,
        string? preamble,
        string? query,
        string expected
    )
    {
        var project = new TestProject(_folder);
        // An alias: the name of a database is the dialect's first name.
        project.Properties["SqlSourceDialect"] = "postgresql";
        if (property is not null)
        {
            project.Properties["SqlSourceDatabase"] = property;
        }

        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql(
            "Q.sql",
            (preamble is null ? "" : $"-- database: {preamble}\n")
                + "-- name: One\n"
                + (query is null ? "" : $"-- database: {query}\n")
                + "SELECT 1;\n",
            Metadata("SqlSourceDatabase", metadata)
        );

        Plan(project).Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem().Database.ShouldBe(expected);
    }

    [Fact]
    public void Plan_DialectOfAFile_IsItsMarkerThenItsMetadataThenTheProperty()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql("A.sql", "SELECT 1;");
        _ = project.AddSql("B.sql", "SELECT 1;", ("SqlSourceDialect", "mssql"));
        _ = project.AddSql("C.sql", "-- dialect: postgres\nSELECT 1;", ("SqlSourceDialect", "mssql"));

        Plan(project)
            .Plan.Files.Select(file => file.Dialect)
            .ShouldBe([SqlDialect.PostgreSql, SqlDialect.SqlServer, SqlDialect.PostgreSql]);
    }

    [Fact]
    public void Plan_FileWithAByteOrderMarkOrCarriageReturns_HashesAsTheFileWithout()
    {
        const string Sql = "-- name: One\nSELECT id\nFROM users\nWHERE id = @id;\n";
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        _ = project.AddSql("A.sql", Sql);
        _ = project.AddSqlBytes("B.sql", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Sql)]);
        _ = project.AddSql("C.sql", Sql.Replace("\n", "\r\n", StringComparison.Ordinal));

        var result = Plan(project);

        result.Errors.ShouldBeEmpty();
        var hashes = result.Plan.Files.Select(file => file.Queries.ShouldHaveSingleItem().Hash).ToArray();
        hashes.Length.ShouldBe(3);
        _ = hashes.Distinct().ShouldHaveSingleItem().ShouldNotBeNull();
    }

    [Fact]
    public void Plan_TwoListedPathsThatDifferOnlyByCase_PlanTheFirst()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Type("Queries"));
        var first = project.AddSql("Users.sql", "SELECT 1;");
        project.ListSql(project.PathOf("USERS.SQL"));

        Plan(project).Plan.Files.ShouldHaveSingleItem().Path.ShouldBe(first);
    }
}
