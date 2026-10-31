// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// A JSON null read into a property of System.Text.Json's: a node, an object or an array not null throws, naming the
// column, whether a generated parse or the driver read it; a nullable one is null; a value type is never checked.
public sealed class JsonMethodTests
{
    private const string Json = "global::SqlSource.JsonConversions.";

    private const string JsonObject = "global::System.Text.Json.Nodes.JsonObject";

    private static readonly SqlServerType SqlServerJson = TestSidecars.SqlServer("json", -1, 0);

    // A not-null object throws on a JSON null after its read, by the edge on SQL Server and by the driver on Npgsql 10;
    // a nullable one reads with no check.
    [Theory]
    [InlineData(true, Json + "JsonObjectFromString(reader.GetFieldValue<string>(0))")]
    [InlineData(false, "reader.GetFieldValue<" + JsonObject + ">(0)")]
    public void Run_NotNullJsonObjectColumn_ThrowsOnAJsonNull(bool isSqlServer, string read)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetMeta\n-- column: meta -> System.Text.Json.Nodes.JsonObject\n"
                + "-- column: maybe_meta -> System.Text.Json.Nodes.JsonObject\nSELECT meta, maybe_meta FROM docs;\n"
        );

        var run = isSqlServer
            ? MethodRuns.RunSqlServer(
                file,
                [
                    MethodRuns.SqlServerEntry(
                        file,
                        "GetMeta",
                        [],
                        [
                            TestSidecars.Column(0, "meta", SqlServerJson, nullable: false),
                            TestSidecars.Column(0, "maybe_meta", SqlServerJson, nullable: true),
                        ]
                    ),
                ]
            )
            : MethodRuns.Run(
                file,
                [
                    Entry(
                        file,
                        "GetMeta",
                        [],
                        [Column("meta", Catalog("jsonb")), Column("maybe_meta", Catalog("jsonb"), nullable: true)]
                    ),
                ]
            );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "(" + read + " ?? throw new global::System.InvalidCastException(\"Column 'meta' holds a JSON null.\"), "
        );
        methods.ShouldContain(
            "reader.IsDBNull(1) ? default("
                + JsonObject
                + "?) : "
                + read.Replace("(0)", "(1)", System.StringComparison.Ordinal)
                + ")"
        );
        methods.ShouldNotContain("Column 'maybe_meta' holds a JSON null.");
    }

    // On Npgsql 8, jsonb[] as JsonNode[] is parsed element by element: a JSON null element of a not-null element
    // throws, naming the column, and a NULL element stays null; JsonNode?[] gives null for both.
    [Theory]
    [InlineData(
        "System.Text.Json.Nodes.JsonNode[]",
        "global::System.Text.Json.Nodes.JsonNode>(reader.GetFieldValue<string[]>(0), static element => element is { } "
            + "item ? ("
            + Json
            + "JsonNodeFromString(item) ?? throw new global::System.InvalidCastException("
            + "\"Column 'c' holds a JSON null.\")) : null!)"
    )]
    [InlineData(
        "System.Text.Json.Nodes.JsonNode?[]",
        "global::System.Text.Json.Nodes.JsonNode?>(reader.GetFieldValue<string[]>(0), static element => element is "
            + "{ } item ? "
            + Json
            + "JsonNodeFromString(item) : null)"
    )]
    public void Run_JsonNodeArrayByElement_ThrowsOnAJsonNullElement(string type, string converted)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: jsonb[]=" + type + "\n\n-- name: GetNodes\nSELECT c FROM docs;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetNodes", [], [Column("c", ArrayOf(Catalog("jsonb"), "_jsonb"))])],
            references: [NpgsqlReferences.Npgsql8]
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("global::System.Array.ConvertAll<string, " + converted);
    }

    // A JsonElement is a value type, whose read never gives null: neither the edge on SQL Server nor the driver on
    // Npgsql checks it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Run_JsonElementColumn_HasNoJsonNullCheck(bool isSqlServer)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- library-types: json=on\n\n-- name: GetDoc\nSELECT doc FROM docs;\n"
        );

        var run = isSqlServer
            ? MethodRuns.RunSqlServer(
                file,
                [
                    MethodRuns.SqlServerEntry(
                        file,
                        "GetDoc",
                        [],
                        [TestSidecars.Column(0, "doc", SqlServerJson, nullable: false)]
                    ),
                ]
            )
            : MethodRuns.Run(file, [Entry(file, "GetDoc", [], [Column("doc", Catalog("jsonb"))])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            isSqlServer
                ? Json + "JsonElementFromString(reader.GetFieldValue<string>(0))"
                : "reader.GetFieldValue<global::System.Text.Json.JsonElement>(0)"
        );
        methods.ShouldNotContain("holds a JSON null");
    }

    // On SQL Server a JsonElement is converted from and to a string, so one given to a database type that is not read
    // as a string, an int, is SQLSRC279 for the column and for the parameter, and the query gets no method.
    [Fact]
    public void Run_JsonElementOnAnIntOnSqlServer_IsSqlsrc279()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: int=System.Text.Json.JsonElement\n\n-- name: GetDoc\nSELECT n FROM docs WHERE n = @n;\n"
        );
        var integer = TestSidecars.SqlServer("int", 4, 10);

        var run = MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "GetDoc",
                    [TestSidecars.Parameter(0, "n", integer, false)],
                    [TestSidecars.Column(0, "n", integer, nullable: false)]
                ),
            ]
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC279 /app/Repo/Users.sql(3,10)-(3,16): The column 'n' has type 'System.Text.Json.JsonElement', "
                    + "whose converters convert from and to 'string', and not from and to 'int', the type the database "
                    + "type 'int' is read as.  Add a converter between 'System.Text.Json.JsonElement' and 'int'.",
                "SQLSRC279 /app/Repo/Users.sql(3,10)-(3,16): The parameter 'n' has type "
                    + "'System.Text.Json.JsonElement', whose converters convert from and to 'string', and not from and "
                    + "to 'int', the type the database type 'int' is read as.  Add a converter between "
                    + "'System.Text.Json.JsonElement' and 'int'.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }
}
