using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/RowBuffers.sql, run through the methods that RowBufferQueries gets, at counts on each side of
// the row buffer's sizes: none, one, its first buffer of 16 and one more, the largest buffer it rents, of 1,024, and
// one more, and 3,000, which grows past it into plain arrays.  Each of the eight types that read through the buffer,
// the array, the three read-only interfaces, the List and IList and ICollection and the ImmutableArray, returns the
// numbers from 1 to the count, in order, and its twin under no-array-pool, which reads without the pool, returns the
// same rows.  The array and the three read-only interfaces return an array, List and the two interfaces over it a List,
// and ImmutableArray one that is never the default, ImmutableArray.Empty when there are no rows.  A pooled list is the
// caller's alone: its capacity is the count, and clearing it changes no later call.  An even count runs on an open
// connection and an odd one on a closed one, which the methods open.
[Collection(UsesSqlServer.Name)]
public sealed class RowBufferQueryTests(SqlServerDatabases sqlServer)
{
    public static TheoryData<int> Counts => [0, 1, 16, 17, 1024, 1025, 3000];

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) => Rows(connection.NumbersAsArray(new(count), cancellationToken: token)),
            (connection, token) => Rows(connection.NumbersNoPoolAsArray(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsIEnumerable_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) => connection.NumbersAsIEnumerable(new(count), cancellationToken: token),
            (connection, token) => connection.NumbersNoPoolAsIEnumerable(new(count), cancellationToken: token)
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsIReadOnlyCollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) => Rows(connection.NumbersAsIReadOnlyCollection(new(count), cancellationToken: token)),
            (connection, token) =>
                Rows(connection.NumbersNoPoolAsIReadOnlyCollection(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsIReadOnlyList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) => Rows(connection.NumbersAsIReadOnlyList(new(count), cancellationToken: token)),
            (connection, token) => Rows(connection.NumbersNoPoolAsIReadOnlyList(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<List<CountedNumber>>(
            count,
            (connection, token) => Rows(connection.NumbersAsList(new(count), cancellationToken: token)),
            (connection, token) => Rows(connection.NumbersNoPoolAsList(new(count), cancellationToken: token)),
            (rows, connection, token) =>
                ShouldBeTheCallersAloneAsync(
                    rows,
                    count,
                    () => Rows(connection.NumbersAsList(new(count), cancellationToken: token))
                )
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsIList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<List<CountedNumber>>(
            count,
            (connection, token) => Rows(connection.NumbersAsIList(new(count), cancellationToken: token)),
            (connection, token) => Rows(connection.NumbersNoPoolAsIList(new(count), cancellationToken: token)),
            (rows, connection, token) =>
                ShouldBeTheCallersAloneAsync(
                    rows,
                    count,
                    () => Rows(connection.NumbersAsIList(new(count), cancellationToken: token))
                )
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsICollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<List<CountedNumber>>(
            count,
            (connection, token) => Rows(connection.NumbersAsICollection(new(count), cancellationToken: token)),
            (connection, token) => Rows(connection.NumbersNoPoolAsICollection(new(count), cancellationToken: token)),
            (rows, connection, token) =>
                ShouldBeTheCallersAloneAsync(
                    rows,
                    count,
                    () => Rows(connection.NumbersAsICollection(new(count), cancellationToken: token))
                )
        );

    [Theory]
    [MemberData(nameof(Counts))]
    public Task NumbersAsImmutableArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        ShouldReadOneToTheCountAsync<ImmutableArray<CountedNumber>>(
            count,
            (connection, token) =>
                ImmutableRowsNeverDefault(
                    connection.NumbersAsImmutableArray(new(count), cancellationToken: token),
                    count
                ),
            (connection, token) =>
                ImmutableRowsNeverDefault(
                    connection.NumbersNoPoolAsImmutableArray(new(count), cancellationToken: token),
                    count
                )
        );

    // Reads the pooled method and its twin under no-array-pool on one connection, and asserts that both return a TRows
    // and the numbers from 1 to the count, in order.  Then passes the pooled result, as the TRows it is, to afterwards.
    private async Task ShouldReadOneToTheCountAsync<TRows>(
        int count,
        Func<SqlConnection, CancellationToken, Task<IEnumerable<CountedNumber>>> pooled,
        Func<SqlConnection, CancellationToken, Task<IEnumerable<CountedNumber>>> unpooled,
        Func<TRows, SqlConnection, CancellationToken, Task>? afterwards = null
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        if (count % 2 == 0)
        {
            await connection.OpenAsync(token);
        }

        var expected = Enumerable.Range(1, count).Select(n => new CountedNumber(n)).ToArray();

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
    private static async Task ShouldBeTheCallersAloneAsync(
        List<CountedNumber> rows,
        int count,
        Func<Task<IEnumerable<CountedNumber>>> again
    )
    {
        rows.Capacity.ShouldBe(count);
        rows.Clear();

        var second = await again();

        var secondList = second.ShouldBeOfType<List<CountedNumber>>();
        secondList.ShouldNotBeSameAs(rows);
        secondList.ShouldBe(Enumerable.Range(1, count).Select(n => new CountedNumber(n)));
        secondList.Capacity.ShouldBe(count);
    }

    // The rows of a method that declares another type, as the IEnumerable that the readers return.
    private static async Task<IEnumerable<CountedNumber>> Rows<T>(Task<T> rows)
        where T : IEnumerable<CountedNumber> => await rows;

    // The rows of a method that declares an ImmutableArray, boxed as the IEnumerable that the readers return.  It
    // asserts that the array is never the default and, for no rows, is ImmutableArray.Empty.
    private static async Task<IEnumerable<CountedNumber>> ImmutableRowsNeverDefault(
        Task<ImmutableArray<CountedNumber>> rows,
        int count
    )
    {
        var array = await rows;
        array.IsDefault.ShouldBeFalse();
        if (count == 0)
        {
            (array == ImmutableArray<CountedNumber>.Empty).ShouldBeTrue();
        }

        return array;
    }
}
