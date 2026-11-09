using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Parsing.Markers;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleQueryFileTests
{
    // One valid marker line of each kind, under PostgreSQL.  A kind missing from it fails the theory by name.
    private static readonly Dictionary<SqlMarkerKind, string> MarkerLines = new()
    {
        [SqlMarkerKind.Name] = "-- name: Second",
        [SqlMarkerKind.Summary] = "-- summary: Finds a user.",
        [SqlMarkerKind.GeneratorParameters] = "-- generator: keep-comments",
        [SqlMarkerKind.Dialect] = "-- dialect: postgres",
        [SqlMarkerKind.Token] = "-- token: {{t:x}}",
        [SqlMarkerKind.TokenIgnore] = "-- token-ignore: t",
        [SqlMarkerKind.Param] = "-- param: @id int not null",
        [SqlMarkerKind.Database] = "-- database: postgres",
        [SqlMarkerKind.Output] = "-- output: models",
        [SqlMarkerKind.InputModelSuffix] = "-- input-model-suffix: Args",
        [SqlMarkerKind.OutputModelSuffix] = "-- output-model-suffix: Row",
        [SqlMarkerKind.ModelNamespace] = "-- model-namespace: App.Models",
        [SqlMarkerKind.InputModelType] = "-- input-model-type: class",
        [SqlMarkerKind.OutputModelType] = "-- output-model-type: class",
        [SqlMarkerKind.InputModel] = "-- input-model: GetArgs",
        [SqlMarkerKind.OutputModel] = "-- output-model: GetRow",
        [SqlMarkerKind.CollectionType] = "-- collection-type: List",
        [SqlMarkerKind.TypeOverride] = "-- type-override: int8=App.UserId",
        [SqlMarkerKind.Column] = "-- column: id not null",
        [SqlMarkerKind.LibraryTypes] = "-- library-types: nodatime=off",
        [SqlMarkerKind.MemberLocation] = "-- member-location: nested-class",
        [SqlMarkerKind.Visibility] = "-- visibility: public",
        [SqlMarkerKind.NestedClass] = "-- nested-class: Users",
    };

    // By name, since the enum is internal.
    public static TheoryData<string> Kinds => [.. Enum.GetNames<SqlMarkerKind>()];

    // The three places that decide which markers a bundle's query.sql holds: what BundleQueryFile writes, what the
    // renaming of a query file renames, and what the token check reads.  They agree, kind by kind.
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Of_MarkerOfEveryKind_IsHeldExactlyWhereTheRenamerAndTheCheckReadIt(string kindName)
    {
        var kind = Enum.Parse<SqlMarkerKind>(kindName);
        MarkerLines.ShouldContainKey(kind, $"{kindName} has no sample line");
        var word = "-- " + SqlMarkerReader.WordOf(kind) + ":";

        using var folder = new TempFolder();
        var (file, text) = PlannedFileOf(Plans.Postgres(folder), "Q.sql", FileOf(kind, "Get"));
        var written = BundleQueryFile.Of(file.Queries[0], text, file.Dialect);
        var rules = SqlDialectRules.For(file.Dialect);
        var lexed = new SqlLexer(written, rules).ReadToEnd();
        var held = lexed.Lexemes.Any(lexeme =>
            SqlMarkerReader.Read(written, lexeme) is { } found && found.Kind == kind
        );

        var engine = new FakeEngineRenaming(SqlDialect.PostgreSql);
        var mapping = new NameMapping(RenamingKey.Of(new byte[RenamingKey.Length]), engine.Fold, engine.Spelled);
        var refusals = new List<RenamingRefusal>();
        var renamer = new SqlRenamer(
            engine,
            mapping,
            new HashSet<string>(["id"], SqlParameterName.Comparer),
            RenamingCopies.None
        );
        var renamedText = renamer.RenameQueryFile(FileOf(kind, "Get"), refusals.Add).Text;
        var renamed = !refusals.Any(refusal => refusal.Token == word);

        var query = mapping.Name("Get", quoted: false, NameClass.Query);
        var checkedAsHeld = TokenCheck.Check(engine, mapping, [], FileOf(kind, query), expected: null)?.Token != word;

        renamed.ShouldBe(held);
        checkedAsHeld.ShouldBe(held);
        held.ShouldBe(HeldKinds.Contains(kind));
        if (held)
        {
            // A held marker is renamed and passes the check whole: an arm that refuses its value would refuse every
            // bundle that holds it, by the value and never by the marker's word.
            refusals.ShouldBeEmpty();
            TokenCheck.Check(engine, mapping, [], renamedText, expected: null).ShouldBeNull();
        }
    }

    // A file of one query with the kind's line where the generator takes it: a marker of the file header before the
    // query's name, the second name after the query's SQL, any other inside the query.
    private static string FileOf(SqlMarkerKind kind, string name)
    {
        var line = MarkerLines[kind];
        // A token needs a use, and an input model a parameter to hold.
        var body = "SELECT 1";
        if (kind is SqlMarkerKind.Token or SqlMarkerKind.TokenIgnore)
        {
            body = "SELECT 1 AS {{t}}";
        }
        else if (kind == SqlMarkerKind.InputModel)
        {
            body = "SELECT @id";
        }

        if (kind == SqlMarkerKind.Name)
        {
            return $"-- name: {name}\n{body}\n{line}\nSELECT 2\n";
        }

        return HeaderKinds.Contains(kind) ? $"{line}\n-- name: {name}\n{body}\n" : $"-- name: {name}\n{line}\n{body}\n";
    }

    private static readonly SqlMarkerKind[] HeaderKinds =
    [
        SqlMarkerKind.Dialect,
        SqlMarkerKind.InputModelSuffix,
        SqlMarkerKind.OutputModelSuffix,
        SqlMarkerKind.ModelNamespace,
    ];

    private static readonly SqlMarkerKind[] HeldKinds =
    [
        SqlMarkerKind.Name,
        SqlMarkerKind.Dialect,
        SqlMarkerKind.Database,
        SqlMarkerKind.Param,
        SqlMarkerKind.Token,
        SqlMarkerKind.TokenIgnore,
    ];

    [Theory]
    [InlineData('@', "id", null, null, "@id")]
    [InlineData('@', "id", "int", null, "@id int")]
    [InlineData('@', "id", "int", true, "@id int null")]
    [InlineData('@', "id", null, false, "@id not null")]
    public void WriteParam_Declaration_IsWrittenAsTheGeneratorReadsIt(
        char prefix,
        string name,
        string? type,
        bool? nullable,
        string written
    ) => BundleQueryFile.WriteParam(prefix, name, type, nullable).ShouldBe(written);

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
    public void Of_QueryWithGeneratorMarkers_DropsEveryOtherMarker()
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

    // A query of a file of several, one of a file with no name marker, and one whose parameters a marker types, makes
    // nullable and gives a C# type, with a comment and line ends of CR LF.
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
            BundleInfos.Path,
            TestContext.Current.CancellationToken
        );
        planned.Failure.ShouldBeNull();
        return planned.Value;
    }
}
