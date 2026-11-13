using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Json.Types;
using SqlSource.DatabaseTests.SqlServer.SyncAsync.Json;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Json/Queries/: every column of json_values, each an nvarchar, and of json_columns, SQL Server 2025's json, written
// with values and with nulls, and read back from a row that the scenario writes first, on a copy of the seed, which
// holds no row of either table; a JSON null read as each type has it, GetRequiredJson's columns that cannot be null,
// which throw at the read for a JSON null, and GetPlainMeta's class without the opt-in, which SqlClient cannot read.
// The values are those of JsonQueryTests.  The errors are a duplicate id and a JSON null sent to json, which the server
// refuses.
internal static class JsonScenarios
{
    private const int RowId = 201;

    // A row with a value in every column, written by the server from its text.
    private const string InsertRow = """
        INSERT INTO dbo.json_values (
            id, element, document, node, obj, arr, settings_meta, header_meta, tags, marker_meta, camel, plain_meta
        ) VALUES (
            201, N'{"b": [true, null, "x"], "a": 1}', N'{"doc": {"nested": [1, 2.5, "three"]}}', N'3.25',
            N'{"name": "obj", "count": 3}', N'[1, "two", null, {"four": 4}]', N'{"OrderId": 7, "Note": "rush"}',
            N'{"OrderId": 8, "Note": "header"}', N'[{"Name": "red"}, {"Name": "blue"}]',
            N'{"OrderId": 10, "Note": null}', N'{"orderId": 9, "note": "camel"}', N'{"OrderId": 1, "Note": null}'
        )
        """;

    // A JSON null in every column but camel, whose converter refuses it.
    private const string InsertJsonNulls = """
        INSERT INTO dbo.json_values (
            id, element, document, node, obj, arr, settings_meta, header_meta, tags, marker_meta
        ) VALUES (201, N'null', N'null', N'null', N'null', N'null', N'null', N'null', N'null', N'null')
        """;

    // A JSON null in settings_meta alone, of GetRequiredJson's two columns.
    private const string InsertNullSettings = """
        INSERT INTO dbo.json_values (id, settings_meta, obj) VALUES (201, N'null', N'{"a": 1}')
        """;

    private const string InsertColumns = """
        INSERT INTO dbo.json_columns (id, element, meta)
        VALUES (201, N'{"b": [true, null, "x"], "a": 1}', N'{"OrderId": 12, "Note": "json"}'), (202, NULL, NULL)
        """;

    private static readonly JsonDocument Document = JsonDocument.Parse("""{"doc": {"nested": [1, 2.5, "three"]}}""");

    // A value in every column: an object whose keys are out of order, a scalar node, an array that holds a JSON null,
    // and a class at each place of the opt-in, a list of a class and a class with converters.
    private static readonly InsertJsonValuesArgs EveryValue = new(
        Id: RowId,
        Element: JsonElements.ElementOf("""{"b": [true, null, "x"], "a": 1}"""),
        Document: Document,
        Node: JsonValue.Create(3.25),
        Obj: new JsonObject { ["name"] = "obj", ["count"] = 3 },
        Arr: new JsonArray(1, "two", null, new JsonObject { ["four"] = 4 }),
        SettingsMeta: new OrderMetadata(7, "rush"),
        HeaderMeta: new OrderMetadata(8, "header"),
        Tags: [new Tag("red"), new Tag("blue")],
        MarkerMeta: new OrderMetadata(10, null),
        Camel: new CamelMetadata(9, "camel")
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
        null
    );

    private static readonly InsertJsonColumnsArgs ColumnValues = new(
        RowId,
        JsonElements.ElementOf("""{"b": [true, null, "x"], "a": 1}"""),
        new OrderMetadata(12, "json")
    );

    private static readonly Setup ValuesWritten = Writes("SELECT * FROM dbo.json_values ORDER BY id");

    // json's text, which SqlClient may read as another type.
    private static readonly Setup ColumnsWritten = Writes(
        "SELECT id, CAST(element AS nvarchar(max)) AS element, CAST(meta AS nvarchar(max)) AS meta "
            + "FROM dbo.json_columns ORDER BY id"
    );

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(JsonQueriesExtensions);
            var closed = ValuesWritten with { Connection = ConnectionStart.Closed };
            var inTransaction = ValuesWritten with { Transaction = CallerTransaction.Begun };
            var withRow = ValuesWritten with { Prepare = InsertRow };
            var withNulls = ValuesWritten with { Prepare = InsertJsonNulls };
            var withColumns = ColumnsWritten with { Prepare = InsertColumns };
            return
            [
                Of(host, "InsertJsonValues", ScenarioTag.Ordinary, "every value, open", ValuesWritten, EveryValue),
                Of(host, "InsertJsonValues", ScenarioTag.Ordinary, "every value, closed", closed, EveryValue),
                Of(host, "InsertJsonValues", ScenarioTag.Ordinary, "nulls, in a transaction", inTransaction, Nulls),
                Of(
                    host,
                    "InsertJsonValues",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    withRow with
                    {
                        Connection = ConnectionStart.Closed,
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
                // A JSON null is no SQL NULL: the read throws InvalidCastException for a column that cannot be null.
                Of(
                    host,
                    "GetRequiredJson",
                    ScenarioTag.Other,
                    "a JSON null in settings_meta, closed",
                    ValuesWritten with
                    {
                        Connection = ConnectionStart.Closed,
                        Prepare = InsertNullSettings,
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
                // SqlClient cannot read the class itself.
                Of(host, "GetPlainMeta", ScenarioTag.Other, "a row, open", withRow, new GetPlainMetaArgs(RowId)),
                Of(
                    host,
                    "GetPlainMeta",
                    ScenarioTag.Other,
                    "a row, in a transaction",
                    withRow with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetPlainMetaArgs(RowId)
                ),
                Of(
                    host,
                    "GetPlainMeta",
                    ScenarioTag.Ordinary,
                    "a NULL, closed",
                    withNulls with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetPlainMetaArgs(RowId)
                ),
                Of(host, "GetPlainMeta", ScenarioTag.NoRow, "no row, closed", Closed, new GetPlainMetaArgs(RowId)),
                Of(host, "InsertJsonColumns", ScenarioTag.Ordinary, "values, open", ColumnsWritten, ColumnValues),
                Of(
                    host,
                    "InsertJsonColumns",
                    ScenarioTag.Ordinary,
                    "values, closed",
                    ColumnsWritten with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    ColumnValues
                ),
                Of(
                    host,
                    "InsertJsonColumns",
                    ScenarioTag.Ordinary,
                    "nulls, in a transaction",
                    ColumnsWritten with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new InsertJsonColumnsArgs(RowId, null, null)
                ),
                // json refuses every scalar at its top, a JSON null among them.
                Of(
                    host,
                    "InsertJsonColumns",
                    ScenarioTag.DatabaseError,
                    "an element of the kind Null, closed",
                    ColumnsWritten with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new InsertJsonColumnsArgs(RowId, JsonElements.ElementOf("null"), null)
                ),
                Of(
                    host,
                    "InsertJsonColumns",
                    ScenarioTag.DatabaseError,
                    "an element of the kind Null, in a transaction",
                    ColumnsWritten with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new InsertJsonColumnsArgs(RowId, JsonElements.ElementOf("null"), null)
                ),
                Of(
                    host,
                    "InsertJsonColumns",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    withColumns with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    ColumnValues
                ),
                Of(
                    host,
                    "GetJsonColumns",
                    ScenarioTag.Ordinary,
                    "a row, open",
                    withColumns,
                    new GetJsonColumnsArgs(RowId)
                ),
                Of(
                    host,
                    "GetJsonColumns",
                    ScenarioTag.Ordinary,
                    "a row of nulls, closed",
                    withColumns with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetJsonColumnsArgs(RowId + 1)
                ),
                Of(
                    host,
                    "GetJsonColumns",
                    ScenarioTag.Ordinary,
                    "a row, in a transaction",
                    withColumns with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetJsonColumnsArgs(RowId)
                ),
                Of(host, "GetJsonColumns", ScenarioTag.NoRow, "no row, closed", Closed, new GetJsonColumnsArgs(RowId)),
            ];
        }
    }
}
