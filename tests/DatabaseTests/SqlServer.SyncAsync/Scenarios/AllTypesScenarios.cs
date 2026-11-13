using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/AllTypes.sql: both rows of all_types read, every type of the map written, by InsertAllTypes with the columns
// Dapper can send and by WriteAllTypes with every column but the rowversion, with values and with nulls, each on a copy
// of the seed, and the facets of the bindings, in a WHERE and in a write.  The values are those of row 1 of the seed,
// written out, since a scenario holds its input before any database is read.  The errors are a duplicate id, a
// varchar and a varbinary longer than their columns, which the server refuses, and a decimal outside its precision,
// which SqlClient refuses before sending it.
internal static class AllTypesScenarios
{
    // The id of the row an insert adds, past the seed's two.
    private const int InsertedId = 3;

    // Every column, the rowversion, which the server fills, by its length alone.
    private const string AllTypes =
        "SELECT id, bigint_value, int_value, smallint_value, tinyint_value, bit_value, decimal_value, numeric_value, "
        + "money_value, smallmoney_value, float_value, real_value, date_value, datetime_value, datetime2_value, "
        + "smalldatetime_value, time_value, datetimeoffset_value, char_value, varchar_value, text_value, nchar_value, "
        + "nvarchar_value, ntext_value, xml_value, binary_value, varbinary_value, image_value, "
        + "DATALENGTH(rowversion_value) AS rowversion_length, uniqueidentifier_value, sql_variant_value, "
        + "sysname_value FROM dbo.all_types ORDER BY id";

    private static readonly Setup WritesOpen = Writes(AllTypes);

    private static readonly Setup WritesClosed = WritesOpen with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = WritesOpen with { Transaction = CallerTransaction.Begun };

    private static readonly DateTime Second = new(2024, 3, 15, 13, 45, 30, DateTimeKind.Unspecified);

    // Row 1's datetime2, with all seven digits.
    private static readonly DateTime Datetime2 = Second.AddTicks(1234567);

    // Row 1 of the seed, with another id, in every column WriteAllTypes writes.
    private static readonly WriteAllTypesArgs EveryValue = new(
        Id: InsertedId,
        BigintValue: long.MinValue,
        IntValue: int.MaxValue,
        SmallintValue: short.MinValue,
        TinyintValue: byte.MaxValue,
        BitValue: true,
        DecimalValue: 12345678.1234m,
        NumericValue: -1234567.89m,
        MoneyValue: 922337203685477.5807m,
        SmallmoneyValue: -214748.3648m,
        FloatValue: -2.25,
        RealValue: 1.5f,
        DateValue: new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Unspecified),
        DatetimeValue: new DateTime(2024, 3, 15, 13, 45, 30, 3, DateTimeKind.Unspecified),
        Datetime2Value: Datetime2,
        SmalldatetimeValue: new DateTime(2024, 3, 15, 13, 46, 0, DateTimeKind.Unspecified),
        TimeValue: new TimeSpan(0, 13, 45, 30).Add(TimeSpan.FromTicks(1234567)),
        DatetimeoffsetValue: new DateTimeOffset(2024, 3, 15, 13, 45, 30, TimeSpan.FromHours(2)).AddTicks(1234567),
        CharValue: "héllo",
        VarcharValue: "Grüße, café",
        TextValue: "Ünïcödé text",
        NcharValue: "Zoë日本",
        NvarcharValue: "Grüße, 日本語",
        NtextValue: "Ζωή, 日本語",
        XmlValue: """<root><item id="1">Zoë</item></root>""",
        BinaryValue: [0xde, 0xad, 0xbe, 0xef],
        VarbinaryValue: [0x00, 0xff, 0x10, 0xee, 0x2a],
        ImageValue: [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
        UniqueidentifierValue: new Guid("123e4567-e89b-12d3-a456-426614174000"),
        SqlVariantValue: 42,
        SysnameValue: "a_name"
    );

    private static readonly WriteAllTypesArgs Nulls = new(
        InsertedId,
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
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null
    );

    // The columns of row 1 that Dapper can send exactly: all but the datetime2.
    private static readonly InsertAllTypesArgs WhatDapperSends = new(
        Id: InsertedId,
        BigintValue: EveryValue.BigintValue!.Value,
        IntValue: EveryValue.IntValue!.Value,
        SmallintValue: EveryValue.SmallintValue!.Value,
        TinyintValue: EveryValue.TinyintValue!.Value,
        BitValue: EveryValue.BitValue!.Value,
        DecimalValue: EveryValue.DecimalValue!.Value,
        NumericValue: EveryValue.NumericValue!.Value,
        MoneyValue: EveryValue.MoneyValue!.Value,
        SmallmoneyValue: EveryValue.SmallmoneyValue!.Value,
        FloatValue: EveryValue.FloatValue!.Value,
        RealValue: EveryValue.RealValue!.Value,
        DateValue: EveryValue.DateValue!.Value,
        DatetimeValue: EveryValue.DatetimeValue!.Value,
        SmalldatetimeValue: EveryValue.SmalldatetimeValue!.Value,
        TimeValue: EveryValue.TimeValue!.Value,
        DatetimeoffsetValue: EveryValue.DatetimeoffsetValue!.Value,
        CharValue: EveryValue.CharValue!,
        VarcharValue: EveryValue.VarcharValue!,
        TextValue: EveryValue.TextValue!,
        NcharValue: EveryValue.NcharValue!,
        NvarcharValue: EveryValue.NvarcharValue!,
        NtextValue: EveryValue.NtextValue!,
        XmlValue: EveryValue.XmlValue!,
        BinaryValue: EveryValue.BinaryValue!,
        VarbinaryValue: EveryValue.VarbinaryValue!,
        ImageValue: EveryValue.ImageValue!,
        UniqueidentifierValue: EveryValue.UniqueidentifierValue!.Value,
        SqlVariantValue: EveryValue.SqlVariantValue!,
        SysnameValue: EveryValue.SysnameValue!
    );

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(AllTypesQueriesExtensions);
            return
            [
                Of(host, "ListAllTypes", ScenarioTag.Ordinary, "open", Open),
                Of(host, "ListAllTypes", ScenarioTag.Ordinary, "closed", Closed),
                Of(host, "ListAllTypes", ScenarioTag.Ordinary, "in a transaction", InTransaction),
                Of(host, "InsertAllTypes", ScenarioTag.Ordinary, "row 1's values, open", WritesOpen, WhatDapperSends),
                Of(
                    host,
                    "InsertAllTypes",
                    ScenarioTag.Ordinary,
                    "row 1's values, closed",
                    WritesClosed,
                    WhatDapperSends
                ),
                Of(
                    host,
                    "InsertAllTypes",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WritesClosed,
                    WhatDapperSends with
                    {
                        Id = SqlServerAllTypesRows.FullRowId,
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
                        Id = SqlServerAllTypesRows.FullRowId,
                    }
                ),
                Of(host, "WriteAllTypes", ScenarioTag.Ordinary, "every value, open", WritesOpen, EveryValue),
                Of(host, "WriteAllTypes", ScenarioTag.Ordinary, "every value, closed", WritesClosed, EveryValue),
                Of(host, "WriteAllTypes", ScenarioTag.Ordinary, "nulls, in a transaction", WritesInTransaction, Nulls),
                // A decimal of more scale than its decimal(12,4) is rounded, half away from zero.
                Of(
                    host,
                    "WriteAllTypes",
                    ScenarioTag.Other,
                    "a decimal of more scale, closed",
                    WritesClosed,
                    Nulls with
                    {
                        DecimalValue = -1.23465m,
                    }
                ),
                Of(
                    host,
                    "WriteAllTypes",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WritesClosed,
                    Nulls with
                    {
                        Id = SqlServerAllTypesRows.NullRowId,
                    }
                ),
                Of(
                    host,
                    "WriteAllTypes",
                    ScenarioTag.DatabaseError,
                    "a varchar longer than its column, closed",
                    WritesClosed,
                    Nulls with
                    {
                        VarcharValue = new string('a', 21),
                    }
                ),
                Of(
                    host,
                    "WriteAllTypes",
                    ScenarioTag.DatabaseError,
                    "a varbinary longer than its column, in a transaction",
                    WritesInTransaction,
                    Nulls with
                    {
                        VarbinaryValue = [1, 2, 3, 4, 5, 6, 7, 8, 9],
                    }
                ),
                Of(
                    host,
                    "WriteAllTypes",
                    ScenarioTag.Other,
                    "a decimal outside its precision, in a transaction",
                    WritesInTransaction,
                    Nulls with
                    {
                        DecimalValue = 123456789.1m,
                    }
                ),
                // No rowversion of a row is known before the database is read, so the one given matches none.
                Of(
                    host,
                    "FindByRowversion",
                    ScenarioTag.Ordinary,
                    "no row, open",
                    Open,
                    new FindByRowversionArgs(new byte[8])
                ),
                Of(
                    host,
                    "FindByRowversion",
                    ScenarioTag.Ordinary,
                    "no row, closed",
                    Closed,
                    new FindByRowversionArgs(new byte[8])
                ),
                Of(
                    host,
                    "FindByDatetime2",
                    ScenarioTag.Ordinary,
                    "row 1, open",
                    Open,
                    new FindByDatetime2Args(Datetime2)
                ),
                Of(
                    host,
                    "FindByDatetime2",
                    ScenarioTag.Other,
                    "a tick later, closed",
                    Closed,
                    new FindByDatetime2Args(Datetime2.AddTicks(1))
                ),
                Of(host, "FindByChar", ScenarioTag.Ordinary, "row 1, open", Open, new FindByCharArgs("héllo")),
                Of(
                    host,
                    "FindByChar",
                    ScenarioTag.Other,
                    "a longer value, closed",
                    Closed,
                    new FindByCharArgs("héllo!")
                ),
                Of(
                    host,
                    "FindByDecimal",
                    ScenarioTag.Ordinary,
                    "row 1, open",
                    Open,
                    new FindByDecimalArgs(12345678.1234m)
                ),
                Of(
                    host,
                    "FindByDecimal",
                    ScenarioTag.Ordinary,
                    "row 1 by a value of more scale, closed",
                    Closed,
                    new FindByDecimalArgs(12345678.12344m)
                ),
                Of(
                    host,
                    "WriteDecimalsOfScale0",
                    ScenarioTag.Ordinary,
                    "fractions, open",
                    Open,
                    new WriteDecimalsOfScale0Args(1234.5m, -1234.5m)
                ),
                Of(
                    host,
                    "WriteDecimalsOfScale0",
                    ScenarioTag.Ordinary,
                    "a scale of 2, closed",
                    Closed,
                    new WriteDecimalsOfScale0Args(12345.00m, -12345.00m)
                ),
                Of(
                    host,
                    "WriteDecimalsOfScale0",
                    ScenarioTag.Other,
                    "a value outside numeric(5,0), in a transaction",
                    InTransaction,
                    new WriteDecimalsOfScale0Args(123456m, 0m)
                ),
                Of(
                    host,
                    "FindDecimalOfScale0",
                    ScenarioTag.Ordinary,
                    "1.5, open",
                    Open,
                    new FindDecimalOfScale0Args(1.5m)
                ),
                Of(
                    host,
                    "FindDecimalOfScale0",
                    ScenarioTag.Ordinary,
                    "-0.4, closed",
                    Closed,
                    new FindDecimalOfScale0Args(-0.4m)
                ),
                Of(
                    host,
                    "WriteDatetime2Of3",
                    ScenarioTag.Ordinary,
                    "seven digits, open",
                    Open,
                    new WriteDatetime2Of3Args(Datetime2)
                ),
                Of(
                    host,
                    "WriteDatetime2Of3",
                    ScenarioTag.Ordinary,
                    "seven digits, closed",
                    Closed,
                    new WriteDatetime2Of3Args(Second.AddTicks(9_995_000))
                ),
                Of(
                    host,
                    "FindDatetime2Of3",
                    ScenarioTag.Ordinary,
                    "its three digits, open",
                    Open,
                    new FindDatetime2Of3Args(Second.AddTicks(1_240_000))
                ),
                Of(
                    host,
                    "FindDatetime2Of3",
                    ScenarioTag.Other,
                    "seven digits, closed",
                    Closed,
                    new FindDatetime2Of3Args(Second.AddTicks(1_235_678))
                ),
                Of(
                    host,
                    "ListTinyintLevels",
                    ScenarioTag.Ordinary,
                    "high, open",
                    Open,
                    new ListTinyintLevelsArgs(Level.High)
                ),
                Of(
                    host,
                    "ListTinyintLevels",
                    ScenarioTag.Other,
                    "low, which no row holds, closed",
                    Closed,
                    new ListTinyintLevelsArgs(Level.Low)
                ),
            ];
        }
    }
}
