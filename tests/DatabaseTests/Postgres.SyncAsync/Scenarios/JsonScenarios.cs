using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Json.Types;
using SqlSource.DatabaseTests.Postgres.SyncAsync.Json;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Json/Queries/Json.sql, on the plain data source of app: every column of json_values written, with values and with
// nulls, and read back from a row that the scenario writes first, on a copy of the seed, which holds no row of the
// table; a JSON null read as each type has it, and GetRequiredJson's columns that cannot be null, which throw at the
// read for a JSON null.  The values are those of JsonQueryTests.
internal static class JsonScenarios
{
    private const int RowId = 201;

    // A row with a value in every column, written by the server from its text.
    private const string InsertRow =
        "INSERT INTO json_values VALUES (201, '{\"b\": [true, null, \"x\"], \"a\": 1}', '{\"z\": {\"y\": []},  \"a\": "
        + "-1.5}', ARRAY['{\"k\": \"v\"}'::jsonb, '42', '\"text\"'], '{\"doc\": {\"nested\": [1, 2.5, \"three\"]}}', "
        + "'3.25', '{\"name\": \"obj\", \"count\": 3}', '[1, \"two\", null, {\"four\": 4}]', "
        + "'{\"OrderId\": 7, \"Note\": \"rush\"}', ARRAY['{\"OrderId\": 1, \"Note\": \"first\"}'::jsonb, "
        + "'{\"OrderId\": 2, \"Note\": null}'], '[{\"Name\": \"red\"}, {\"Name\": \"blue\"}]', "
        + "'{\"orderId\": 9, \"note\": \"camel\"}', '{\"OrderId\": 8, \"Note\": \"text\"}')";

    // A JSON null in every column that takes one.
    private const string InsertJsonNulls =
        "INSERT INTO json_values (id, element, element_json, elements, document, node, obj, arr, meta, tags, "
        + "text_meta) VALUES (201, 'null', 'null', ARRAY['null'::jsonb], 'null', 'null', 'null', 'null', 'null', "
        + "'null', 'null')";

    private static readonly JsonDocument Document = JsonDocument.Parse("""{"doc": {"nested": [1, 2.5, "three"]}}""");

    // A value in every column: objects whose keys jsonb reorders, a scalar node, an array that holds a JSON null, and
    // an object, a number and a string in the jsonb[].
    private static readonly InsertJsonValuesArgs EveryValue = new(
        Id: RowId,
        Element: JsonElements.ElementOf("""{"b": [true, null, "x"], "a": 1}"""),
        ElementJson: JsonElements.ElementOf("""{"z": {"y": []},  "a": -1.5}"""),
        Elements:
        [
            JsonElements.ElementOf("""{"k": "v"}"""),
            JsonElements.ElementOf("42"),
            JsonElements.ElementOf("\"text\""),
        ],
        Document: Document,
        Node: JsonValue.Create(3.25),
        Obj: new JsonObject { ["name"] = "obj", ["count"] = 3 },
        Arr: new JsonArray(1, "two", null, new JsonObject { ["four"] = 4 }),
        Meta: new OrderMetadata(7, "rush"),
        Metas: [new OrderMetadata(1, "first"), new OrderMetadata(2, null)],
        Tags: [new Tag("red"), new Tag("blue")],
        Camel: new CamelMetadata(9, "camel"),
        TextMeta: new OrderMetadata(8, "text")
    );

    private static readonly InsertJsonValuesArgs Nulls = new(
        RowId,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null
    );

    private static readonly Setup Writes = new(
        Writes: true,
        Tables: ["SELECT json_values::text FROM json_values ORDER BY id"]
    );

    private static readonly Setup WritesClosed = Writes with { Connection = ConnectionStart.Closed };

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(JsonQueriesExtensions);
            var withRow = Writes with { Prepare = InsertRow };
            var withNulls = Writes with { Prepare = InsertJsonNulls };
            return
            [
                Of(host, "InsertJsonValues", ScenarioTag.Ordinary, "every value, open", Writes, EveryValue),
                Of(host, "InsertJsonValues", ScenarioTag.Ordinary, "every value, closed", WritesClosed, EveryValue),
                Of(
                    host,
                    "InsertJsonValues",
                    ScenarioTag.Ordinary,
                    "nulls, in a transaction",
                    Writes with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    Nulls
                ),
                Of(
                    host,
                    "InsertJsonValues",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WritesClosed with
                    {
                        Prepare = InsertRow,
                    },
                    EveryValue
                ),
                Of(host, "GetJsonValues", ScenarioTag.Ordinary, "a row, open", withRow, new GetJsonValuesArgs(RowId)),
                Of(
                    host,
                    "GetJsonValues",
                    ScenarioTag.Ordinary,
                    "a row, closed",
                    withRow with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetJsonValuesArgs(RowId)
                ),
                Of(
                    host,
                    "GetJsonValues",
                    ScenarioTag.Other,
                    "JSON nulls, in a transaction",
                    withNulls with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetJsonValuesArgs(RowId)
                ),
                Of(host, "GetJsonValues", ScenarioTag.NoRow, "no row, closed", Closed, new GetJsonValuesArgs(RowId)),
                Of(
                    host,
                    "GetRequiredJson",
                    ScenarioTag.Ordinary,
                    "a row, open",
                    withRow,
                    new GetRequiredJsonArgs(RowId)
                ),
                Of(
                    host,
                    "GetRequiredJson",
                    ScenarioTag.Ordinary,
                    "a row, closed",
                    withRow with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetRequiredJsonArgs(RowId)
                ),
                // A JSON null is no SQL NULL: the read throws InvalidCastException for meta, which cannot be null.
                Of(
                    host,
                    "GetRequiredJson",
                    ScenarioTag.Other,
                    "JSON nulls, closed",
                    withNulls with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetRequiredJsonArgs(RowId)
                ),
                Of(
                    host,
                    "GetRequiredJson",
                    ScenarioTag.Other,
                    "JSON nulls, in a transaction",
                    withNulls with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetRequiredJsonArgs(RowId)
                ),
                Of(host, "GetRequiredJson", ScenarioTag.NoRow, "no row, open", Open, new GetRequiredJsonArgs(RowId)),
            ];
        }
    }
}
