using System;
using System.Collections.Generic;
using NodaTime;
using SqlSource.DatabaseTests.SqlServer.SyncAsync.NodaTime;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// NodaTime/Queries/: every column of noda_values written, by InsertNodaValues and InsertNodaOverrides, with values and
// with nulls, and read back from a row that the scenario writes first, each on a copy of the seed, which holds no row
// of the table.  The values are those of NodaTimeQueryTests: the least and the greatest of each column, and an Instant
// and a Duration to the tick.  The errors are a duplicate id and a Duration of 24 hours, which SqlClient refuses before
// sending it.
internal static class NodaTimeScenarios
{
    private const int RowId = 201;

    // The last tick of a day, 23:59:59.9999999.
    private const long LastTickOfDay = TimeSpan.TicksPerDay - 1;

    // A row with a value in every column, written by the server from its text.
    private const string InsertRow =
        "INSERT INTO dbo.noda_values (id, date_value, time_value, datetime2_value, datetime_value, "
        + "smalldatetime_value, datetimeoffset_value, instant_value, instant_offset_value, duration_value) VALUES "
        + "(201, '2024-02-29', '13:14:15.1234567', '2024-01-02T03:04:05.1234567', '2024-01-02T03:04:05.997', "
        + "'2024-01-02T03:04:00', '2024-01-02T03:04:05.1234567+05:30', '2024-01-02T03:04:05.1234567', "
        + "'2024-02-29T23:59:59.9999999-08:00', '23:59:59.9999999')";

    // Every column of the table, with all seven digits of each fraction.
    private static readonly Setup WritesOpen = Writes("SELECT * FROM dbo.noda_values ORDER BY id");

    private static readonly Setup WritesClosed = WritesOpen with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = WritesOpen with { Transaction = CallerTransaction.Begun };

    private static readonly Setup WithRow = WritesOpen with { Prepare = InsertRow };

    // The least value of each of the six columns.
    private static readonly InsertNodaValuesArgs Least = new(
        Id: RowId,
        DateValue: new LocalDate(1, 1, 1),
        TimeValue: LocalTime.Midnight,
        Datetime2Value: new LocalDateTime(1, 1, 1, 0, 0),
        DatetimeValue: new LocalDateTime(1753, 1, 1, 0, 0),
        SmalldatetimeValue: new LocalDateTime(1900, 1, 1, 0, 0),
        DatetimeoffsetValue: new OffsetDateTime(new LocalDateTime(1, 1, 1, 0, 0), Offset.FromHours(-14))
    );

    // The greatest value of each of the six columns, to the last tick that each holds.
    private static readonly InsertNodaValuesArgs Greatest = new(
        Id: RowId,
        DateValue: new LocalDate(9999, 12, 31),
        TimeValue: LocalTime.FromTicksSinceMidnight(LastTickOfDay),
        Datetime2Value: new LocalDate(9999, 12, 31).At(LocalTime.FromTicksSinceMidnight(LastTickOfDay)),
        DatetimeValue: new LocalDateTime(9999, 12, 31, 23, 59, 59, 997),
        SmalldatetimeValue: new LocalDateTime(2079, 6, 6, 23, 59),
        DatetimeoffsetValue: new OffsetDateTime(
            new LocalDate(9999, 12, 31).At(LocalTime.FromTicksSinceMidnight(LastTickOfDay)),
            Offset.FromHours(14)
        )
    );

    private static readonly InsertNodaValuesArgs Nulls = new(RowId, null, null, null, null, null, null);

    // An Instant through datetime2 and through datetimeoffset, to the tick, and a Duration of the last tick of a day.
    private static readonly InsertNodaOverridesArgs Overrides = new(
        Id: RowId,
        InstantValue: Instant.FromUtc(2024, 1, 2, 3, 4, 5).PlusTicks(1_234_567),
        InstantOffsetValue: Instant.FromUtc(2024, 2, 29, 23, 59, 59).PlusTicks(9_999_999),
        DurationValue: Duration.FromTicks(LastTickOfDay)
    );

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(NodaTimeQueriesExtensions);
            return
            [
                Of(host, "InsertNodaValues", ScenarioTag.Ordinary, "the greatest values, open", WritesOpen, Greatest),
                Of(host, "InsertNodaValues", ScenarioTag.Ordinary, "the least values, closed", WritesClosed, Least),
                Of(
                    host,
                    "InsertNodaValues",
                    ScenarioTag.Ordinary,
                    "nulls, in a transaction",
                    WritesInTransaction,
                    Nulls
                ),
                // Nanoseconds below a tick are dropped, never rounded up.
                Of(
                    host,
                    "InsertNodaValues",
                    ScenarioTag.Other,
                    "nanoseconds below a tick, closed",
                    WritesClosed,
                    Nulls with
                    {
                        TimeValue = new LocalTime(13, 14, 15).PlusNanoseconds(999_999_999),
                        Datetime2Value = new LocalDate(2024, 1, 2).At(
                            new LocalTime(13, 14, 15).PlusNanoseconds(999_999_999)
                        ),
                    }
                ),
                Of(
                    host,
                    "InsertNodaValues",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WithRow with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    Nulls
                ),
                Of(
                    host,
                    "InsertNodaValues",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, in a transaction",
                    WithRow with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    Nulls
                ),
                Of(host, "GetNodaValues", ScenarioTag.Ordinary, "a row, open", WithRow, new GetNodaValuesArgs(RowId)),
                Of(
                    host,
                    "GetNodaValues",
                    ScenarioTag.Ordinary,
                    "a row, closed",
                    WithRow with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetNodaValuesArgs(RowId)
                ),
                Of(
                    host,
                    "GetNodaValues",
                    ScenarioTag.Ordinary,
                    "a row, in a transaction",
                    WithRow with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetNodaValuesArgs(RowId)
                ),
                Of(host, "GetNodaValues", ScenarioTag.NoRow, "no row, closed", Closed, new GetNodaValuesArgs(RowId)),
                Of(host, "InsertNodaOverrides", ScenarioTag.Ordinary, "values, open", WritesOpen, Overrides),
                Of(host, "InsertNodaOverrides", ScenarioTag.Ordinary, "values, closed", WritesClosed, Overrides),
                Of(
                    host,
                    "InsertNodaOverrides",
                    ScenarioTag.Ordinary,
                    "nulls, in a transaction",
                    WritesInTransaction,
                    new InsertNodaOverridesArgs(RowId, null, null, null)
                ),
                // time(7) holds less than a day, and SqlClient refuses a TimeSpan of 24 hours before it sends.
                Of(
                    host,
                    "InsertNodaOverrides",
                    ScenarioTag.Other,
                    "a duration of 24 hours, in a transaction",
                    WritesInTransaction,
                    Overrides with
                    {
                        DurationValue = Duration.FromHours(24),
                    }
                ),
                Of(
                    host,
                    "InsertNodaOverrides",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WithRow with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    Overrides
                ),
                Of(
                    host,
                    "GetNodaOverrides",
                    ScenarioTag.Ordinary,
                    "a row, open",
                    WithRow,
                    new GetNodaOverridesArgs(RowId)
                ),
                Of(
                    host,
                    "GetNodaOverrides",
                    ScenarioTag.Ordinary,
                    "a row, in a transaction",
                    WithRow with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetNodaOverridesArgs(RowId)
                ),
                Of(
                    host,
                    "GetNodaOverrides",
                    ScenarioTag.NoRow,
                    "no row, closed",
                    Closed,
                    new GetNodaOverridesArgs(RowId)
                ),
            ];
        }
    }
}
