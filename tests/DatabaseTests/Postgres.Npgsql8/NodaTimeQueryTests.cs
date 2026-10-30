using System.Threading.Tasks;
using NodaTime;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of the linked NodaTime.sql on Npgsql 8, the oldest driver that is supported, through Npgsql.NodaTime 8:
// every column of noda_values written by InsertNodaValues and read back by GetNodaValues, in a transaction that is
// rolled back, with a copy of the rows of the end-to-end project's NodaTimeQueryTests, which proves the same on
// Npgsql 10.
[Collection(UsesApp.Name)]
public sealed class NodaTimeQueryTests(AppContainer app)
{
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

    // A period of every unit that interval keeps, which the plugin reads back unit by unit, not normalized.
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

    // A value in every column, the ranges [a,b), the multiranges of two ranges, and two elements in every array, with a
    // NULL element in interval[], the one array whose element type can hold null.
    private static readonly InsertNodaValuesArgs EveryValue = new(
        Id: 101,
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

    // An empty range in each range column and each array of ranges, and an empty multirange, which holds no range,
    // since PostgreSQL drops an empty range from a multirange.
    private static readonly InsertNodaValuesArgs EmptyRanges = Nulls(102) with
    {
        TstzrangeValue = NpgsqlRange<Instant>.Empty,
        TsrangeValue = NpgsqlRange<LocalDateTime>.Empty,
        DaterangeValue = NpgsqlRange<LocalDate>.Empty,
        TstzmultirangeValue = [],
        TsmultirangeValue = [],
        DatemultirangeValue = [],
        TstzrangeArrayValue = [NpgsqlRange<Instant>.Empty, new(Instant1, Instant2)],
        TsrangeArrayValue = [NpgsqlRange<LocalDateTime>.Empty, new(Local1, Local2)],
        DaterangeArrayValue = [NpgsqlRange<LocalDate>.Empty, new(Date1, true, Date2, false)],
    };

    // A range with no lower bound and one with no upper bound, in each range column, multirange and array of ranges.
    private static readonly InsertNodaValuesArgs UnboundedRanges = Nulls(103) with
    {
        TstzrangeValue = new NpgsqlRange<Instant>(default, false, true, Instant2, false, false),
        TsrangeValue = new NpgsqlRange<LocalDateTime>(Local1, true, false, default, false, true),
        DaterangeValue = new NpgsqlRange<LocalDate>(default, false, true, default, false, true),
        TstzmultirangeValue =
        [
            new(default, false, true, Instant2, false, false),
            new(Instant3, true, false, default, false, true),
        ],
        TsmultirangeValue = [new(Local1, true, false, default, false, true)],
        DatemultirangeValue = [new(default, false, true, Date2, false, false)],
        TstzrangeArrayValue = [new(Instant1, true, false, default, false, true)],
        TsrangeArrayValue = [new(default, false, true, Local2, false, false)],
        DaterangeArrayValue = [new(Date1, true, false, default, false, true)],
    };

    // A tstzrange that excludes its lower bound and includes its upper, which the plugin keeps as it is.
    private static readonly InsertNodaValuesArgs LowerExclusiveRange = Nulls(104) with
    {
        TstzrangeValue = new NpgsqlRange<Instant>(Instant1, false, Instant2, true),
    };

    [Fact]
    public async Task EveryRow_RoundTrips()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NodaTimeSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        foreach (var row in new[] { EveryValue, EmptyRanges, UnboundedRanges, LowerExclusiveRange })
        {
            var inserted = await connection.InsertNodaValues(row, transaction, cancellationToken: token);
            var read = await connection.GetNodaValues(
                new GetNodaValuesArgs(row.Id),
                transaction,
                cancellationToken: token
            );

            inserted.ShouldBe(1);
            ModelReflection.ShouldHoldTheValuesOf(read, row);
            if (ReferenceEquals(row, LowerExclusiveRange))
            {
                var range = read.TstzrangeValue.ShouldNotBeNull();
                range.LowerBoundIsInclusive.ShouldBeFalse();
                range.UpperBoundIsInclusive.ShouldBeTrue();
            }
        }

        await transaction.RollbackAsync(token);
    }

    [Fact]
    public async Task Nulls_RoundTrip()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NodaTimeSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var row = Nulls(105);

        var inserted = await connection.InsertNodaValues(row, transaction, cancellationToken: token);
        var read = await connection.GetNodaValues(new GetNodaValuesArgs(row.Id), transaction, cancellationToken: token);
        await transaction.RollbackAsync(token);

        inserted.ShouldBe(1);
        ModelReflection.ShouldHoldTheValuesOf(read, row);
        foreach (var property in ModelReflection.PropertiesOf(read.GetType()))
        {
            if (property.Name != nameof(read.Id))
            {
                property.GetValue(read).ShouldBeNull(property.Name);
            }
        }
    }

    // A data source of "app" with the plugin, as an application builds one for these methods.
    private NpgsqlDataSource NodaTimeSource()
    {
        var builder = new NpgsqlDataSourceBuilder(app.ConnectionString);
        _ = builder.UseNodaTime();
        return builder.Build();
    }

    // A row with NULL in every column but id.
    private static InsertNodaValuesArgs Nulls(int id) =>
        new(
            Id: id,
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
}
