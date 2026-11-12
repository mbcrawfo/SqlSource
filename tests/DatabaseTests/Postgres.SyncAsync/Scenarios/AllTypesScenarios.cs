using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using NpgsqlTypes;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/AllTypes.sql: both rows of all_types read, and every type of the map written, by InsertAllTypes with the
// columns Dapper can send and by InsertEveryType with every column, with values and with nulls, each on a copy of the
// seed.  The values are those of row 1 of the seed, written out, since a scenario holds its input before any database
// is read.  The errors are a duplicate id and a varchar longer than its length.
internal static class AllTypesScenarios
{
    // The id of the row an insert adds, past the seed's two.
    private const int InsertedId = 3;

    private static readonly Setup Writes = new(Writes: true, Tables: ["SELECT * FROM all_types ORDER BY id"]);

    private static readonly Setup WritesClosed = Writes with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = Writes with { Transaction = CallerTransaction.Begun };

    private static readonly DateTime Timestamp = new(2024, 3, 15, 13, 45, 30, 123, 456, DateTimeKind.Unspecified);

    private static readonly DateTime Timestamptz = new(2024, 3, 15, 13, 45, 30, 123, 456, DateTimeKind.Utc);

    private static readonly InsertAllTypesArgs WhatDapperSends = new(
        Id: InsertedId,
        BoolValue: true,
        Int2Value: short.MinValue,
        Int4Value: int.MaxValue,
        Int8Value: long.MinValue,
        Float4Value: 1.5f,
        Float8Value: -2.25,
        NumericValue: 12345678.1234m,
        MoneyValue: 1234.56m,
        TextValue: "Grüße, 日本語",
        VarcharValue: "Ünïcödé",
        BpcharValue: "héllo",
        NameValue: "a_name",
        CharValue: 'A',
        CitextValue: "Mixed Case",
        ByteaValue: [0xde, 0xad, 0xbe, 0xef, 0x00, 0xff],
        DateValue: new DateOnly(2024, 3, 15),
        TimeValue: new TimeOnly(13, 45, 30, 123, 456),
        TimestampValue: Timestamp,
        TimestamptzValue: Timestamptz,
        IntervalValue: new TimeSpan(3, 4, 5, 6, 789, 12),
        UuidValue: new Guid("123e4567-e89b-12d3-a456-426614174000"),
        Bit1Value: new BitArray([true]),
        Bit8Value: Bits("10101010"),
        VarbitValue: Bits("1011001"),
        PathValue: new NpgsqlPath([new(0, 0), new(1, 1), new(2, 0)], open: true),
        PolygonValue: new NpgsqlPolygon(new(0, 0), new(4, 0), new(4, 3)),
#pragma warning disable CS0618 // Parse is NpgsqlTsVector's one public way to make a value with lexemes.
        TsvectorValue: NpgsqlTsVector.Parse("'fat':2 'rat':3"),
#pragma warning restore CS0618
        HstoreValue: new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["k1"] = "v1",
            ["k2"] = "Zoë",
            ["k3"] = null,
        },
        Int4multirangeValue: [new(1, 5), new(10, 20)],
        Int8multirangeValue: [new(1L, 5L), new(10L, 20L)],
        NummultirangeValue: [new(1.5m, false, 2.5m, true), new(4m, 5m)],
        TsmultirangeValue: [new(Day(1, 1), Day(1, 2)), new(Day(2, 1), Day(2, 2))],
        TstzmultirangeValue: [new(UtcDay(1, 1), UtcDay(1, 2)), new(UtcDay(2, 1), UtcDay(2, 2))],
        DatemultirangeValue: [new(new(2024, 1, 1), new(2024, 1, 5)), new(new(2024, 2, 1), new(2024, 2, 5))],
        Int4ArrayValue: [1, 2, 3],
        TextArrayValue: ["a", "b c", "Zoë"],
        DateArrayValue: [new(2024, 1, 1), new(2024, 12, 31)],
        EmailValue: "zoe@example.com"
    );

    private static readonly InsertEveryTypeArgs EveryValue = new(
        Id: InsertedId,
        BoolValue: WhatDapperSends.BoolValue,
        Int2Value: WhatDapperSends.Int2Value,
        Int4Value: WhatDapperSends.Int4Value,
        Int8Value: WhatDapperSends.Int8Value,
        Float4Value: WhatDapperSends.Float4Value,
        Float8Value: WhatDapperSends.Float8Value,
        NumericValue: WhatDapperSends.NumericValue,
        MoneyValue: WhatDapperSends.MoneyValue,
        TextValue: WhatDapperSends.TextValue,
        VarcharValue: WhatDapperSends.VarcharValue,
        BpcharValue: WhatDapperSends.BpcharValue,
        NameValue: WhatDapperSends.NameValue,
        CharValue: WhatDapperSends.CharValue,
        CitextValue: WhatDapperSends.CitextValue,
        ByteaValue: WhatDapperSends.ByteaValue,
        DateValue: WhatDapperSends.DateValue,
        TimeValue: WhatDapperSends.TimeValue,
        TimetzValue: new DateTimeOffset(1, 1, 2, 13, 45, 30, 123, 456, TimeSpan.FromHours(2)),
        TimestampValue: WhatDapperSends.TimestampValue,
        TimestamptzValue: WhatDapperSends.TimestamptzValue,
        IntervalValue: WhatDapperSends.IntervalValue,
        UuidValue: WhatDapperSends.UuidValue,
        JsonValue: /*lang=json,strict*/
        """{"name": "Zoë", "tags": ["a", "b"]}""",
        JsonbValue: /*lang=json,strict*/
        """{"name": "Zoë", "tags": ["a", "b"]}""",
        JsonpathValue: "$.\"name\"",
        XmlValue: """<root><item id="1">Zoë</item></root>""",
        InetValue: IPAddress.Parse("192.168.1.10"),
        CidrValue: IPNetwork.Parse("192.168.100.128/25"),
        MacaddrValue: PhysicalAddress.Parse("08-00-2B-01-02-03"),
        Macaddr8Value: PhysicalAddress.Parse("08-00-2B-01-02-03-04-05"),
        Bit1Value: WhatDapperSends.Bit1Value,
        Bit8Value: WhatDapperSends.Bit8Value,
        VarbitValue: WhatDapperSends.VarbitValue,
        PointValue: new NpgsqlPoint(1.5, -2.25),
        LineValue: new NpgsqlLine(1, -1, 0),
        LsegValue: new NpgsqlLSeg(new(0, 0), new(1.5, 2.5)),
        BoxValue: new NpgsqlBox(new(3, 4), new(1, 2)),
        PathValue: WhatDapperSends.PathValue,
        PolygonValue: WhatDapperSends.PolygonValue,
        CircleValue: new NpgsqlCircle(1, 2, 3),
        TsvectorValue: WhatDapperSends.TsvectorValue,
        TsqueryValue: new NpgsqlTsQueryAnd(new NpgsqlTsQueryLexeme("fat"), new NpgsqlTsQueryLexeme("rat")),
        OidValue: uint.MaxValue,
        XidValue: uint.MaxValue,
        CidValue: 12345u,
        // The oids of integer and simple, which are the same on every major.
        RegtypeValue: 23u,
        RegconfigValue: 3748u,
        Xid8Value: ulong.MaxValue,
        TidValue: new NpgsqlTid(0, 1),
        PgLsnValue: NpgsqlLogSequenceNumber.Parse("16/B374D848"),
        Int2vectorValue: [1, 2, 3],
        OidvectorValue: [4, 5, 6],
        RefcursorValue: "cursor_1",
        HstoreValue: WhatDapperSends.HstoreValue,
        LtreeValue: "Top.Science.Astronomy",
        LqueryValue: "Top.*{0,2}.Astronomy",
        LtxtqueryValue: "Science & Astronomy",
        Int4rangeValue: new NpgsqlRange<int>(1, 10),
        Int8rangeValue: new NpgsqlRange<long>(1, 100),
        NumrangeValue: new NpgsqlRange<decimal>(1.5m, false, 10.25m, true),
        TsrangeValue: new NpgsqlRange<DateTime>(Day(1, 1).AddMilliseconds(500), Day(2, 1)),
        TstzrangeValue: new NpgsqlRange<DateTime>(UtcDay(1, 1), UtcDay(2, 1)),
        DaterangeValue: new NpgsqlRange<DateOnly>(new(2024, 1, 1), new(2024, 2, 1)),
        Int4multirangeValue: WhatDapperSends.Int4multirangeValue,
        Int8multirangeValue: WhatDapperSends.Int8multirangeValue,
        NummultirangeValue: WhatDapperSends.NummultirangeValue,
        TsmultirangeValue: WhatDapperSends.TsmultirangeValue,
        TstzmultirangeValue: WhatDapperSends.TstzmultirangeValue,
        DatemultirangeValue: WhatDapperSends.DatemultirangeValue,
        Int4ArrayValue: WhatDapperSends.Int4ArrayValue,
        TextArrayValue: WhatDapperSends.TextArrayValue,
        DateArrayValue: WhatDapperSends.DateArrayValue,
        StatusArrayValue: ["active", "closed"],
        EmailValue: WhatDapperSends.EmailValue,
        StatusValue: "suspended"
    );

    // Null in every column but id, as row 2 of the seed.
    private static readonly InsertEveryTypeArgs Nulls = NullsOf(InsertedId);

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(AllTypesQueriesExtensions);
            return
            [
                .. OpenAndClosed(host, "ListAllTypes"),
                Of(host, "ListAllTypes", ScenarioTag.Ordinary, "in a transaction", InTransaction),
                Of(host, "InsertAllTypes", ScenarioTag.Ordinary, "open", Writes, WhatDapperSends),
                Of(host, "InsertAllTypes", ScenarioTag.Ordinary, "closed", WritesClosed, WhatDapperSends),
                Of(
                    host,
                    "InsertAllTypes",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WritesClosed,
                    WhatDapperSends with
                    {
                        Id = 1,
                    }
                ),
                Of(
                    host,
                    "InsertAllTypes",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, in a transaction",
                    WritesInTransaction,
                    WhatDapperSends with
                    {
                        Id = 1,
                    }
                ),
                Of(host, "InsertEveryType", ScenarioTag.Ordinary, "every value, open", Writes, EveryValue),
                Of(host, "InsertEveryType", ScenarioTag.Ordinary, "every value, closed", WritesClosed, EveryValue),
                Of(host, "InsertEveryType", ScenarioTag.Ordinary, "nulls, closed", WritesClosed, Nulls),
                Of(
                    host,
                    "InsertEveryType",
                    ScenarioTag.Ordinary,
                    "nulls, in a transaction",
                    WritesInTransaction,
                    Nulls
                ),
                // varchar_value is varchar(20): the server refuses a longer value and never cuts it.
                Of(
                    host,
                    "InsertEveryType",
                    ScenarioTag.DatabaseError,
                    "a varchar longer than its length, closed",
                    WritesClosed,
                    Nulls with
                    {
                        VarcharValue = new string('x', 21),
                    }
                ),
                Of(
                    host,
                    "InsertEveryType",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, in a transaction",
                    WritesInTransaction,
                    EveryValue with
                    {
                        Id = 1,
                    }
                ),
            ];
        }
    }

    // An InsertEveryTypeArgs with the id and null in every other parameter, by its constructor.
    private static InsertEveryTypeArgs NullsOf(int id)
    {
        var constructor = typeof(InsertEveryTypeArgs).GetConstructors().Single();
        return (InsertEveryTypeArgs)
            constructor.Invoke([
                .. constructor.GetParameters().Select(parameter => parameter.Name == "Id" ? (object?)id : null),
            ]);
    }

    private static DateTime Day(int month, int day) => new(2024, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static DateTime UtcDay(int month, int day) => new(2024, month, day, 0, 0, 0, DateTimeKind.Utc);

    // A bit string, its first bit first.
    private static BitArray Bits(string bits) => new(bits.Select(static bit => bit == '1').ToArray());
}
