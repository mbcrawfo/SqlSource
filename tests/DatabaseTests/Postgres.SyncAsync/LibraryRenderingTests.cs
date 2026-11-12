using System;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using NodaTime;
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
