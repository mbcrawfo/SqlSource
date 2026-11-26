using System;
using System.Linq;
using System.Text;
using System.Threading;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
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
        project.Settings["output"] = "models";
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"Queries\""));
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
                getUser.Query.Parameters,
                getUser.Query.MarkedVariables
            )
        );
        getUser.Hash!.Length.ShouldBe(64);
        file.Queries[1].Hash.ShouldBeNull();
    }

    // The plan hashes a query as the generator checks it: with the names of its -- variable: markers, which take names
    // out of its parameters.
    [Fact]
    public void Plan_QueryWithAVariableMarker_HashesItsNamesAsTheGeneratorDoes()
    {
        var project = Postgres();
        project.Settings["output"] = "models";
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"Queries\""));
        const string WithoutMarker = "-- name: ListPosts\nSELECT id FROM posts WHERE author_id = @author;\n";
        const string WithMarker =
            "-- name: ListPosts\n-- variable: @author\nSELECT id FROM posts WHERE author_id = @author;\n";
        _ = project.AddSql("Queries/Posts.sql", WithMarker);

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        var query = result.Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem();
        query.Query.MarkedVariables.ShouldBe(["author"]);
        query.Hash.ShouldBe(
            Sidecars.HashOf(new SqlFile("Queries/Posts.sql", WithMarker), "ListPosts", SqlDialect.PostgreSql)
        );
        query.Hash.ShouldNotBe(
            Sidecars.HashOf(new SqlFile("Queries/Posts.sql", WithoutMarker), "ListPosts", SqlDialect.PostgreSql)
        );
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
        _ = project.AddSource("Repository.cs", Plans.TypeNamed("Repository", "Source = \"Queries/b.SQL\""));
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

    // The file owns its output, so the two types agree on what it needs.
    [Theory]
    [InlineData("models", true)]
    [InlineData("sql", false)]
    public void Plan_TwoTypesThatClaimOneFile_NeedWhatItsHeaderNeeds(string output, bool needsEntry)
    {
        var project = Postgres();
        _ = project.AddSource("A.cs", Plans.TypeNamed("A", "Source = \"Q\""));
        _ = project.AddSource("B.cs", Plans.TypeNamed("B", "Source = \"Q\""));
        _ = project.AddSql("Q/One.sql", "-- output: " + output + "\n\nSELECT 1;\n");

        var file = Plans.Result(project).Plan.Files.ShouldHaveSingleItem();

        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBe(needsEntry);
    }

    // The order the project compiles its files in, which is not the order of their paths here.
    [Fact]
    public void Plan_TwoTypesThatClaimOneFile_HasBothClaimsInTheOrderOfTheirSourceFiles()
    {
        var project = Postgres();
        var first = project.AddSource("B.cs", Plans.TypeNamed("B", "Source = \"Q\""));
        var second = project.AddSource("A.cs", Plans.TypeNamed("A", "Source = \"Q/One.sql\""));
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var file = Plans.Result(project).Plan.Files.ShouldHaveSingleItem();

        file.Claims.Select(claim => claim.SourcePath).ShouldBe([first, second]);
    }

    [Fact]
    public void Plan_FileThatNoTypeClaims_IsNotReadThoughItDoesNotParse()
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"Queries\""));
        _ = project.AddSql("Queries/One.sql", "SELECT 1;");
        _ = project.AddSql("Migrations/Broken.sql", "-- name: Broken\nSELECT 'open\n");

        var result = Plans.Result(project);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.Count.ShouldBe(1);
    }

    // The build reports a Source that matches nothing.
    [Theory]
    [InlineData("Nowhere")]
    [InlineData("Nowhere.sql")]
    [InlineData("../../../../../../../../../../../../../../../../../../../../../../../../..")]
    public void Plan_PathThatNamesNothing_PlansNothingAndReportsNothing(string path)
    {
        var project = Postgres();
        _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries", $"Source = \"{path}\""));
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
        _ = first.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../Shared\""));
        first.ListSql(shared);
        var second = Postgres("B");
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../Shared\""));
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
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../A/X.sql\""));
        second.ListSql(shared);
        _ = second.AddSql("Lone.sql", "SELECT 3;");

        var result = Plans.Result(first, second);

        result.Plan.Files.Count(file => file.Path == shared).ShouldBe(1);
        result.Unclaimed.ShouldBe([(first.ProjectPath, 0), (second.ProjectPath, 1)]);
    }

    // The two manifests of one project, written for net8.0 and for net10.0, with no settings file of their own.
    private (TestProject Net8, TestProject Net10) TwoFrameworks()
    {
        var net8 = new TestProject(_folder) { TargetFramework = "net8.0" };
        var net10 = new TestProject(_folder) { TargetFramework = "net10.0" };
        return (net8, net10);
    }

    private string WriteSettings(string relativePath, string json) => _folder.WriteFile(relativePath, json);

    [Fact]
    public void Plan_FileClaimedForTheSecondFrameworkAlone_IsPlanned()
    {
        var (net8, net10) = TwoFrameworks();
        var root = WriteSettings(
            "App/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres"}"""
        );
        net8.ListSettings(root);
        net10.ListSettings(root);
        _ = net8.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = net10.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        _ = net10.AddSource("Extra.cs", Plans.TypeNamed("Extra", "Source = \"Extra\""));
        var extra = net10.AddSql("Extra/Y.sql", "SELECT 1;");

        var result = Plans.Result(net8, net10);

        result.Errors.ShouldBeEmpty();
        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(extra);
        file.ProjectPath.ShouldBe(net10.ProjectPath);
    }

    // The settings file of net10.0 gives the folder an output that needs an entry, and net8.0's chain does not.
    [Fact]
    public void Plan_QueryNeedsAnEntryForOneFrameworkAlone_NeedsAnEntry()
    {
        var (net8, net10) = TwoFrameworks();
        var root = WriteSettings(
            "App/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres", "output": "sql"}"""
        );
        var folder = WriteSettings(
            "App/Q/sqlsource.json", /*lang=json,strict*/
            """{"output": "models"}"""
        );
        net8.ListSettings(root);
        net10.ListSettings(root);
        net10.ListSettings(folder);
        _ = net8.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"Q\""));
        _ = net10.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"Q\""));
        net8.ListSql(net8.AddSql("Q/One.sql", "SELECT 1;"));
        net10.ListSql(net8.PathOf("Q/One.sql"));

        var query = Plans.Result(net8, net10).Plan.Files.ShouldHaveSingleItem().Queries.ShouldHaveSingleItem();

        query.NeedsEntry.ShouldBeTrue();
        query.Database.ShouldBe("postgres");
    }

    [Fact]
    public void Plan_FileListedByBothFrameworks_IsPlannedOnceWithEachClaimOnce()
    {
        var (net8, net10) = TwoFrameworks();
        var root = WriteSettings(
            "App/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres"}"""
        );
        foreach (var project in new[] { net8, net10 })
        {
            project.ListSettings(root);
            _ = project.AddSource("Queries.cs", Plans.TypeNamed("Queries"));
        }

        _ = net8.AddSql("One.sql", "SELECT 1;");
        net10.ListSql(net8.PathOf("One.sql"));

        var file = Plans.Result(net8, net10).Plan.Files.ShouldHaveSingleItem();

        file.Claims.Count.ShouldBe(1);
    }

    [Fact]
    public void Plan_SourceNotALiteralInAFileOfBothFrameworks_ReportsSQLSRC208Once()
    {
        var (net8, net10) = TwoFrameworks();
        var root = WriteSettings(
            "App/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres"}"""
        );
        var source = net8.AddSource("A.cs", Plans.TypeNamed("A", "Source = Folder"));
        net10.ListSource(source);
        net8.ListSettings(root);
        net10.ListSettings(root);

        var result = Plans.Result(net8, net10);

        result.Errors.Count(error => error.Descriptor.Id == "SQLSRC208").ShouldBe(1);
    }

    [Fact]
    public void Plan_UnclaimedFiles_AreCountedOncePerProject()
    {
        var (net8, net10) = TwoFrameworks();
        var root = WriteSettings(
            "App/sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres"}"""
        );
        net8.ListSettings(root);
        net10.ListSettings(root);
        _ = net8.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"Claimed\""));
        _ = net10.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"Claimed\""));
        _ = net10.AddSource("Late.cs", Plans.TypeNamed("Late", "Source = \"Late\""));
        var neither = net8.AddSql("Neither/N.sql", "SELECT 1;");
        var both = net8.AddSql("Claimed/C.sql", "SELECT 1;");
        var late = net8.AddSql("Late/L.sql", "SELECT 1;");
        foreach (var path in new[] { neither, both, late })
        {
            net10.ListSql(path);
        }

        var result = Plans.Result(net8, net10);

        // Late/L.sql is claimed by net10.0 alone, Neither/N.sql by neither.
        result.Unclaimed.ShouldBe([(net8.ProjectPath, 1)]);
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
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../Shared\""));
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
    public void Plan_DatabaseOfTheChain_IsTheQuerysDatabase()
    {
        var project = Postgres();
        project.Settings["database"] = "billing";
        _ = project.AddSource("Queries.cs", Plans.Type);
        _ = project.AddSql("Q.sql", "-- name: One\nSELECT 1;\n\n-- name: Two\n-- database: other\nSELECT 2;\n");

        OnlyFile(project).Queries.Select(query => query.Database).ShouldBe(["billing", "other"]);
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
        _ = first.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../Shared\""));
        first.ListSql(shared);
        var second = Postgres("B");
        second.ListSettings(farther);
        second.ListSettings(nearer);
        _ = second.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../Shared\""));
        second.ListSql(shared);

        var file = Plans.Result(first, second).Plan.Files.ShouldHaveSingleItem();

        file.ProjectPath.ShouldBe(first.ProjectPath);
        file.Queries.ShouldHaveSingleItem().NeedsEntry.ShouldBeTrue();
    }

    // The plan of a run on the first projects, with the last ones left out by --project.
    private static RunPlanResult WithOthers(TestProject[] run, params TestProject[] others) =>
        RunPlanner.Plan(
            [.. run.Select(project => project.Manifest())],
            RunFilters.None,
            TestContext.Current.CancellationToken,
            [.. others.Select(project => project.Manifest())]
        );

    // A shared file, which the run's project A finds the output sql for, and the left-out project B finds models for,
    // by a nearer settings file that only B lists.  Its first query says models itself.
    private (TestProject Run, TestProject Other, string Shared) SharedWithALeftOutProject()
    {
        var farther = _folder.WriteFile(
            "sqlsource.json", /*lang=json,strict*/
            """{"dialect": "postgres", "output": "sql"}"""
        );
        var nearer = _folder.WriteFile(
            "Shared/sqlsource.json", /*lang=json,strict*/
            """{"output": "models"}"""
        );
        var shared = _folder.WriteFile(
            "Shared/One.sql",
            "-- name: Both\n-- output: models\nSELECT 1;\n\n-- name: OtherOnly\nSELECT 2;\n"
        );
        var run = Postgres("A");
        run.ListSettings(farther);
        _ = run.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../Shared\""));
        run.ListSql(shared);
        var other = Postgres("B");
        other.ListSettings(farther);
        other.ListSettings(nearer);
        _ = other.AddSource("Queries.cs", Plans.TypeNamed("Queries", "Source = \"../Shared\""));
        other.ListSql(shared);
        return (run, other, shared);
    }

    [Fact]
    public void Plan_QueryThatOnlyALeftOutProjectNeeds_NeedsAnEntryAndIsNotSelected()
    {
        var (run, other, _) = SharedWithALeftOutProject();

        var result = WithOthers([run], other);

        var file = result.Plan.Files.ShouldHaveSingleItem();
        file.ProjectPath.ShouldBe(run.ProjectPath);
        file.Queries.Select(query => (query.Query.Name, query.NeedsEntry, query.IsSelected))
            .ShouldBe([("Both", true, true), ("OtherOnly", true, false)]);
        file.Claims.Select(claim => claim.ProjectPath).ShouldBe([run.ProjectPath, other.ProjectPath]);
        result.Unclaimed.ShouldBe([(run.ProjectPath, 0)]);
    }

    // A filter narrows the queries of the run's projects, and selects none that only the left-out project needs.
    [Fact]
    public void Plan_QueryThatOnlyALeftOutProjectNeeds_IsNotSelectedByAFileFilter()
    {
        var (run, other, shared) = SharedWithALeftOutProject();
        var filters = RunFilters.Create([new UsageValue(null, shared, 1)], [], _folder.Path, [.. run.Manifest().Files]);

        var plan = RunPlanner
            .Plan([run.Manifest()], filters, TestContext.Current.CancellationToken, [other.Manifest()])
            .Plan;

        plan.Files.ShouldHaveSingleItem().Queries.Select(query => query.IsSelected).ShouldBe([true, false]);
    }

    [Fact]
    public void Plan_FileThatOnlyALeftOutProjectClaims_IsNotPlanned()
    {
        var (run, other, shared) = SharedWithALeftOutProject();
        _ = other.AddSource("Own.cs", Plans.TypeNamed("Own"));
        _ = other.AddSql("Own.sql", "-- name: Own\nSELECT 3;\n");

        var files = WithOthers([run], other).Plan.Files;

        files.Select(file => file.Path).ShouldBe([shared]);
    }

    // The left-out project's errors are its own build's: a Source that is no literal, and its own settings file's
    // output that is not valid, which a file it lists reaches.
    [Fact]
    public void Plan_LeftOutProjectsErrors_AreNotReported()
    {
        var (run, other, _) = SharedWithALeftOutProject();
        _ = other.AddSource("Broken.cs", Plans.TypeNamed("Broken", "Source = Folder"));
        other.Settings["output"] = "nonsense";
        _ = other.AddSql("Own.sql", "-- name: Own\nSELECT 3;\n");

        var result = WithOthers([run], other);

        result.Errors.ShouldBeEmpty();
        result.Plan.Files.ShouldHaveSingleItem().Queries[1].NeedsEntry.ShouldBeTrue();
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
