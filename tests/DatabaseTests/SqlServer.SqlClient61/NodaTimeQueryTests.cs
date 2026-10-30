using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NodaTime;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The queries of the linked EndToEnd/NodaTime/Queries/ on SqlClient 6.1, the oldest driver that is supported, and
// NodaTime 3.1.9, the oldest version that SqlSource supports: every column of noda_values written by InsertNodaValues
// or InsertNodaOverrides and read back by GetNodaValues or GetNodaOverrides, in a transaction that is rolled back,
// with a copy of the rows and tests of the end-to-end project's NodaTimeQueryTests, which proves the same on
// SqlClient 7 and NodaTime 3.3.7.
[Collection(UsesApp.Name)]
public sealed class NodaTimeQueryTests(AppContainer app)
{
    // The last tick of a day, 23:59:59.9999999: time(7) holds ticks, and nothing after the last.
    private const long LastTickOfDay = TimeSpan.TicksPerDay - 1;

    // The least value of each of the six columns: datetimeoffset's least local time at -14:00, the least offset, whose
    // instant is inside DateTimeOffset's range.
    private static readonly InsertNodaValuesArgs Least = new(
        Id: 201,
        DateValue: new LocalDate(1, 1, 1),
        TimeValue: LocalTime.Midnight,
        Datetime2Value: new LocalDateTime(1, 1, 1, 0, 0),
        DatetimeValue: new LocalDateTime(1753, 1, 1, 0, 0),
        SmalldatetimeValue: new LocalDateTime(1900, 1, 1, 0, 0),
        DatetimeoffsetValue: new OffsetDateTime(new LocalDateTime(1, 1, 1, 0, 0), Offset.FromHours(-14))
    );

    // The greatest value of each of the six columns, to the last tick that each holds: datetime's .997, since it keeps
    // three-hundredths of a second, smalldatetime's minute, and datetimeoffset's greatest local time at +14:00, the
    // greatest offset, whose instant is inside DateTimeOffset's range.
    private static readonly InsertNodaValuesArgs Greatest = new(
        Id: 202,
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

    // An Instant through datetime2 and through datetimeoffset, to the tick, and a Duration of the last tick of a day,
    // the greatest that time(7) holds.
    private static readonly InsertNodaOverridesArgs Overrides = new(
        Id: 203,
        InstantValue: Instant.FromUtc(2024, 1, 2, 3, 4, 5).PlusTicks(1_234_567),
        InstantOffsetValue: Instant.FromUtc(2024, 2, 29, 23, 59, 59).PlusTicks(9_999_999),
        DurationValue: Duration.FromTicks(LastTickOfDay)
    );

    [Theory]
    [InlineData(nameof(Least))]
    [InlineData(nameof(Greatest))]
    public async Task InsertNodaValues_EachDefault_RoundTrips(string limit)
    {
        var row = limit == nameof(Least) ? Least : Greatest;

        var (inserted, read) = await WriteAndReadAsync(row);

        inserted.ShouldBe(1);
        ModelReflection.ShouldHoldTheValuesOf(read, row);
    }

    [Fact]
    public async Task InsertNodaValues_AllNull_ReadsNull()
    {
        var row = new InsertNodaValuesArgs(
            Id: 204,
            DateValue: null,
            TimeValue: null,
            Datetime2Value: null,
            DatetimeValue: null,
            SmalldatetimeValue: null,
            DatetimeoffsetValue: null
        );

        var (inserted, read) = await WriteAndReadAsync(row);

        inserted.ShouldBe(1);
        read.ShouldBe(new GetNodaValuesDto(204, null, null, null, null, null, null));
    }

    [Fact]
    public async Task InsertNodaOverrides_InstantDurationAndOffset_RoundTrip()
    {
        var token = TestContext.Current.CancellationToken;
        var inserted = 0;
        GetNodaOverridesDto? read = null;
        await using var connection = new SqlConnection(app.ConnectionString);

        await RolledBackAsync(
            connection,
            async transaction =>
            {
                inserted = await connection.InsertNodaOverrides(Overrides, transaction, cancellationToken: token);
                read = await connection.GetNodaOverrides(
                    new GetNodaOverridesArgs(Overrides.Id),
                    transaction,
                    cancellationToken: token
                );
            },
            token
        );

        inserted.ShouldBe(1);
        ModelReflection.ShouldHoldTheValuesOf(read.ShouldNotBeNull(), Overrides);
    }

    // A tick is 100 nanoseconds, and the conversion to SqlClient's type drops what is below it, never rounding up.
    [Fact]
    public async Task InsertNodaValues_NanosecondsBelowATick_AreTruncated()
    {
        var time = new LocalTime(13, 14, 15).PlusNanoseconds(999_999_999);
        var local = new LocalDate(2024, 1, 2).At(time);
        var truncatedTime = new LocalTime(13, 14, 15).PlusTicks(9_999_999);
        var truncatedLocal = new LocalDate(2024, 1, 2).At(truncatedTime);
        var row = new InsertNodaValuesArgs(
            Id: 205,
            DateValue: null,
            TimeValue: time,
            Datetime2Value: local,
            DatetimeValue: null,
            SmalldatetimeValue: null,
            DatetimeoffsetValue: new OffsetDateTime(local, Offset.FromHours(2))
        );

        var (inserted, read) = await WriteAndReadAsync(row);

        inserted.ShouldBe(1);
        read.TimeValue.ShouldBe(truncatedTime);
        read.Datetime2Value.ShouldBe(truncatedLocal);
        read.DatetimeoffsetValue.ShouldBe(new OffsetDateTime(truncatedLocal, Offset.FromHours(2)));
    }

    // time(7) holds less than a day, and SqlClient refuses a TimeSpan of 24 hours before it sends.
    [Fact]
    public async Task InsertNodaOverrides_DurationOf24Hours_ThrowsOverflowException()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(app.ConnectionString);

        var thrown = await Should.ThrowAsync<OverflowException>(() =>
            RolledBackAsync(
                connection,
                transaction =>
                    connection.InsertNodaOverrides(
                        Overrides with
                        {
                            Id = 206,
                            DurationValue = Duration.FromHours(24),
                        },
                        transaction,
                        cancellationToken: token
                    ),
                token
            )
        );

        thrown.Message.ShouldContain("SqlDbType.Time");
    }

    // Writes the row through InsertNodaValues and reads it back through GetNodaValues, in a transaction that is rolled
    // back.
    private async Task<(int Inserted, GetNodaValuesDto Read)> WriteAndReadAsync(InsertNodaValuesArgs row)
    {
        var token = TestContext.Current.CancellationToken;
        var inserted = 0;
        GetNodaValuesDto? read = null;
        await using var connection = new SqlConnection(app.ConnectionString);

        await RolledBackAsync(
            connection,
            async transaction =>
            {
                inserted = await connection.InsertNodaValues(row, transaction, cancellationToken: token);
                read = await connection.GetNodaValues(
                    new GetNodaValuesArgs(row.Id),
                    transaction,
                    cancellationToken: token
                );
            },
            token
        );

        return (inserted, read.ShouldNotBeNull());
    }

    // Opens the connection and runs the write in a transaction that is rolled back when it is disposed, given to every
    // call, since SqlClient refuses a command on a connection whose transaction it is not given.
    private static async Task RolledBackAsync(
        SqlConnection connection,
        Func<DbTransaction, Task> write,
        CancellationToken cancellationToken
    )
    {
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await write(transaction);
    }
}
