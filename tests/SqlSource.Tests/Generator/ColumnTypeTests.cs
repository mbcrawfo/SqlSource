// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Generation.Types;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// A column given a C# type by its source, the column of a table or view it is read from, at each level of the
// columnTypes, and by its query's -- column: marker, over the type override at any level.  Each run is at
// Models under postgres on Npgsql 10, with an entry written for each query's current hash, unless it says otherwise.
public sealed class ColumnTypeTests
{
    private const string TheProjectFilePlace =
        "the columnTypes of the settings file " + GeneratorHarness.ProjectSettingsPath;

    private const string TheFolderFilePlace = "the columnTypes of the settings file " + FolderSettingsPath;

    private const string TheHeaderPlace = "a -- column: marker in the file header";

    private const string TheQueryPlace = "a -- column: marker of the query";

    // The settings file of the folder of GeneratorHarness.UsersPath.
    private const string FolderSettingsPath = "/app/Repo/sqlsource.json";

    private const string UserId = "global::App.UserId";

    private const string DtoModel = "App.ListPostsDto-Model.g.cs";

    // App.UserId and App.OtherId, over long, with UserId's converters.
    private const string IdsSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public readonly record struct UserId(long Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(long value) => new(value);

                [SqlSourceConverter]
                public static long ToDatabase(UserId id) => id.Value;
            }

            public readonly record struct OtherId(long Value);
        }
        """;

    private const string ListPosts = "-- name: ListPosts\nSELECT id FROM users;\n";

    private static readonly PostgresType Int8 = Catalog("int8") with { Name = "bigint" };

    private static readonly SidecarOrigin UsersId = new("public", "users", "id");

    // What the base map reads int8 as, which a type that a column type chose carries.
    private static readonly DatabaseType LongBase = new("long", true, false);

    [Fact]
    public void Run_ProjectFilesColumnType_TypesItsSourceColumn()
    {
        var run = RunPosts(ListPosts, UsersIdIs("UserId"));

        run.ShouldBeClean();
        StepOutputs
            .ColumnsOf(run)
            .ShouldBe([
                new PropertyType(
                    UserId,
                    true,
                    true,
                    EquatableArray<string>.Empty,
                    "int8",
                    TheProjectFilePlace,
                    LongBase,
                    "users.id"
                ),
            ]);
        run.Sources[DtoModel].ShouldContain("(global::App.UserId Id)");
    }

    [Fact]
    public void Run_FolderFilesColumnType_OverTheProjectsFile()
    {
        var run = RunPosts(
            ListPosts,
            UsersIdIs("OtherId"),
            folderFile: /*lang=json,strict*/
            """{"public.users.id": "UserId"}"""
        );

        run.ShouldBeClean();
        var column = StepOutputs.ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe(UserId);
        column.Place.ShouldBe(TheFolderFilePlace);
        column.Mapped.ShouldBe("public.users.id");
    }

    // Two keys that both match the column, so the places decide, not a key replaced by its own: the header's, though
    // the folder's file's names the schema.
    [Fact]
    public void Run_HeaderColumnType_OverTheFoldersFile()
    {
        var run = RunPosts(
            "-- column: users.id -> UserId\n\n" + ListPosts,
            folderFile: /*lang=json,strict*/
            """{"public.users.id": "OtherId"}"""
        );

        run.ShouldBeClean();
        var column = StepOutputs.ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe(UserId);
        column.Place.ShouldBe(TheHeaderPlace);
    }

    [Fact]
    public void Run_QueryMarker_OverTheHeader()
    {
        var run = RunPosts(
            "-- column: users.id -> OtherId\n\n-- name: ListPosts\n-- column: id -> UserId\nSELECT id FROM users;\n"
        );

        run.ShouldBeClean();
        StepOutputs
            .ColumnsOf(run)
            .ShouldBe([
                new PropertyType(
                    UserId,
                    true,
                    true,
                    EquatableArray<string>.Empty,
                    "int8",
                    TheQueryPlace,
                    LongBase,
                    "id"
                ),
            ]);
    }

    // The tool names the column as the database returns it, and the marker's name is found ignoring case.
    [Fact]
    public void Run_QueryMarker_MatchesItsColumnIgnoringCase()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListPosts\n-- column: ID -> UserId\nSELECT id FROM users;\n"
        );

        var run = Run(IdsSource, [file, Sidecars.Of(file, PostsEntry(file, Column("Id", Int8, origin: UsersId)))]);

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldHaveSingleItem().Text.ShouldBe(UserId);
    }

    // -> default takes the column out of the column types of every level, and the type override gives its type.
    [Fact]
    public void Run_QueryMarkerDefault_FreesTheColumnForTheTypeOverride()
    {
        var run = RunPosts(
            "-- name: ListPosts\n-- column: id -> default\nSELECT id FROM users;\n",
            UsersIdIs("UserId"),
            typeOverrides: /*lang=json,strict*/
            """{"int8": "long"}"""
        );

        run.ShouldBeClean();
        var column = StepOutputs.ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe("long");
        column.Place.ShouldBe("the typeOverrides of the settings file " + GeneratorHarness.ProjectSettingsPath);
    }

    // Naming a column is more specific than naming a type, so a column type at any level wins over a type override
    // at any level.
    [Fact]
    public void Run_ColumnType_WinsOverATypeOverrideOfAHigherPlace()
    {
        var run = RunPosts(
            "-- name: ListPosts\n-- type-override: int8=long\nSELECT id FROM users;\n",
            UsersIdIs("UserId")
        );

        run.ShouldBeClean();
        var column = StepOutputs.ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe(UserId);
        column.Place.ShouldBe(TheProjectFilePlace);
    }

    // A marker that says only the nullability leaves the column to the column types, which keep the marker's null.
    [Fact]
    public void Run_NullabilityOnlyMarker_KeepsTheColumnType()
    {
        var run = RunPosts("-- name: ListPosts\n-- column: id null\nSELECT id FROM users;\n", UsersIdIs("UserId"));

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldHaveSingleItem().Place.ShouldBe(TheProjectFilePlace);
        run.Sources[DtoModel].ShouldContain("(global::App.UserId? Id)");
    }

    // An expression has no origin, and an origin without a table names no source.
    [Fact]
    public void Run_ColumnWithoutOrigin_IsUntouchedByAColumnType()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListPosts\nSELECT id + 0, id FROM users;\n");
        var entry = PostsEntry(
            file,
            Column("total", Int8),
            Column("id", Int8, origin: new SidecarOrigin("public", null, "id"))
        );

        var run = Run(IdsSource, [file, Sidecars.Of(file, entry)], Settings(UsersIdIs("UserId")));

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).Select(column => column.Text).ShouldBe(["long", "long"]);
    }

    // A column read through a view has the view's origin, so an entry of the table under it does not match it.
    [Fact]
    public void Run_ViewColumn_IsTheViewsKey()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListPosts\nSELECT id FROM user_summaries;\n");
        var entry = PostsEntry(file, Column("id", Int8, origin: new SidecarOrigin("public", "user_summaries", "id")));

        var ofTheTable = Run(IdsSource, [file, Sidecars.Of(file, entry)], Settings(UsersIdIs("UserId")));
        var ofTheView = Run(
            IdsSource,
            [file, Sidecars.Of(file, entry)],
            Settings( /*lang=json,strict*/
                """{"user_summaries.id": "UserId"}"""
            )
        );

        ofTheTable.ShouldBeClean();
        StepOutputs.ColumnsOf(ofTheTable).ShouldHaveSingleItem().Text.ShouldBe("long");
        ofTheView.ShouldBeClean();
        StepOutputs.ColumnsOf(ofTheView).ShouldHaveSingleItem().Text.ShouldBe(UserId);
    }

    // The nullability stays the column's: a column that can be null is a UserId?, and a type-only marker may name a
    // column whose ! mark says it is not null.
    [Fact]
    public void Run_ColumnType_KeepsTheNullability()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListPosts\n-- column: author -> UserId\nSELECT id, author_id AS \"author!\" FROM users;\n"
        );
        var entry = PostsEntry(
            file,
            Column("id", Int8, nullable: true, origin: UsersId),
            Column("author!", Int8, nullable: true)
        );

        var run = Run(IdsSource, [file, Sidecars.Of(file, entry)], Settings(UsersIdIs("UserId")));

        run.ShouldBeClean();
        run.Sources[DtoModel].ShouldContain("(global::App.UserId? Id, global::App.UserId Author)");
    }

    // A value of the project's file serves the files of both engines.
    [Fact]
    public void Run_ProjectFilesColumnType_MapsBothEngines()
    {
        var posts = new SqlFile(GeneratorHarness.UsersPath, ListPosts);
        var orders = new SqlFile(
            "/app/Repo/Orders.sql",
            "-- dialect: mssql\n-- name: ListOrders\nSELECT id FROM users;\n"
        );
        var ordersEntry = MethodRuns.SqlServerEntry(
            orders,
            "ListOrders",
            [],
            [TestSidecars.Column(0, "id", TestSidecars.SqlServer("bigint", 8, 19), origin: new("dbo", "users", "id"))]
        );

        var run = Run(
            IdsSource,
            [
                posts,
                Sidecars.Of(posts, PostsEntry(posts, Column("id", Int8, origin: UsersId))),
                orders,
                Sidecars.Of(orders, ordersEntry),
            ],
            Settings(UsersIdIs("UserId")),
            [NpgsqlReferences.Npgsql10, SqlClientReferences.SqlClient7]
        );

        run.ShouldBeClean();
        run.Sources[DtoModel].ShouldContain("(global::App.UserId Id)");
        run.Sources["App.ListOrdersDto-Model.g.cs"].ShouldContain("(global::App.UserId Id)");
    }

    // The message names the key that chose the type, and the place that wrote it.
    [Theory]
    [InlineData("projectFile", "users.id", TheProjectFilePlace)]
    [InlineData("folderFile", "users.id", TheFolderFilePlace)]
    [InlineData("header", "users.id", TheHeaderPlace)]
    [InlineData("query", "id", TheQueryPlace)]
    public void Run_ColumnTypeMissingItsType_IsSqlsrc250AtItsPlace(string level, string key, string place)
    {
        var text = level switch
        {
            "header" => "-- column: users.id -> NoSuchType\n\n" + ListPosts,
            "query" => "-- name: ListPosts\n-- column: id -> NoSuchType\nSELECT id FROM users;\n",
            _ => ListPosts,
        };
        var line = level == "header" ? 3 : 1;

        var run = RunPosts(
            text,
            level == "projectFile" ? UsersIdIs("NoSuchType") : null,
            level == "folderFile" ? UsersIdIs("NoSuchType") : null
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC250 {GeneratorHarness.UsersPath}({line},10)-({line},19): '{key}' is mapped to 'NoSuchType' for "
                + $"column 'id', in {place}, and no type of that name is found, or more than one that this project "
                + "can see.  Correct the name, with its namespace and type arguments, or for a name that two "
                + "references have, map to a type of this project.",
        ]);
    }

    // An array mapped to an array of a foreign type is read as the base map's array and converted element by element.
    [Fact]
    public void Run_ColumnTypeOfAnArrayOfAForeignType_ConvertsByElement()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListPosts\nSELECT tag_ids FROM posts;\n");
        var column = Column("tag_ids", ArrayOf(Catalog("int8"), "_int8"), origin: new("public", "posts", "tag_ids"));

        var run = MethodRuns.Run(
            file,
            [PostsEntry(file, column)],
            IdsSource,
            settings: Settings( /*lang=json,strict*/
                """{"posts.tag_ids": "UserId[]"}"""
            )
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                "global::System.Array.ConvertAll<long?, global::App.UserId>(reader.GetFieldValue<long?[]>(0), "
                    + "static element => element is { } item ? global::App.UserId.FromDatabase(item) : "
            );
    }

    [Fact]
    public void Run_ColumnType_UsesTheConverter()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, ListPosts);

        var run = MethodRuns.Run(
            file,
            [PostsEntry(file, Column("id", Int8, origin: UsersId))],
            IdsSource,
            settings: Settings(UsersIdIs("UserId"))
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain("(global::App.UserId.FromDatabase(reader.GetFieldValue<long>(0)))");
    }

    private static SidecarEntry PostsEntry(SqlFile file, params SidecarColumn[] columns) =>
        Entry(file, "ListPosts", [], columns);

    // The column types of the one entry that maps users.id to the type, as a settings file writes them.
    private static string UsersIdIs(string type) => GeneratorHarness.Json(new JsonObject { ["users.id"] = type });

    // The settings of the column types and the type overrides, each a JSON object of its entries, or none.
    private static string Settings(string? columnTypes, string? typeOverrides = null)
    {
        var json = new JsonObject();
        if (columnTypes is not null)
        {
            json["columnTypes"] = JsonNode.Parse(columnTypes);
        }

        if (typeOverrides is not null)
        {
            json["typeOverrides"] = JsonNode.Parse(typeOverrides);
        }

        return GeneratorHarness.Json(json);
    }

    // ListPosts of this text, whose one column, id, is an int8 read from public.users.id, with the column types of
    // the project's file and of the folder's file given, each a JSON object of its entries, and the project's file's
    // type overrides.
    private static GeneratorRun RunPosts(
        string text,
        string? projectFile = null,
        string? folderFile = null,
        string? typeOverrides = null
    )
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        SqlFile[] folder = folderFile is null
            ? []
            : [GeneratorHarness.SettingsFile(FolderSettingsPath, Settings(folderFile))];
        return Run(
            IdsSource,
            [file, Sidecars.Of(file, PostsEntry(file, Column("id", Int8, origin: UsersId))), .. folder],
            Settings(projectFile, typeOverrides)
        );
    }
}
