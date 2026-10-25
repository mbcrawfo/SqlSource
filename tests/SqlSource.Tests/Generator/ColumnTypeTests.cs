// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// A column given a C# type by its source, the column of a table or view it is read from, at each level of
// SqlSourceColumnTypes, and by its query's -- column: marker, over the type override at any level.  Each run is at
// Models under postgres on Npgsql 10, with an entry written for each query's current hash, unless it says otherwise.
public sealed class ColumnTypeTests
{
    private const string ThePropertyPlace = "the property SqlSourceColumnTypes";

    private const string TheMetadataPlace = "the metadata SqlSourceColumnTypes of the file";

    private const string TheHeaderPlace = "a -- column: marker in the file header";

    private const string TheQueryPlace = "a -- column: marker of the query";

    private const string ColumnTypes = "SqlSourceColumnTypes";

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
    public void Run_PropertyColumnType_TypesItsSourceColumn()
    {
        var run = RunPosts(ListPosts, "users.id=UserId");

        run.ShouldBeClean();
        ColumnsOf(run)
            .ShouldBe([
                new PropertyType(
                    UserId,
                    true,
                    true,
                    EquatableArray<string>.Empty,
                    "int8",
                    ThePropertyPlace,
                    LongBase,
                    "users.id"
                ),
            ]);
        run.Sources[DtoModel].ShouldContain("(global::App.UserId Id)");
    }

    [Fact]
    public void Run_MetadataColumnType_OverTheProperty()
    {
        var run = RunPosts(ListPosts, "users.id=OtherId", metadata: "public.users.id=UserId");

        run.ShouldBeClean();
        var column = ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe(UserId);
        column.Place.ShouldBe(TheMetadataPlace);
        column.Mapped.ShouldBe("public.users.id");
    }

    [Fact]
    public void Run_HeaderColumnType_OverTheMetadata()
    {
        var run = RunPosts("-- column: users.id -> UserId\n\n" + ListPosts, metadata: "users.id=OtherId");

        run.ShouldBeClean();
        var column = ColumnsOf(run).ShouldHaveSingleItem();
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
        ColumnsOf(run)
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
        ColumnsOf(run).ShouldHaveSingleItem().Text.ShouldBe(UserId);
    }

    // -> default takes the column out of the column types of every level, and the type override gives its type.
    [Fact]
    public void Run_QueryMarkerDefault_FreesTheColumnForTheTypeOverride()
    {
        var run = RunPosts(
            "-- name: ListPosts\n-- column: id -> default\nSELECT id FROM users;\n",
            "users.id=UserId",
            typeOverrides: "int8=long"
        );

        run.ShouldBeClean();
        var column = ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe("long");
        column.Place.ShouldBe("the property SqlSourceTypeOverrides");
    }

    // Naming a column is more specific than naming a type, so a column type at any level wins over a type override
    // at any level.
    [Fact]
    public void Run_ColumnType_WinsOverATypeOverrideOfAHigherPlace()
    {
        var run = RunPosts(
            "-- name: ListPosts\n-- type-override: int8=long\nSELECT id FROM users;\n",
            "users.id=UserId"
        );

        run.ShouldBeClean();
        var column = ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe(UserId);
        column.Place.ShouldBe(ThePropertyPlace);
    }

    // A marker that says only the nullability leaves the column to the column types, which keep the marker's null.
    [Fact]
    public void Run_NullabilityOnlyMarker_KeepsTheColumnType()
    {
        var run = RunPosts("-- name: ListPosts\n-- column: id null\nSELECT id FROM users;\n", "users.id=UserId");

        run.ShouldBeClean();
        ColumnsOf(run).ShouldHaveSingleItem().Place.ShouldBe(ThePropertyPlace);
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

        var run = Run(IdsSource, [file, Sidecars.Of(file, entry)], Properties("users.id=UserId"));

        run.ShouldBeClean();
        ColumnsOf(run).Select(column => column.Text).ShouldBe(["long", "long"]);
    }

    // A column read through a view has the view's origin, so an entry of the table under it does not match it.
    [Fact]
    public void Run_ViewColumn_IsTheViewsKey()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListPosts\nSELECT id FROM user_summaries;\n");
        var entry = PostsEntry(file, Column("id", Int8, origin: new SidecarOrigin("public", "user_summaries", "id")));

        var ofTheTable = Run(IdsSource, [file, Sidecars.Of(file, entry)], Properties("users.id=UserId"));
        var ofTheView = Run(IdsSource, [file, Sidecars.Of(file, entry)], Properties("user_summaries.id=UserId"));

        ofTheTable.ShouldBeClean();
        ColumnsOf(ofTheTable).ShouldHaveSingleItem().Text.ShouldBe("long");
        ofTheView.ShouldBeClean();
        ColumnsOf(ofTheView).ShouldHaveSingleItem().Text.ShouldBe(UserId);
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

        var run = Run(IdsSource, [file, Sidecars.Of(file, entry)], Properties("users.id=UserId"));

        run.ShouldBeClean();
        run.Sources[DtoModel].ShouldContain("(global::App.UserId? Id, global::App.UserId Author)");
    }

    // A value in MSBuild serves the files of both engines.
    [Fact]
    public void Run_PropertyColumnType_MapsBothEngines()
    {
        var posts = new SqlFile(GeneratorHarness.UsersPath, ListPosts);
        var orders = new SqlFile("/app/Repo/Orders.sql", "-- name: ListOrders\nSELECT id FROM users;\n", "mssql");
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
            Properties("users.id=UserId"),
            [NpgsqlReferences.Npgsql10, SqlClientReferences.SqlClient7]
        );

        run.ShouldBeClean();
        run.Sources[DtoModel].ShouldContain("(global::App.UserId Id)");
        run.Sources["App.ListOrdersDto-Model.g.cs"].ShouldContain("(global::App.UserId Id)");
    }

    // The message names the key that chose the type, and the place that wrote it.
    [Theory]
    [InlineData("property", "users.id", ThePropertyPlace)]
    [InlineData("metadata", "users.id", TheMetadataPlace)]
    [InlineData("header", "users.id", TheHeaderPlace)]
    [InlineData("query", "id", TheQueryPlace)]
    public void Run_ColumnType_MissingType_IsSqlsrc250AtItsPlace(string level, string key, string place)
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
            level == "property" ? "users.id=NoSuchType" : null,
            metadata: level == "metadata" ? "users.id=NoSuchType" : null
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
    public void Run_ColumnType_ArrayOfAForeignType_ConvertsByElement()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListPosts\nSELECT tag_ids FROM posts;\n");
        var column = Column("tag_ids", ArrayOf(Catalog("int8"), "_int8"), origin: new("public", "posts", "tag_ids"));

        var run = MethodRuns.Run(
            file,
            [PostsEntry(file, column)],
            IdsSource,
            properties: Properties("posts.tag_ids=UserId[]")
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
            properties: Properties("users.id=UserId")
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain("(global::App.UserId.FromDatabase(reader.GetFieldValue<long>(0)))");
    }

    private static SidecarEntry PostsEntry(SqlFile file, params SidecarColumn[] columns) =>
        Entry(file, "ListPosts", [], columns);

    private static Dictionary<string, string?> Properties(string? columnTypes, string? typeOverrides = null) =>
        new() { [ColumnTypes] = columnTypes, ["SqlSourceTypeOverrides"] = typeOverrides };

    // ListPosts of this text, whose one column, id, is an int8 read from public.users.id, with the property's and the
    // metadata's column types given, and the property's type override.
    private static GeneratorRun RunPosts(
        string text,
        string? property = null,
        string? metadata = null,
        string? typeOverrides = null
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            text,
            Metadata: metadata is null ? null : new Dictionary<string, string> { [ColumnTypes] = metadata }
        );
        return Run(
            IdsSource,
            [file, Sidecars.Of(file, PostsEntry(file, Column("id", Int8, origin: UsersId)))],
            Properties(property, typeOverrides)
        );
    }

    // The types of the columns of the run's one query.
    private static PropertyType[] ColumnsOf(GeneratorRun run) =>
        [
            .. StepOutputs
                .Of<FileTypes>(run, TrackingNames.FileTypes)
                .SelectMany(file => file.Queries)
                .ShouldHaveSingleItem()
                .Columns.ShouldNotBeNull(),
        ];
}
