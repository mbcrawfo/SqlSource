using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The queries of Queries/RowBuffers.sql, run through the methods that RowBufferQueries gets, at counts on each side of
// the row buffer's sizes: none, one, its first buffer of 16 and one more, the largest buffer it rents, of 1,024, and
// one more, and 3,000, which grows past it into plain arrays.  Each method returns the numbers from 1 to the count, in
// order, in an array whatever type it declares, and its twin under no-array-pool, which reads without the pool, returns
// the same rows.  An even count runs on an open connection and an odd one on a closed one, which the methods open.
[Collection(UsesPostgres.Name)]
public sealed class RowBufferQueryTests(PostgresContainer postgres)
{
    public static TheoryData<int> Counts => [0, 1, 16, 17, 1024, 1025, 3000];

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync(
            count,
            (connection, token) => Rows(connection.NumbersAsArray(new(count), cancellationToken: token)),
            (connection, token) => Rows(connection.NumbersNoPoolAsArray(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsIEnumerable_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync(
            count,
            (connection, token) => connection.NumbersAsIEnumerable(new(count), cancellationToken: token),
            (connection, token) => connection.NumbersNoPoolAsIEnumerable(new(count), cancellationToken: token)
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsIReadOnlyCollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync(
            count,
            (connection, token) => Rows(connection.NumbersAsIReadOnlyCollection(new(count), cancellationToken: token)),
            (connection, token) =>
                Rows(connection.NumbersNoPoolAsIReadOnlyCollection(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsIReadOnlyList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync(
            count,
            (connection, token) => Rows(connection.NumbersAsIReadOnlyList(new(count), cancellationToken: token)),
            (connection, token) => Rows(connection.NumbersNoPoolAsIReadOnlyList(new(count), cancellationToken: token))
        );

    // Reads the rows through the pooled method and its twin under no-array-pool, on one connection: each an array of
    // the numbers from 1 to the count, in order.
    private async Task ShouldReadOneToTheCountAsync(
        int count,
        Func<NpgsqlConnection, CancellationToken, Task<IEnumerable<CountedNumber>>> pooled,
        Func<NpgsqlConnection, CancellationToken, Task<IEnumerable<CountedNumber>>> unpooled
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection =
            count % 2 == 0 ? await source.OpenConnectionAsync(token) : source.CreateConnection();
        var expected = Enumerable.Range(1, count).Select(n => new CountedNumber(n)).ToArray();

        var pooledRows = await pooled(connection, token);
        var unpooledRows = await unpooled(connection, token);

        pooledRows.ShouldBeOfType<CountedNumber[]>().ShouldBe(expected);
        unpooledRows.ShouldBeOfType<CountedNumber[]>().ShouldBe(pooledRows);
    }

    // The rows of a method that declares another type, as the IEnumerable that the readers return.
    private static async Task<IEnumerable<CountedNumber>> Rows<T>(Task<T> rows)
        where T : IEnumerable<CountedNumber> => await rows;
}
