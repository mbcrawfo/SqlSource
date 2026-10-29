using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The body of the RowBufferQueryTests of both end-to-end projects, which run it with their generated methods.  Each
// theory reads the pooled method and its twin under no-array-pool, at a count on each side of the row buffer's sizes,
// and asserts that both return the numbers from 1 to the count, in order, as the collection the method declares.  An
// even count runs on a connection that the helper opened and an odd one on the closed connection that connect gives,
// which the methods open.
internal sealed class RowBufferReads<TConnection, TRow>(Func<TConnection> connect, Func<int, TRow> row)
    where TConnection : DbConnection
{
    public static TheoryData<int> Counts => [0, 1, 16, 17, 1024, 1025, 3000];

    // The rows of a method that declares another type, as the IEnumerable that the readers return.
    public static async Task<IEnumerable<TRow>> Rows<T>(Task<T> rows)
        where T : IEnumerable<TRow> => await rows;

    // Array, IEnumerable, IReadOnlyCollection and IReadOnlyList: the pooled method and its twin return a TRows of the
    // numbers.
    public Task ShouldReadOneToTheCountAsync<TRows>(
        int count,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> pooled,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> unpooled
    ) => ShouldReadOneToTheCountAsync<TRows>(count, pooled, unpooled, null);

    // List, IList and ICollection: as above with TRows a List<TRow>, then the pooled list is the caller's alone.
    public Task ShouldReadOneToTheCountIntoTheCallersListAsync(
        int count,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> pooled,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> unpooled
    ) =>
        ShouldReadOneToTheCountAsync<List<TRow>>(
            count,
            pooled,
            unpooled,
            (rows, connection, token) => ShouldBeTheCallersAloneAsync(rows, count, () => pooled(connection, token))
        );

    // ImmutableArray: as above with TRows an ImmutableArray<TRow> that is never the default, and Empty for no rows.
    public Task ShouldReadOneToTheCountIntoAnImmutableArrayAsync(
        int count,
        Func<TConnection, CancellationToken, Task<ImmutableArray<TRow>>> pooled,
        Func<TConnection, CancellationToken, Task<ImmutableArray<TRow>>> unpooled
    ) =>
        ShouldReadOneToTheCountAsync<ImmutableArray<TRow>>(
            count,
            (connection, token) => ImmutableRowsNeverDefault(pooled(connection, token), count),
            (connection, token) => ImmutableRowsNeverDefault(unpooled(connection, token), count),
            null
        );

    // Reads the pooled method and its twin under no-array-pool on one connection, and asserts that both return a TRows
    // and the numbers from 1 to the count, in order.  Then passes the pooled result, as the TRows it is, to afterwards.
    private async Task ShouldReadOneToTheCountAsync<TRows>(
        int count,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> pooled,
        Func<TConnection, CancellationToken, Task<IEnumerable<TRow>>> unpooled,
        Func<TRows, TConnection, CancellationToken, Task>? afterwards
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = connect();
        if (count % 2 == 0)
        {
            await connection.OpenAsync(token);
        }

        var expected = Enumerable.Range(1, count).Select(row).ToArray();

        var pooledRows = await pooled(connection, token);
        var unpooledRows = await unpooled(connection, token);

        var pooledTyped = pooledRows.ShouldBeOfType<TRows>();
        _ = unpooledRows.ShouldBeOfType<TRows>();
        pooledRows.ShouldBe(expected);
        unpooledRows.ShouldBe(pooledRows);
        if (afterwards is not null)
        {
            await afterwards(pooledTyped, connection, token);
        }
    }

    // A pooled list is the caller's alone: its capacity is the count, and after the caller clears it a second call of
    // the method gets the numbers from 1 to the count again, in a list of its own.
    private async Task ShouldBeTheCallersAloneAsync(List<TRow> rows, int count, Func<Task<IEnumerable<TRow>>> again)
    {
        rows.Capacity.ShouldBe(count);
        rows.Clear();

        var second = await again();

        var secondList = second.ShouldBeOfType<List<TRow>>();
        secondList.ShouldNotBeSameAs(rows);
        secondList.ShouldBe(Enumerable.Range(1, count).Select(row));
        secondList.Capacity.ShouldBe(count);
    }

    // The rows of a method that declares an ImmutableArray, boxed as the IEnumerable that the readers return.  It
    // asserts that the array is never the default and, for no rows, is ImmutableArray.Empty.
    private static async Task<IEnumerable<TRow>> ImmutableRowsNeverDefault(Task<ImmutableArray<TRow>> rows, int count)
    {
        var array = await rows;
        array.IsDefault.ShouldBeFalse();
        if (count == 0)
        {
            (array == ImmutableArray<TRow>.Empty).ShouldBeTrue();
        }

        return array;
    }
}
