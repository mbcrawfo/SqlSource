using System;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NodaTime;
using NpgsqlTypes;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// Values that their own ToString renders the same, which the engine's rendering tells apart; no test needs the server.
public sealed class LibraryRenderingTests
{
    private static readonly LocalDateTime Moment = new(2024, 1, 2, 3, 4, 5);

    private static readonly Offset Plus = Offset.FromHours(1);

    private static readonly GeometryFactory Wgs84 = new(new PrecisionModel(), 4326);

    // Two values of a kind, alike but for what their own ToString drops.
    private static (object, object) PairOf(string kind) =>
        kind switch
        {
            "Instant" => (Instant.FromUnixTimeTicks(0), Instant.FromUnixTimeTicks(1_234_567)),
            "LocalDateTime" => (Moment, Moment.PlusTicks(1_234_567)),
            "LocalDateTime calendar" => (Moment, Moment.WithCalendar(CalendarSystem.Julian).PlusDays(13)),
            "LocalDate era" => (new LocalDate(-5, 3, 1), new LocalDate(6, 3, 1)),
            "LocalTime" => (Moment.TimeOfDay, Moment.TimeOfDay.PlusTicks(1_234_567)),
            "OffsetDateTime" => (Moment.WithOffset(Plus), Moment.PlusTicks(1_234_567).WithOffset(Plus)),
            "OffsetDate calendar" => (
                new OffsetDate(Moment.Date, Plus),
                new OffsetDate(new LocalDate(2024, 1, 2, CalendarSystem.Julian), Plus)
            ),
            "OffsetTime" => (
                new OffsetTime(Moment.TimeOfDay, Plus),
                new OffsetTime(Moment.TimeOfDay.PlusTicks(1_234_567), Plus)
            ),
            "ZonedDateTime" => (Moment.InUtc(), Moment.PlusTicks(1_234_567).InUtc()),
            "Geometry M" => (Wgs84.CreatePoint(new CoordinateM(1, 2, 3)), Wgs84.CreatePoint(new CoordinateM(1, 2, 4))),
            "Geometry SRID" => (
                Wgs84.CreatePoint(new Coordinate(1, 2)),
                new GeometryFactory(new PrecisionModel(), 3857).CreatePoint(new Coordinate(1, 2))
            ),
            _ => throw new ArgumentException($"No pair of kind '{kind}'.", nameof(kind)),
        };

    [Theory]
    [InlineData("Instant")]
    [InlineData("LocalDateTime")]
    [InlineData("LocalDateTime calendar")]
    [InlineData("LocalDate era")]
    [InlineData("LocalTime")]
    [InlineData("OffsetDateTime")]
    [InlineData("OffsetDate calendar")]
    [InlineData("OffsetTime")]
    [InlineData("ZonedDateTime")]
    [InlineData("Geometry M")]
    [InlineData("Geometry SRID")]
    public void Of_ValuesTheirOwnTextMakesAlike_RenderApart(string kind)
    {
        var (left, right) = PairOf(kind);

        Rendering.Of(left).ShouldBe(Rendering.Of(right));
        LibraryRendering.Of(left).ShouldNotBeNull().ShouldNotBe(LibraryRendering.Of(right));
    }

    // The same instant at the same offset in another zone, which its own ToString tells apart too.
    [Fact]
    public void Of_ZonedDateTimesInOtherZones_RenderApart() =>
        LibraryRendering
            .Of(Moment.InUtc())
            .ShouldNotBe(LibraryRendering.Of(Moment.InZoneStrictly(DateTimeZoneProviders.Tzdb["Europe/London"])));

    private static readonly DateTime Midnight = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Two ranges alike but for one part: a bound's value, which the range's own ToString writes by the bound's, or
    // its emptiness, inclusivity or infinity.
    private static (object, object) RangesOf(string kind) =>
        kind switch
        {
            "Instant" => (
                new NpgsqlRange<Instant>(Instant.FromUnixTimeTicks(0), Instant.FromUnixTimeTicks(10_000_000)),
                new NpgsqlRange<Instant>(Instant.FromUnixTimeTicks(1_234_567), Instant.FromUnixTimeTicks(10_000_000))
            ),
            "LocalDateTime" => (
                new NpgsqlRange<LocalDateTime>(Moment, Moment.PlusHours(1)),
                new NpgsqlRange<LocalDateTime>(Moment.PlusTicks(1_234_567), Moment.PlusHours(1))
            ),
            "DateTime" => (
                new NpgsqlRange<DateTime>(Midnight, true, false, default, false, true),
                new NpgsqlRange<DateTime>(Midnight.AddMilliseconds(500), true, false, default, false, true)
            ),
            "DateTime kind" => (
                new NpgsqlRange<DateTime>(Midnight, true, false, default, false, true),
                new NpgsqlRange<DateTime>(
                    DateTime.SpecifyKind(Midnight, DateTimeKind.Unspecified),
                    true,
                    false,
                    default,
                    false,
                    true
                )
            ),
            "empty" => (NpgsqlRange<int>.Empty, new NpgsqlRange<int>(0, true, false, 0, true, false)),
            "infinite" => (
                new NpgsqlRange<int>(0, true, true, 5, false, false),
                new NpgsqlRange<int>(0, true, false, 5, false, false)
            ),
            "inclusive" => (new NpgsqlRange<int>(0, true, 5, false), new NpgsqlRange<int>(0, false, 5, false)),
            _ => throw new ArgumentException($"No pair of kind '{kind}'.", nameof(kind)),
        };

    // A range is rendered by its parts, its bounds through the engine's Render, so a bound that lost its sub-second
    // digits or its Kind renders apart, where the range's own ToString, which writes each bound by its own, does not.
    [Theory]
    [InlineData("Instant", true)]
    [InlineData("LocalDateTime", true)]
    [InlineData("DateTime", true)]
    [InlineData("DateTime kind", true)]
    [InlineData("empty", false)]
    [InlineData("infinite", false)]
    [InlineData("inclusive", false)]
    public void RenderingOf_RangesAlikeButForOnePart_RenderApart(string kind, bool ownTextAlike)
    {
        var (left, right) = RangesOf(kind);

        (left.ToString() == right.ToString()).ShouldBe(ownTextAlike);
        Rendering.Of(left, LibraryRendering.Of).ShouldNotBe(Rendering.Of(right, LibraryRendering.Of));
    }

    [Fact]
    public void Of_ValueOfNoLibrary_IsNull() => LibraryRendering.Of(Moment.TimeOfDay.ToTimeOnly()).ShouldBeNull();

    // The runner renders a result and a table's values through the engine's Render, which is this rendering.
    [Fact]
    public async Task Render_LibraryValue_IsItsRendering()
    {
        await using var engine = new PostgresEngine(null!);
        var value = Wgs84.CreatePoint(new CoordinateM(1, 2, 3));

        engine.Render(value).ShouldBe(LibraryRendering.Of(value));
    }
}
