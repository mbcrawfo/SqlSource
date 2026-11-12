using System.Collections.Generic;
using NodaTime;
using NpgsqlTypes;
using SqlSource.DatabaseTests.Postgres.SyncAsync.NodaTime;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// NodaTime/Queries/NodaTime.sql, on the data source with NodaTime: every column of noda_values written, with values and
// with nulls, and read back from a row that the scenario writes first, each on a copy of the seed, which holds no row
// of the table.  The values are those of NodaTimeQueryTests' row with a value in every column.
internal static class NodaTimeScenarios
{
    private const int RowId = 101;

    // A row with a value in every column, written by the server from its text.
    private const string InsertRow =
        "INSERT INTO noda_values VALUES (101, '2024-01-02 03:04:05.123456+00', '2024-01-02 03:04:05.123456', "
        + "'2024-02-29', '13:14:15.123456', '13:14:15.123456+05:30', '1 year 2 mons 3 days 04:05:06.789', "
        + "'[2024-01-02 03:04:05.123456+00,2024-02-01 00:00:00+00)', "
        + "'[2024-01-02 03:04:05.123456,2024-02-01 00:00:00)', "
        + "'[2024-02-29,2024-03-10)', '{[2024-01-02 03:04:05+00,2024-02-01 00:00:00+00)}', "
        + "'{[2024-01-02 03:04:05,2024-02-01 00:00:00)}', '{[2024-02-29,2024-03-10),[2024-04-01,2024-05-01)}', "
        + "'{\"2024-01-02 03:04:05+00\",\"2024-02-01 00:00:00+00\"}', '{\"2024-01-02 03:04:05\"}', "
        + "'{2024-02-29,2024-03-10}', '{13:14:15,23:59:59}', '{13:14:15+05:30,23:59:59-08}', "
        + "'{\"1 year 2 mons\",NULL,\"10 days\"}', '{\"[2024-01-02 03:04:05+00,2024-02-01 00:00:00+00)\"}', "
        + "'{\"[2024-01-02 03:04:05,2024-02-01 00:00:00)\"}', '{\"[2024-02-29,2024-03-10)\"}')";

    private static readonly Instant Instant1 = Instant.FromUtc(2024, 1, 2, 3, 4, 5).PlusNanoseconds(123_456_000);
    private static readonly Instant Instant2 = Instant.FromUtc(2024, 2, 1, 0, 0);
    private static readonly Instant Instant3 = Instant.FromUtc(2024, 3, 1, 12, 30);
    private static readonly Instant Instant4 = Instant.FromUtc(2024, 4, 1, 0, 0);
    private static readonly LocalDateTime Local1 = new LocalDateTime(2024, 1, 2, 3, 4, 5).PlusNanoseconds(123_456_000);
    private static readonly LocalDateTime Local2 = new(2024, 2, 1, 0, 0);
    private static readonly LocalDateTime Local3 = new(2024, 3, 1, 12, 30);
    private static readonly LocalDateTime Local4 = new(2024, 4, 1, 0, 0);
    private static readonly LocalDate Date1 = new(2024, 2, 29);
    private static readonly LocalDate Date2 = new(2024, 3, 10);
    private static readonly LocalDate Date3 = new(2024, 4, 1);
    private static readonly LocalDate Date4 = new(2024, 5, 1);
    private static readonly LocalTime Time1 = new LocalTime(13, 14, 15).PlusNanoseconds(123_456_000);
    private static readonly LocalTime Time2 = new(23, 59, 59);
    private static readonly OffsetTime OffsetTime1 = new(Time1, Offset.FromHoursAndMinutes(5, 30));
    private static readonly OffsetTime OffsetTime2 = new(Time2, Offset.FromHours(-8));

    private static readonly Period Period1 = new PeriodBuilder
    {
        Years = 1,
        Months = 2,
        Days = 3,
        Hours = 4,
        Minutes = 5,
        Seconds = 6,
        Milliseconds = 789,
    }.Build();

    private static readonly Period Period2 = Period.FromDays(10);

    // A value in every column, with a NULL element in interval[], the one array whose element type can hold null.
    private static readonly InsertNodaValuesArgs EveryValue = new(
        Id: RowId,
        TimestamptzValue: Instant1,
        TimestampValue: Local1,
        DateValue: Date1,
        TimeValue: Time1,
        TimetzValue: OffsetTime1,
        IntervalValue: Period1,
        TstzrangeValue: new NpgsqlRange<Instant>(Instant1, Instant2),
        TsrangeValue: new NpgsqlRange<LocalDateTime>(Local1, Local2),
        DaterangeValue: new NpgsqlRange<LocalDate>(Date1, true, Date2, false),
        TstzmultirangeValue: [new(Instant1, Instant2), new(Instant3, Instant4)],
        TsmultirangeValue: [new(Local1, Local2), new(Local3, Local4)],
        DatemultirangeValue: [new(Date1, true, Date2, false), new(Date3, true, Date4, false)],
        TimestamptzArrayValue: [Instant1, Instant2],
        TimestampArrayValue: [Local1, Local2],
        DateArrayValue: [Date1, Date2],
        TimeArrayValue: [Time1, Time2],
        TimetzArrayValue: [OffsetTime1, OffsetTime2],
        IntervalArrayValue: [Period1, null!, Period2],
        TstzrangeArrayValue: [new(Instant1, Instant2), new(Instant3, Instant4)],
        TsrangeArrayValue: [new(Local1, Local2), new(Local3, Local4)],
        DaterangeArrayValue: [new(Date1, true, Date2, false), new(Date3, true, Date4, false)]
    );

    private static readonly InsertNodaValuesArgs Nulls = new(
        Id: RowId,
        TimestamptzValue: null,
        TimestampValue: null,
        DateValue: null,
        TimeValue: null,
        TimetzValue: null,
        IntervalValue: null,
        TstzrangeValue: null,
        TsrangeValue: null,
        DaterangeValue: null,
        TstzmultirangeValue: null,
        TsmultirangeValue: null,
        DatemultirangeValue: null,
        TimestamptzArrayValue: null,
        TimestampArrayValue: null,
        DateArrayValue: null,
        TimeArrayValue: null,
        TimetzArrayValue: null,
        IntervalArrayValue: null,
        TstzrangeArrayValue: null,
        TsrangeArrayValue: null,
        DaterangeArrayValue: null
    );

    // The row as text, which keeps every fraction that a NodaTime value's own text leaves out.
    private static readonly Setup Writes = new(
        Source: "nodatime",
        Writes: true,
        Tables: ["SELECT noda_values::text FROM noda_values ORDER BY id"]
    );

    private static readonly Setup WritesClosed = Writes with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = Writes with { Transaction = CallerTransaction.Begun };

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(NodaTimeQueriesExtensions);
            var withRow = Writes with { Prepare = InsertRow };
            return
            [
                Of(host, "InsertNodaValues", ScenarioTag.Ordinary, "every value, open", Writes, EveryValue),
                Of(host, "InsertNodaValues", ScenarioTag.Ordinary, "every value, closed", WritesClosed, EveryValue),
                Of(
                    host,
                    "InsertNodaValues",
                    ScenarioTag.Ordinary,
                    "nulls, in a transaction",
                    WritesInTransaction,
                    Nulls
                ),
                Of(
                    host,
                    "InsertNodaValues",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WritesClosed with
                    {
                        Prepare = InsertRow,
                    },
                    Nulls
                ),
                Of(host, "GetNodaValues", ScenarioTag.Ordinary, "a row, open", withRow, new GetNodaValuesArgs(RowId)),
                Of(
                    host,
                    "GetNodaValues",
                    ScenarioTag.Ordinary,
                    "a row, closed",
                    withRow with
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
                    withRow with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetNodaValuesArgs(RowId)
                ),
                Of(
                    host,
                    "GetNodaValues",
                    ScenarioTag.NoRow,
                    "no row, closed",
                    new Setup(ConnectionStart.Closed, Source: "nodatime"),
                    new GetNodaValuesArgs(RowId)
                ),
            ];
        }
    }
}
