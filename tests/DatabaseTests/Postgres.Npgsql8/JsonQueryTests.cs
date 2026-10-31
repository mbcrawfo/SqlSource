using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.Npgsql8.Types;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of the linked Json.sql on Npgsql 8, the oldest driver that is supported, whose folder turns
// System.Text.Json on: every column of json_values written by InsertJsonValues and read back by GetJsonValues, and
// GetRequiredJson's columns that cannot be null, on a plain data source of "app", which calls no EnableDynamicJson(),
// in a transaction that is rolled back.  The node types go through generated conversions here, where Npgsql 10 reads
// and sends them itself.  A copy of the end-to-end project's JsonQueryTests, which proves the same on Npgsql 10.
[Collection(UsesApp.Name)]
public sealed class JsonQueryTests(AppContainer app)
{
    [Fact]
    public async Task InsertJsonValues_EveryColumn_RoundTrips()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        using var document = JsonDocument.Parse("""{"doc": {"nested": [1, 2.5, "three"]}}""");
        var row = EveryValue(document);

        var inserted = await connection.InsertJsonValues(row, transaction, cancellationToken: token);
        var read = await connection.GetJsonValues(new GetJsonValuesArgs(row.Id), transaction, cancellationToken: token);
        using var readDocument = read.Document;
        await transaction.RollbackAsync(token);

        inserted.ShouldBe(1);
        read.Id.ShouldBe(row.Id);
        ShouldBeSame(read.Element, row.Element, nameof(read.Element));
        ShouldBeSame(read.ElementJson, row.ElementJson, nameof(read.ElementJson));
        var elements = read.Elements.ShouldNotBeNull();
        elements.Length.ShouldBe(row.Elements!.Length);
        for (var i = 0; i < elements.Length; i++)
        {
            ShouldBeSame(elements[i], row.Elements[i], nameof(read.Elements));
        }

        ShouldBeSame(readDocument.ShouldNotBeNull().RootElement, document.RootElement, nameof(read.Document));
        JsonNode.DeepEquals(read.Node, row.Node).ShouldBeTrue(nameof(read.Node));
        JsonNode.DeepEquals(read.Obj, row.Obj).ShouldBeTrue(nameof(read.Obj));
        JsonNode.DeepEquals(read.Arr, row.Arr).ShouldBeTrue(nameof(read.Arr));
        read.Meta.ShouldBe(row.Meta);
        read.Metas.ShouldBe(row.Metas);
        read.Tags.ShouldBe(row.Tags);
        read.Camel.ShouldBe(row.Camel);
        read.TextMeta.ShouldBe(row.TextMeta);
    }

    [Fact]
    public async Task InsertJsonValues_Nulls_RoundTrip()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var row = Nulls(202);

        var inserted = await connection.InsertJsonValues(row, transaction, cancellationToken: token);
        var read = await connection.GetJsonValues(new GetJsonValuesArgs(row.Id), transaction, cancellationToken: token);
        await transaction.RollbackAsync(token);

        inserted.ShouldBe(1);
        read.Id.ShouldBe(row.Id);
        foreach (var property in ModelReflection.PropertiesOf(read.GetType()))
        {
            if (property.Name != nameof(read.Id))
            {
                property.GetValue(read).ShouldBeNull(property.Name);
            }
        }
    }

    // A JSON null is no SQL NULL: a JsonElement holds it as a value of the kind Null, a JsonDocument as its root, and a
    // nullable class or node reads it as null.
    [Fact]
    public async Task GetJsonValues_JsonNull_IsNullOrOfKindNull()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (
            var insert = new NpgsqlCommand(
                "INSERT INTO json_values (id, element, element_json, elements, document, node, obj, arr, meta, tags, "
                    + "text_meta) VALUES (203, 'null', 'null', ARRAY['null'::jsonb], 'null', 'null', 'null', 'null', "
                    + "'null', 'null', 'null')",
                connection,
                transaction
            )
        )
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var read = await connection.GetJsonValues(new GetJsonValuesArgs(203), transaction, cancellationToken: token);
        using var readDocument = read.Document;
        await transaction.RollbackAsync(token);

        read.Element.ShouldNotBeNull().ValueKind.ShouldBe(JsonValueKind.Null);
        read.ElementJson.ShouldNotBeNull().ValueKind.ShouldBe(JsonValueKind.Null);
        read.Elements.ShouldNotBeNull().ShouldHaveSingleItem().ValueKind.ShouldBe(JsonValueKind.Null);
        readDocument.ShouldNotBeNull().RootElement.ValueKind.ShouldBe(JsonValueKind.Null);
        read.Node.ShouldBeNull();
        read.Obj.ShouldBeNull();
        read.Arr.ShouldBeNull();
        read.Meta.ShouldBeNull();
        read.Tags.ShouldBeNull();
        read.TextMeta.ShouldBeNull();
    }

    // A JSON null in a column that the query says cannot be null throws at the read, a class's from the serializer
    // and a node's from the driver's read, or from the generated conversion on Npgsql 8.
    [Theory]
    [InlineData("meta")]
    [InlineData("node")]
    public async Task GetRequiredJson_JsonNull_ThrowsInvalidCast(string column)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (
            var insert = new NpgsqlCommand(
                "INSERT INTO json_values (id, meta, node) VALUES (204, @meta::jsonb, @node::jsonb)",
                connection,
                transaction
            )
        )
        {
            _ = insert.Parameters.AddWithValue(
                "meta",
                column == "meta"
                    ? "null"
                    : /*lang=json,strict*/
                    """{"OrderId": 1, "Note": null}"""
            );
            _ = insert.Parameters.AddWithValue("node", column == "node" ? "null" : "[1]");
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var thrown = await Should.ThrowAsync<InvalidCastException>(() =>
            connection.GetRequiredJson(new GetRequiredJsonArgs(204), transaction, cancellationToken: token)
        );
        await transaction.RollbackAsync(token);

        thrown.Message.ShouldBe($"Column '{column}' holds a JSON null.");
    }

    [Fact]
    public async Task GetRequiredJson_Values_AreRead()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (
            var insert = new NpgsqlCommand(
                """INSERT INTO json_values (id, meta, node) VALUES (205, '{"OrderId": 1, "Note": "n"}', '[1, "a"]')""",
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
        await transaction.RollbackAsync(token);

        read.Meta.ShouldBe(new OrderMetadata(1, "n"));
        JsonNode.DeepEquals(read.Node, new JsonArray(1, "a")).ShouldBeTrue();
    }

    // The serializer's default options store a class's properties by their own names, and CamelMetadata's converters
    // the web's, in camelCase: the database's own operator reads each by that name.
    [Fact]
    public async Task InsertJsonValues_Classes_AreStoredByTheirOptions()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        using var document = JsonDocument.Parse("{}");

        _ = await connection.InsertJsonValues(EveryValue(document), transaction, cancellationToken: token);
        await using var command = new NpgsqlCommand(
            "SELECT camel ->> 'orderId', meta ->> 'OrderId', text_meta::jsonb ->> 'OrderId', camel ->> 'OrderId' "
                + "FROM json_values WHERE id = 201",
            connection,
            transaction
        );
        await using var reader = await command.ExecuteReaderAsync(token);
        (await reader.ReadAsync(token)).ShouldBeTrue();
        string?[] stored = [.. Enumerable.Range(0, 4).Select(i => reader.IsDBNull(i) ? null : reader.GetString(i))];
        await reader.DisposeAsync();
        await transaction.RollbackAsync(token);

        stored.ShouldBe(["9", "7", "8", null]);
    }

    [Fact]
    public void MethodTypes_AreJsonTypesAndClasses()
    {
        (string Name, Type Type, bool Nullable)[] properties =
        [
            ("Id", typeof(int), false),
            ("Element", typeof(JsonElement?), true),
            ("ElementJson", typeof(JsonElement?), true),
            ("Elements", typeof(JsonElement[]), true),
            ("Document", typeof(JsonDocument), true),
            ("Node", typeof(JsonNode), true),
            ("Obj", typeof(JsonObject), true),
            ("Arr", typeof(JsonArray), true),
            ("Meta", typeof(OrderMetadata), true),
            ("Metas", typeof(OrderMetadata[]), true),
            ("Tags", typeof(List<Tag>), true),
            ("Camel", typeof(CamelMetadata), true),
            ("TextMeta", typeof(OrderMetadata), true),
        ];

        ModelReflection.TypedPropertiesOf(typeof(JsonValuesDto)).ShouldBe(properties);
        ModelReflection.TypedPropertiesOf(typeof(InsertJsonValuesArgs)).ShouldBe(properties);
        ModelReflection
            .TypedPropertiesOf(typeof(GetRequiredJsonDto))
            .ShouldBe([("Meta", typeof(OrderMetadata), false), ("Node", typeof(JsonNode), false)]);
    }

    // A value in every column: objects whose keys jsonb reorders, a scalar node, an array that holds a JSON null, and
    // an object, a number and a string in the jsonb[].
    private static InsertJsonValuesArgs EveryValue(JsonDocument document) =>
        new(
            Id: 201,
            Element: ElementOf("""{"b": [true, null, "x"], "a": 1}"""),
            ElementJson: ElementOf("""{"z": {"y": []},  "a": -1.5}"""),
            Elements: [ElementOf("""{"k": "v"}"""), ElementOf("42"), ElementOf("\"text\"")],
            Document: document,
            Node: JsonValue.Create(3.25),
            Obj: new JsonObject { ["name"] = "obj", ["count"] = 3 },
            Arr: new JsonArray(1, "two", null, new JsonObject { ["four"] = 4 }),
            Meta: new OrderMetadata(7, "rush"),
            Metas: [new OrderMetadata(1, "first"), new OrderMetadata(2, null)],
            Tags: [new Tag("red"), new Tag("blue")],
            Camel: new CamelMetadata(9, "camel"),
            TextMeta: new OrderMetadata(8, "text")
        );

    // A row with NULL in every column but id.
    private static InsertJsonValuesArgs Nulls(int id) =>
        new(
            Id: id,
            Element: null,
            ElementJson: null,
            Elements: null,
            Document: null,
            Node: null,
            Obj: null,
            Arr: null,
            Meta: null,
            Metas: null,
            Tags: null,
            Camel: null,
            TextMeta: null
        );

    private static JsonElement ElementOf([StringSyntax(StringSyntaxAttribute.Json)] string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void ShouldBeSame(JsonElement? read, JsonElement? written, string name) =>
        JsonElement.DeepEquals(read.ShouldNotBeNull(name), written!.Value).ShouldBeTrue(name);
}
