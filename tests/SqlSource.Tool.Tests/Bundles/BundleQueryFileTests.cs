using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleQueryFileTests
{
    // Where the bundle's errors would be.  Nothing is read there.
    private const string BundlePath = "/work/report.zip";

    [Fact]
    public void Of_AQueryOfAFileOfSeveral_HoldsItAlone()
    {
        using var folder = new TempFolder();
        var (file, text) = PlannedFileOf(
            Plans.Postgres(folder),
            "Users.sql",
            """
            -- name: First
            SELECT 1;

            -- name: GetUser
            SELECT id FROM users WHERE id = @id;

            -- name: Last
            SELECT 2;

            """
        );

        BundleQueryFile
            .Of(file.Queries[1], text, file.Dialect)
            .ShouldBe(
                """
                -- dialect: postgres
                -- database: postgres
                -- name: GetUser
                SELECT id FROM users WHERE id = @id;


                """
            );
    }

    [Fact]
    public void Of_DialectAndDatabaseFromSettings_WritesThemAsMarkers()
    {
        using var folder = new TempFolder();
        var project = Plans.Postgres(folder);
        project.Settings["database"] = "app";
        var (file, text) = PlannedFileOf(
            project,
            "Users.sql",
            """
            -- name: GetUser -> one
            SELECT id FROM users WHERE id = @id;

            """
        );

        var written = BundleQueryFile.Of(file.Queries[0], text, file.Dialect);

        written.ShouldBe(
            """
            -- dialect: postgres
            -- database: app
            -- name: GetUser -> one
            SELECT id FROM users WHERE id = @id;

            """
        );
        var (query, dialect) = PlanOf(written);
        dialect.ShouldBe(SqlDialect.PostgreSql);
        query.Database.ShouldBe("app");
        query.Query.Shape.ShouldBe(ResultShape.One);
    }

    [Fact]
    public void Of_DropsEveryOtherMarker()
    {
        using var folder = new TempFolder();
        var (file, text) = PlannedFileOf(
            Plans.Postgres(folder),
            "Users.sql",
            """
            -- output: models
            -- generator: keep-comments
            -- model-namespace: App.Models

            -- name: GetUser
            -- summary: Finds a user.
            -- param: @id int -> UserId
            -- column: name not null
                -- output: codegen
            -- output-model: UserRow
            -- token: {{order:id}}
            -- token-ignore: literal
            SELECT id, name, '{{literal}}' AS template FROM users WHERE id = @id ORDER BY {{order}};

            """
        );

        BundleQueryFile
            .Of(file.Queries[0], text, file.Dialect)
            .ShouldBe(
                """
                -- dialect: postgres
                -- database: postgres
                -- name: GetUser
                -- param: @id int
                -- token: {{order:id}}
                -- token-ignore: literal
                SELECT id, name, '{{literal}}' AS template FROM users WHERE id = @id ORDER BY {{order}};

                """
            );
    }

    // A query of a file of several, one of a file with no name marker, one whose parameters a marker types, makes
    // nullable and gives a C# type, one with a comment and line ends of CR LF.
    [Theory]
    [InlineData(
        "Users.sql",
        "-- name: A\nSELECT 1;\n\n-- name: B\n-- param: @id int not null -> UserId\nSELECT @id, @name;\n",
        1
    )]
    [InlineData("GetUser.sql", "-- summary: Finds a user.\nSELECT id FROM users WHERE id = @id;\n", 0)]
    [InlineData(
        "Users.sql",
        "-- name: Find -> many\r\n-- param: @name text null\r\n-- param: @extra -> json Extra\r\n"
            + "-- token: {{where:}}\r\nSELECT id -- the key\r\n"
            + "FROM users WHERE name = @name AND extra = @extra {{where}};\r\n",
        0
    )]
    public void Of_ReadsBackToTheSameSampleSqlAndParameters(string fileName, string sql, int index)
    {
        using var folder = new TempFolder();
        var (file, text) = PlannedFileOf(Plans.Postgres(folder), fileName, sql);
        var original = file.Queries[index];

        var (query, _) = PlanOf(BundleQueryFile.Of(original, text, file.Dialect));

        query.Query.Name.ShouldBe(original.Query.Name);
        query.Query.Shape.ShouldBe(original.Query.Shape);
        SampleSql.Build(query.Query, file.Dialect).ShouldBe(SampleSql.Build(original.Query, file.Dialect));
        // The C# type and the JSON opt-in describe nothing to the database, and query.sql drops them.
        SqlQueryParameter[] parameters = [.. query.Query.Parameters];
        parameters.ShouldBe([
            .. original.Query.Parameters.Select(static parameter => parameter with { ClrType = null, IsJson = false }),
        ]);
        query.Hash.ShouldBe(original.Hash);
        query.Database.ShouldBe(original.Database);
    }

    private static (PlannedFile File, string Text) PlannedFileOf(TestProject project, string fileName, string sql)
    {
        var path = project.AddSql(fileName, sql);
        var result = Plans.Result(project);
        result.Errors.Select(static error => error.Descriptor.Id).ShouldBeEmpty();
        var file = result.Plan.Files.Single();
        file.State.ShouldBe(PlannedFileState.Ready);
        return (file, File.ReadAllText(path));
    }

    private static (PlannedQuery Query, SqlDialect Dialect) PlanOf(string queryFile)
    {
        var planned = BundleReplay.Plan(
            new Bundle(Info: null, queryFile, Capture: "{}", Expected: null, []),
            "postgres",
            BundlePath,
            TestContext.Current.CancellationToken
        );
        planned.Failure.ShouldBeNull();
        return planned.Value;
    }
}
