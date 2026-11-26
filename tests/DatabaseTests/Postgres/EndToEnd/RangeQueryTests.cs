using System.Threading.Tasks;
using NpgsqlTypes;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The query of Queries/Ranges.sql, run on the seed of "app" through its generated method, on a data source that calls
// EnableUnmappedTypes(): the manual's mapping of a range of a user's own builds, reads the range and its multirange,
// and sends a range parameter.
[Collection(UsesPostgres.Name)]
public sealed class RangeQueryTests(PostgresDatabases postgres)
{
    [Fact]
    public async Task ListReadingsOverlapping_RangeOverEveryReading_ReadsTheRangesAndTheMultirange()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.AppWithUnmappedTypes(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var readings = await connection.ListReadingsOverlapping(
            new ListReadingsOverlappingArgs(new NpgsqlRange<double>(0, 100)),
            cancellationToken: token
        );

        readings.Count.ShouldBe(2);
        readings[0].Id.ShouldBe(1);
        readings[0].Span.ShouldBe(new NpgsqlRange<double>(1.5, true, 2.5, false));
        readings[0]
            .Spans.ShouldBe([new NpgsqlRange<double>(1, true, 2, false), new NpgsqlRange<double>(3, false, 4, true)]);
        readings[1].Id.ShouldBe(2);
        readings[1].Span.ShouldBe(new NpgsqlRange<double>(10, false, 20, true));
        readings[1].Spans.ShouldBeNull();
    }

    [Fact]
    public async Task ListReadingsOverlapping_RangeOverOneReading_SendsTheRange()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.AppWithUnmappedTypes(postgres);
        await using var connection = await source.OpenConnectionAsync(token);

        var readings = await connection.ListReadingsOverlapping(
            new ListReadingsOverlappingArgs(new NpgsqlRange<double>(15, 16)),
            cancellationToken: token
        );

        readings.ShouldHaveSingleItem().Id.ShouldBe(2);
    }
}
