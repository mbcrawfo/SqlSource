using System;
using System.Linq;
using System.Text;
using System.Threading;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Testing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

// The plan of a run, from manifests that a test builds: which files are claimed, and what each query of them needs.
public sealed class RunPlannerTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    // A project whose settings file says the dialect is postgres.
    private TestProject Postgres(string name = "App")
    {
        var project = new TestProject(_folder, name);
        project.Settings["dialect"] = "postgres";
        return project;
    }

    [Fact]
    public void Plan_ExampleOfTheSpec_HasThreeQueriesWithTheirValues()
    {
        var project = Postgres();
        _ = project.AddSource(
            "Queries.cs",
            Plans.TypeNamed("Queries", "Path = \"Queries\", Output = SqlSource.GeneratorOutput.Models")
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

        var result = Plans.Result(project);

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
        _ = project.AddSource("Repo/Repository.cs", Plans.TypeNamed("Repository"));
        var beside = project.AddSql("Repo/A.sql", "SELECT 1;");
        _ = project.AddSql("Repo/Sub/B.sql", "SELECT 1;");

        Plans.Result(project).Plan.Files.Select(file => file.Path).ShouldBe([beside]);
    }

    [Fact]
    public void Plan_PathOfAFile_ClaimsThatFile()
    {
        var project = Postgres();
        _ = project.AddSource("Repository.cs", Plans.TypeNamed("Repository", "Path = \"Queries/b.SQL\""));
        _ = project.AddSql("Queries/A.sql", "SELECT 1;");
        var named = project.AddSql("Queries/B.sql", "SELECT 1;");

        Plans.Result(project).Plan.Files.Select(file => file.Path).ShouldBe([named]);
    }

    [Fact]
    public void Plan_Files_AreInTheOrderOfTheirPaths()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        var b = project.AddSql("b.sql", "SELECT 1;");
        var a = project.AddSql("A.sql", "SELECT 1;");

        Plans.Result(project).Plan.Files.Select(file => file.Path).ShouldBe([a, b]);
    }

    [Theory]
    [InlineData("Sql", "Models", true)]
    [InlineData("Sql", "Sql", false)]
    public void Plan_TwoTypesThatClaimOneFile_NeedWhatEitherNeeds(string first, string second, bool needsEntry)
    {
        var project = Postgres();
        _ = project.AddSource(
            "A.cs",
            Plans.TypeNamed("A", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{first}")
        );
        _ = project.AddSource(
            "B.cs",
            Plans.TypeNamed("B", $"Path = \"Q\", Output = SqlSource.GeneratorOutput.{second}")
        );
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var file = Plans.Result(project).Plan.Files.ShouldHaveSingleItem();

        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBe(needsEntry);
    }

    // The order the project compiles its files in, which is not the order of their paths here.
    [Fact]
    public void Plan_TwoTypesThatClaimOneFile_HasBothClaimsInTheOrderOfTheirSourceFiles()
    {
        var project = Postgres();
        var first = project.AddSource("B.cs", Plans.TypeNamed("B", "Path = \"Q\""));
        var second = project.AddSource("A.cs", Plans.TypeNamed("A", "Path = \"Q/One.sql\""));
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var file = Plans.Result(project).Plan.Files.ShouldHaveSingleItem();

        file.Claims.Select(claim => claim.SourcePath).ShouldBe([first, second]);
    }

    [Fact]
    public void Plan_FileThatNoTypeClaims_IsNotReadThoughItDoesNotParse()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"Queries\""));
        _ = project.AddSql("Queries/One.sql", "SELECT 1;");
        _ = project.AddSql("Migrations/Broken.sql", "-- name: Broken\nSELECT 'open\n");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.Count.ShouldBe(1);
    }

    // The build reports a Path that matches nothing.
    [Theory]
    [InlineData("Nowhere")]
    [InlineData("Nowhere.sql")]
    [InlineData("../../../../../../../../../../../../../../../../../../../../../../../../..")]
    public void Plan_PathThatNamesNothing_PlansNothingAndReportsNothing(string path)
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", $"Path = \"{path}\""));
        _ = project.AddSql("Q.sql", "SELECT 1;");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.Count.ShouldBe(0);
    }

    [Fact]
    public void Plan_FileWithAByteOrderMarkOrCarriageReturns_HashesAsTheFileWithout()
    {
        const string Sql = "-- name: One\nSELECT id\nFROM users\nWHERE id = @id;\n";
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = project.AddSql("A.sql", Sql);
        _ = project.AddSqlBytes("B.sql", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Sql)]);
        _ = project.AddSql("C.sql", Sql.Replace("\n", "\r\n", StringComparison.Ordinal));

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        var hashes = result.Plan.Files.Select(file => file.Queries.ShouldHaveSingleItem().Hash).ToArray();
        hashes.Length.ShouldBe(3);
        _ = hashes.Distinct().ShouldHaveSingleItem().ShouldNotBeNull();
    }

    [Fact]
    public void Plan_TwoListedPathsThatDifferOnlyByCase_PlanTheFirst()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        var first = project.AddSql("Users.sql", "SELECT 1;");
        project.ListSql(project.PathOf("USERS.SQL"));

        Plans.Result(project).Plan.Files.ShouldHaveSingleItem().Path.ShouldBe(first);
    }

    // The project lists the path twice by one spelling: one file, planned once, with its one query.
    [Fact]
    public void Plan_PathListedTwiceByOneSpelling_IsPlannedOnce()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        var path = project.AddSql("Q.sql", "-- name: Q\nSELECT 1;\n");
        project.ListSql(path);

        var file = Plans.Result(project).Plan.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(path);
        file.Queries.ShouldHaveSingleItem().Query.Name.ShouldBe("Q");
    }

    [Fact]
    public void Plan_SeveralProjects_HasTheFilesOfEachInTheOrderOfTheProjects()
    {
        var first = Postgres("Zeta");
        _ = first.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        var z = first.AddSql("Z.sql", "SELECT 1;");
        var second = Postgres("Alpha");
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        var a = second.AddSql("A.sql", "SELECT 1;");

        var files = Plans.Result(first, second).Plan.Files;

        files.Select(file => (file.Path, file.ProjectPath)).ShouldBe([(z, first.ProjectPath), (a, second.ProjectPath)]);
    }

    [Fact]
    public void Plan_FileThatTwoProjectsClaim_IsPlannedOnceUnderTheFirstAndNeedsWhatEitherNeeds()
    {
        var shared = _folder.WriteFile("Shared/One.sql", "SELECT 1;");
        // The file is in neither project's folder: the settings file of its own folder, which the first project lists
        // and the second does not, gives the first's claim no need of an entry.
        var settings = _folder.WriteFile(
            "Shared/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres", "output": "sql"}"""
        );
        var first = Postgres("A");
        first.ListSettings(settings);
        _ = first.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"../Shared\""));
        first.ListSql(shared);
        var second = Postgres("B");
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"../Shared\""));
        second.ListSql(shared);

        var file = Plans.Result(first, second).Plan.Files.ShouldHaveSingleItem();

        file.ProjectPath.ShouldBe(first.ProjectPath);
        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBeTrue();
    }

    // A file that two projects claim is planned once, and each counts it as claimed.
    [Fact]
    public void Plan_FileThatTwoProjectsClaim_CountsUnclaimedForEach()
    {
        var first = Postgres("A");
        _ = first.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        var shared = first.AddSql("X.sql", "SELECT 1;");
        _ = first.AddSql("Only.sql", "SELECT 2;");
        var second = Postgres("B");
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"../A/X.sql\""));
        second.ListSql(shared);
        _ = second.AddSql("Lone.sql", "SELECT 3;");

        var result = Plans.Result(first, second);

        result.Plan.Files.Count(file => file.Path == shared).ShouldBe(1);
        result.Unclaimed.ShouldBe([0, 1]);
    }

    [Fact]
    public void Plan_Filters_AreKeptOnThePlan()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = project.AddSql("Q.sql", "SELECT 1;");
        var filters = new RunFilters([], [new UsageValue(null, "billing", 1)]);

        Plans.Result(filters, project).Plan.Filters.ShouldBeSameAs(filters);
    }

    // A .sql path that climbs above the root names no file: it selects none, beside --database too, and is reported.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plan_FileFilterWithNoNormalizedPath_SelectsNothingAndIsSqlsrc212(bool withDatabase)
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSql("Q.sql", SqlTexts.Users);
        var filters = new RunFilters(
            [new FileFilter(null, "'x'")],
            withDatabase ? [new UsageValue(null, "billing", 1)] : []
        );

        var result = Plans.Result(filters, project);

        result.Plan.Files.SelectMany(file => file.Queries).ShouldAllBe(query => !query.IsSelected);
        result.Errors.ShouldHaveSingleItem().ShouldBe(ToolDiagnostic.Create(ToolDiagnostics.FileNotInRun, "'x'"));
    }

    [Fact]
    public void Plan_FileThatTheFirstProjectListsAndTheSecondClaims_IsPlannedUnderTheSecond()
    {
        var shared = _folder.WriteFile("Shared/One.sql", "SELECT 1;");
        // The file is in neither project's folder.  Both list the farther settings file, which says postgres, and the
        // second lists the nearer one too, which says mssql.
        var farther = _folder.WriteFile(
            "sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres"}"""
        );
        var nearer = _folder.WriteFile(
            "Shared/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "mssql"}"""
        );
        var first = Postgres("A");
        first.ListSettings(farther);
        _ = first.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = first.AddSql("Own.sql", "SELECT 1;");
        first.ListSql(shared);
        var second = Postgres("B");
        second.ListSettings(farther);
        second.ListSettings(nearer);
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"../Shared\""));
        second.ListSql(shared);

        var file = Plans.Result(first, second).Plan.Files.Single(file => file.Path == shared);

        file.ProjectPath.ShouldBe(second.ProjectPath);
        file.Dialect.ShouldBe(SqlDialect.SqlServer);
    }

    [Fact]
    public void Plan_Databases_AreThoseOfTheQueriesThatNeedAnEntryInTheOrderOfThePlan()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = project.AddSql("A.sql", "-- name: One\nSELECT 1;\n\n-- name: Plain\n-- output: sql\nSELECT 0;\n");
        _ = project.AddSql("B.sql", "-- dialect: mssql\nSELECT 2;");
        _ = project.AddSql("C.sql", "-- output: sql\n-- database: unused\n-- name: Three\nSELECT 3;\n");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Databases.ShouldBe([
            new PlannedDatabase("postgres", SqlDialect.PostgreSql),
            new PlannedDatabase("mssql", SqlDialect.SqlServer),
        ]);
    }

    [Fact]
    public void Plan_DatabaseNamesThatDifferInCase_AreOneDatabaseInItsFirstSpelling()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = project.AddSql("A.sql", "-- database: Billing\n-- name: One\nSELECT 1;\n");
        _ = project.AddSql("B.sql", "-- database: BILLING\n-- name: Two\nSELECT 2;\n");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Databases.ShouldBe([new PlannedDatabase("Billing", SqlDialect.PostgreSql)]);
        result
            .Plan.Files.SelectMany(file => file.Queries)
            .Select(query => query.Database)
            .ShouldBe(["Billing", "Billing"]);
    }

    // The settings files of the chain: sqlsource.json files apply to their folder and below.  A project with no
    // settings of its own, so that a test writes the file at its root as it means to.
    private TestProject Chained()
    {
        var project = new TestProject(_folder);
        _ = project.AddSource("Queries.cs", Plans.Type);
        return project;
    }

    private static PlannedFile OnlyFile(TestProject project) => Plans.Result(project).Plan.Files.ShouldHaveSingleItem();

    [Fact]
    public void Plan_DialectOfTheChain_IsTheFilesDialect()
    {
        var project = Chained();
        _ = project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres"}"""
        );
        _ = project.AddSql("Q.sql", "SELECT 1;");

        OnlyFile(project).Dialect.ShouldBe(SqlDialect.PostgreSql);
    }

    [Fact]
    public void Plan_NearerFilesDialect_WinsOverTheFarther()
    {
        var project = Chained();
        _ = project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            """{"dialect": "ansi"}"""
        );
        _ = project.AddSettings(
            "Queries/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "mssql"}"""
        );
        _ = project.AddSource("Queries/Repository.cs", Plans.TypeNamed("Repository"));
        _ = project.AddSql("Queries/Q.sql", "SELECT 1;");

        OnlyFile(project).Dialect.ShouldBe(SqlDialect.SqlServer);
    }

    [Fact]
    public void Plan_HeaderMarkersDialect_WinsOverTheChain()
    {
        var project = Chained();
        _ = project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            """{"dialect": "mssql"}"""
        );
        _ = project.AddSql("Q.sql", "-- dialect: postgres\nSELECT 1;");

        OnlyFile(project).Dialect.ShouldBe(SqlDialect.PostgreSql);
    }

    [Fact]
    public void Plan_RootFile_StopsTheChain()
    {
        var project = Chained();
        project.ListSettings(
            _folder.WriteFile(
                "sqlsource.json", /*lang=json,strict*/
                """{"database": "far"}"""
            )
        );
        _ = project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            """{"root": true, "dialect": "postgres"}"""
        );
        _ = project.AddSql("Q.sql", "SELECT 1;");

        var query = OnlyFile(project).Queries.ShouldHaveSingleItem();

        query.Database.ShouldBe("postgres");
    }

    [Theory]
    [InlineData("sql", false)]
    [InlineData(null, true)]
    public void Plan_OutputOfTheChain_DecidesWhetherAQueryNeedsAnEntry(string? chain, bool needsEntry)
    {
        var project = Postgres();
        if (chain is not null)
        {
            project.Settings["output"] = chain;
        }

        _ = project.AddSource("Queries.cs", Plans.Type);
        _ = project.AddSql("Q.sql", "SELECT 1;");

        OnlyFile(project).Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBe(needsEntry);
    }

    [Fact]
    public void Plan_AttributesOutput_WinsOverTheChain()
    {
        var project = Postgres();
        project.Settings["output"] = "sql";
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Output = SqlSource.GeneratorOutput.Models"));
        _ = project.AddSql("Q.sql", "SELECT 1;");

        OnlyFile(project).Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBeTrue();
    }

    [Fact]
    public void Plan_DatabaseOfTheChain_IsTheQuerysDatabase()
    {
        var project = Postgres();
        project.Settings["database"] = "billing";
        _ = project.AddSource("Queries.cs", Plans.Type);
        _ = project.AddSql("Q.sql", "-- name: One\nSELECT 1;\n\n-- name: Two\n-- database: other\nSELECT 2;\n");

        OnlyFile(project).Queries.Select(query => query.Database).ShouldBe(["billing", "other"]);
    }

    // The two projects see one file: the chain of each is that of the file in its own project's set, and the file
    // needs what either needs.  The second does not list the first's settings file, so its claim has the default
    // output, which needs an entry.
    [Fact]
    public void Plan_FileClaimedByTwoProjects_TakesEachProjectsChainForItsOutput()
    {
        var first = Plans.Postgres(_folder, "A");
        first.Settings["output"] = "sql";
        var shared = first.AddSql("One.sql", "SELECT 1;");
        var second = Postgres("B");
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"../A\""));
        second.ListSql(shared);

        var file = Plans.Result(first, second).Plan.Files.ShouldHaveSingleItem();

        file.ProjectPath.ShouldBe(first.ProjectPath);
        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBeTrue();
    }

    // Both projects list the farther settings file, which says sql, and only the second lists the nearer one, which
    // says models, so only the second's claim needs an entry: the owner's chain is not the one of every claim.
    [Fact]
    public void Plan_FileClaimedByTwoProjects_TakesTheChainOfAProjectThatIsNotTheOwner()
    {
        var farther = _folder.WriteFile(
            "sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres", "output": "sql"}"""
        );
        var nearer = _folder.WriteFile(
            "Shared/sqlsource.json", /*lang=json,strict*/
            """{"output": "models"}"""
        );
        var shared = _folder.WriteFile("Shared/One.sql", "SELECT 1;");
        var first = Postgres("A");
        first.ListSettings(farther);
        _ = first.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"../Shared\""));
        first.ListSql(shared);
        var second = Postgres("B");
        second.ListSettings(farther);
        second.ListSettings(nearer);
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Path = \"../Shared\""));
        second.ListSql(shared);

        var file = Plans.Result(first, second).Plan.Files.ShouldHaveSingleItem();

        file.ProjectPath.ShouldBe(first.ProjectPath);
        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBeTrue();
    }

    [Fact]
    public void Plan_RunThatWasCancelled_Throws()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = project.AddSql("Q.sql", "SELECT 1;");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        _ = Should.Throw<OperationCanceledException>(() =>
            RunPlanner.Plan([project.Manifest()], RunFilters.None, cancelled.Token)
        );
    }
}
