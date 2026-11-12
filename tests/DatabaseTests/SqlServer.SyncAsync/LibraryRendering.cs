using System.Globalization;
using Microsoft.SqlServer.Types;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NodaTime;
using NodaTime.Text;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// The text of a NodaTime value or a NetTopologySuite geometry with all it holds, for SqlServerEngine.Render, since the
// harness references neither library: their own ToString drops a time's sub-second digits, a date's era and calendar,
// and a geometry's M ordinate and SRID, so a synchronous method that lost one would render as its twin.  A
// SqlGeography's or a SqlGeometry's own ToString drops its SRID too.
internal static class LibraryRendering
{
    public static string? Of(object value) =>
        value switch
        {
            Instant instant => InstantPattern.ExtendedIso.Format(instant),
            LocalDateTime local => LocalDateTimePattern.FullRoundtrip.Format(local),
            LocalDate date => LocalDatePattern.FullRoundtrip.Format(date),
            LocalTime time => LocalTimePattern.ExtendedIso.Format(time),
            OffsetDateTime offset => OffsetDateTimePattern.FullRoundtrip.Format(offset),
            OffsetDate date => OffsetDatePattern.FullRoundtrip.Format(date),
            OffsetTime time => OffsetTimePattern.ExtendedIso.Format(time),
            ZonedDateTime zoned => OffsetDateTimePattern.FullRoundtrip.Format(zoned.ToOffsetDateTime())
                + " "
                + zoned.Zone.Id,
            Geometry geometry => "SRID="
                + geometry.SRID.ToString(CultureInfo.InvariantCulture)
                + ";"
                + new WKTWriter(4).Write(geometry),
            SqlGeography geography => "SRID=" + geography.STSrid.ToString() + ";" + geography.ToString(),
            SqlGeometry geometry => "SRID=" + geometry.STSrid.ToString() + ";" + geometry.ToString(),
            _ => null,
        };
}
