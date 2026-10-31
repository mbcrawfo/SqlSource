using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.SqlClient61.Types;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The queries of the linked EndToEnd/Json/Queries/ on SqlClient 6.1, the oldest driver that is supported, whose folder
// turns System.Text.Json on: every column of json_values, each an nvarchar, written by InsertJsonValues and read back
// by GetJsonValues, GetRequiredJson's columns that cannot be null, and GetPlainMeta's class without the opt-in; and
// both columns of json_columns, SQL Server 2025's json, written by InsertJsonColumns and read back by GetJsonColumns.
// Each test writes in a transaction that is rolled back.  A copy of the end-to-end project's JsonQueryTests, which
// proves the same on SqlClient 7 and on every supported major.
[Collection(UsesApp.Name)]
public sealed class JsonQueryTests(AppContainer app)
{
    // The server's error for a json value that is no object or array, a JSON null among them.
    private const int JsonScalarRefused = 13609;

    [Fact]
    public async Task InsertJsonValues_EveryColumn_RoundTrips()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        using var document = JsonDocument.Parse("""{"doc": {"nested": [1, 2.5, "three"]}}""");
        var row = EveryValue(201, document);

        var inserted = await connection.InsertJsonValues(row, transaction, cancellationToken: token);
        var read = await connection.GetJsonValues(new GetJsonValuesArgs(row.Id), transaction, cancellationToken: token);
        using var readDocument = read.Document;

        inserted.ShouldBe(1);
        read.Id.ShouldBe(row.Id);
        ShouldBeSame(read.Element, row.Element, nameof(read.Element));
        ShouldBeSame(readDocument.ShouldNotBeNull().RootElement, document.RootElement, nameof(read.Document));
        JsonNode.DeepEquals(read.Node, row.Node).ShouldBeTrue(nameof(read.Node));
        JsonNode.DeepEquals(read.Obj, row.Obj).ShouldBeTrue(nameof(read.Obj));
        JsonNode.DeepEquals(read.Arr, row.Arr).ShouldBeTrue(nameof(read.Arr));
        read.SettingsMeta.ShouldBe(row.SettingsMeta);
        read.HeaderMeta.ShouldBe(row.HeaderMeta);
        read.Tags.ShouldBe(row.Tags);
        read.MarkerMeta.ShouldBe(row.MarkerMeta);
        read.Camel.ShouldBe(row.Camel);
    }

    [Fact]
    public async Task InsertJsonValues_Nulls_RoundTrip()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        var row = new InsertJsonValuesArgs(
            Id: 202,
            Element: null,
            Document: null,
            Node: null,
            Obj: null,
            Arr: null,
            SettingsMeta: null,
            HeaderMeta: null,
            Tags: null,
            MarkerMeta: null,
            Camel: null
        );

        var inserted = await connection.InsertJsonValues(row, transaction, cancellationToken: token);
        var read = await connection.GetJsonValues(new GetJsonValuesArgs(row.Id), transaction, cancellationToken: token);

        inserted.ShouldBe(1);
        read.ShouldBe(new GetJsonValuesDto(202, null, null, null, null, null, null, null, null, null, null));
    }

    // A JSON null is no SQL NULL: a JsonElement holds it as a value of the kind Null, a JsonDocument as its root, and a
    // nullable class or node reads it as null.
    [Fact]
    public async Task GetJsonValues_JsonNull_IsNullOrOfKindNull()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        await using (
            var insert = new SqlCommand(
                "INSERT INTO dbo.json_values (id, element, document, node, obj, arr, settings_meta, header_meta, tags, "
                    + "marker_meta) VALUES (203, N'null', N'null', N'null', N'null', N'null', N'null', N'null', "
                    + "N'null', N'null')",
                connection,
                transaction
            )
        )
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var read = await connection.GetJsonValues(new GetJsonValuesArgs(203), transaction, cancellationToken: token);
        using var readDocument = read.Document;

        read.Element.ShouldNotBeNull().ValueKind.ShouldBe(JsonValueKind.Null);
        readDocument.ShouldNotBeNull().RootElement.ValueKind.ShouldBe(JsonValueKind.Null);
        read.Node.ShouldBeNull();
        read.Obj.ShouldBeNull();
        read.Arr.ShouldBeNull();
        read.SettingsMeta.ShouldBeNull();
        read.HeaderMeta.ShouldBeNull();
        read.Tags.ShouldBeNull();
        read.MarkerMeta.ShouldBeNull();
        read.Camel.ShouldBeNull();
    }

    // A JSON null in a column that the query says cannot be null throws at the read, a class's after the serializer
    // and a JsonObject's after the generated conversion.
    [Theory]
    [InlineData("settings_meta")]
    [InlineData("obj")]
    public async Task GetRequiredJson_JsonNull_ThrowsInvalidCast(string column)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        await using (
            var insert = new SqlCommand(
                "INSERT INTO dbo.json_values (id, settings_meta, obj) VALUES (204, @settingsMeta, @obj)",
                connection,
                transaction
            )
        )
        {
            _ = insert.Parameters.AddWithValue(
                "@settingsMeta",
                column == "settings_meta"
                    ? "null"
                    : /*lang=json,strict*/
                    """{"OrderId": 1, "Note": null}"""
            );
            _ = insert.Parameters.AddWithValue(
                "@obj",
                column == "obj"
                    ? "null"
                    : /*lang=json,strict*/
                    """{"a": 1}"""
            );
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var thrown = await Should.ThrowAsync<InvalidCastException>(() =>
            connection.GetRequiredJson(new GetRequiredJsonArgs(204), transaction, cancellationToken: token)
        );

        thrown.Message.ShouldBe($"Column '{column}' holds a JSON null.");
    }

    [Fact]
    public async Task GetRequiredJson_Values_AreRead()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        await using (
            var insert = new SqlCommand(
                "INSERT INTO dbo.json_values (id, settings_meta, obj) "
                    + """VALUES (205, N'{"OrderId": 1, "Note": "n"}', N'{"a": [1, "b"]}')""",
                connection,
                transaction
            )
        )
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var read = await connection.GetRequiredJson(
            new GetRequiredJsonArgs(205),
            transaction,
            cancellationToken: token
        );

        read.SettingsMeta.ShouldBe(new OrderMetadata(1, "n"));
        JsonNode.DeepEquals(read.Obj, new JsonObject { ["a"] = new JsonArray(1, "b") }).ShouldBeTrue();
    }

    // The serializer's default options store a class's properties by their own names, at each place of the opt-in,
    // and CamelMetadata's converters the web's, in camelCase: the server's JSON_VALUE reads each by that name.
    [Fact]
    public async Task InsertJsonValues_Classes_AreStoredByTheirOptions()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        using var document = JsonDocument.Parse("{}");

        _ = await connection.InsertJsonValues(EveryValue(206, document), transaction, cancellationToken: token);
        await using var command = new SqlCommand(
            "SELECT JSON_VALUE(settings_meta, '$.OrderId'), JSON_VALUE(header_meta, '$.OrderId'), "
                + "JSON_VALUE(marker_meta, '$.OrderId'), JSON_VALUE(tags, '$[1].Name'), "
                + "JSON_VALUE(camel, '$.orderId'), JSON_VALUE(camel, '$.OrderId') "
                + "FROM dbo.json_values WHERE id = 206",
            connection,
            transaction
        );
        string?[] stored;
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            (await reader.ReadAsync(token)).ShouldBeTrue();
            stored = [.. Enumerable.Range(0, 6).Select(i => reader.IsDBNull(i) ? null : reader.GetString(i))];
        }

        stored.ShouldBe(["7", "8", "10", "blue", "9", null]);
    }

    // Without the opt-in and with no converter, a class on an nvarchar is asked of SqlClient itself, which cannot read
    // it.
    [Fact]
    public async Task GetPlainMeta_ClassWithoutOptIn_ThrowsInvalidCast()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        await using (
            var insert = new SqlCommand(
                """INSERT INTO dbo.json_values (id, plain_meta) VALUES (207, N'{"OrderId": 1, "Note": null}')""",
                connection,
                transaction
            )
        )
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        _ = await Should.ThrowAsync<InvalidCastException>(() =>
            connection.GetPlainMeta(new GetPlainMetaArgs(207), transaction, cancellationToken: token)
        );
    }

    [Fact]
    public async Task InsertJsonColumns_Values_RoundTrip()
    {
        app.SkipBelow(17, "SQL Server before 2025 has no json.");
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);
        var row = new InsertJsonColumnsArgs(
            Id: 211,
            Element: ElementOf("""{"b": [true, null, "x"], "a": 1}"""),
            Meta: new OrderMetadata(12, "json")
        );

        var inserted = await connection.InsertJsonColumns(row, transaction, cancellationToken: token);
        var read = await connection.GetJsonColumns(
            new GetJsonColumnsArgs(row.Id),
            transaction,
            cancellationToken: token
        );
        await using var command = new SqlCommand(
            "SELECT JSON_VALUE(meta, '$.OrderId') FROM dbo.json_columns WHERE id = 211",
            connection,
            transaction
        );
        var stored = await command.ExecuteScalarAsync(token);

        inserted.ShouldBe(1);
        read.Id.ShouldBe(row.Id);
        ShouldBeSame(read.Element, row.Element, nameof(read.Element));
        read.Meta.ShouldBe(row.Meta);
        stored.ShouldBe("12");
    }

    [Fact]
    public async Task InsertJsonColumns_Nulls_RoundTrip()
    {
        app.SkipBelow(17, "SQL Server before 2025 has no json.");
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);

        var inserted = await connection.InsertJsonColumns(
            new InsertJsonColumnsArgs(212, null, null),
            transaction,
            cancellationToken: token
        );
        var read = await connection.GetJsonColumns(new GetJsonColumnsArgs(212), transaction, cancellationToken: token);

        inserted.ShouldBe(1);
        read.ShouldBe(new JsonColumnsDto(212, null, null));
    }

    // A JsonElement of the kind Null is sent as the text null, which json refuses as it refuses every scalar at its
    // top: a JSON null cannot reach a json column.
    [Fact]
    public async Task InsertJsonColumns_ElementOfKindNull_IsRefusedByTheServer()
    {
        app.SkipBelow(17, "SQL Server before 2025 has no json.");
        var token = TestContext.Current.CancellationToken;
        await using var connection = Connection();
        await using var transaction = await BeginAsync(connection, token);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            connection.InsertJsonColumns(
                new InsertJsonColumnsArgs(213, ElementOf("null"), null),
                transaction,
                cancellationToken: token
            )
        );

        thrown.Number.ShouldBe(JsonScalarRefused, thrown.Message);
    }

    [Fact]
    public void MethodTypes_AreJsonTypesAndClasses()
    {
        (string Name, Type Type, bool Nullable)[] properties =
        [
            ("Id", typeof(int), false),
            ("Element", typeof(JsonElement?), true),
            ("Document", typeof(JsonDocument), true),
            ("Node", typeof(JsonNode), true),
            ("Obj", typeof(JsonObject), true),
            ("Arr", typeof(JsonArray), true),
            ("SettingsMeta", typeof(OrderMetadata), true),
            ("HeaderMeta", typeof(OrderMetadata), true),
            ("Tags", typeof(List<Tag>), true),
            ("MarkerMeta", typeof(OrderMetadata), true),
            ("Camel", typeof(CamelMetadata), true),
        ];
        (string Name, Type Type, bool Nullable)[] columns =
        [
            ("Id", typeof(int), false),
            ("Element", typeof(JsonElement?), true),
            ("Meta", typeof(OrderMetadata), true),
        ];

        ModelReflection.TypedPropertiesOf(typeof(GetJsonValuesDto)).ShouldBe(properties);
        ModelReflection.TypedPropertiesOf(typeof(InsertJsonValuesArgs)).ShouldBe(properties);
        ModelReflection
            .TypedPropertiesOf(typeof(GetRequiredJsonDto))
            .ShouldBe([("SettingsMeta", typeof(OrderMetadata), false), ("Obj", typeof(JsonObject), false)]);
        ModelReflection
            .TypedPropertiesOf(typeof(GetPlainMetaDto))
            .ShouldBe([("PlainMeta", typeof(OrderMetadata), true)]);
        ModelReflection.TypedPropertiesOf(typeof(JsonColumnsDto)).ShouldBe(columns);
        ModelReflection.TypedPropertiesOf(typeof(InsertJsonColumnsArgs)).ShouldBe(columns);
    }

    // A value in every column: an object whose keys are out of order, a scalar node, an array that holds a JSON null,
    // and a class at each place of the opt-in, a list of a class and a class with converters.
    private static InsertJsonValuesArgs EveryValue(int id, JsonDocument document) =>
        new(
            Id: id,
            Element: ElementOf("""{"b": [true, null, "x"], "a": 1}"""),
            Document: document,
            Node: JsonValue.Create(3.25),
            Obj: new JsonObject { ["name"] = "obj", ["count"] = 3 },
            Arr: new JsonArray(1, "two", null, new JsonObject { ["four"] = 4 }),
            SettingsMeta: new OrderMetadata(7, "rush"),
            HeaderMeta: new OrderMetadata(8, "header"),
            Tags: [new Tag("red"), new Tag("blue")],
            MarkerMeta: new OrderMetadata(10, null),
            Camel: new CamelMetadata(9, "camel")
        );

    // The connection to "app" of the container.
    private SqlConnection Connection() => new(app.ConnectionString);

    // Opens the connection and begins the transaction that the test gives to every call, which disposing it rolls back.
    private static async Task<SqlTransaction> BeginAsync(SqlConnection connection, CancellationToken token)
    {
        await connection.OpenAsync(token);
        return (SqlTransaction)await connection.BeginTransactionAsync(token);
    }

    private static JsonElement ElementOf([StringSyntax(StringSyntaxAttribute.Json)] string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void ShouldBeSame(JsonElement? read, JsonElement? written, string name) =>
        JsonElement.DeepEquals(read.ShouldNotBeNull(name), written!.Value).ShouldBeTrue(name);
}
