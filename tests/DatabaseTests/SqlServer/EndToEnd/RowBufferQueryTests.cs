using System.Threading.Tasks;
using Xunit;
using Reads = SqlSource.DatabaseTests.TestHarness.EndToEnd.RowBufferReads<
    Microsoft.Data.SqlClient.SqlConnection,
    SqlSource.DatabaseTests.SqlServer.EndToEnd.CountedNumber
>;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The queries of Queries/RowBuffers.sql, run through the methods that RowBufferQueries gets, at counts on each side of
// the row buffer's sizes: none, one, its first buffer of 16 and one more, the largest buffer it rents, of 1,024, and
// one more, and 3,000, which grows past it into plain arrays.  Each of the eight types that read through the buffer,
// the array, the three read-only interfaces, the List and IList and ICollection and the ImmutableArray, returns the
// numbers from 1 to the count, in order, and its twin under no-array-pool, which reads without the pool, returns the
// same rows.  The array and the three read-only interfaces return an array, List and the two interfaces over it a List,
// and ImmutableArray one that is never the default, ImmutableArray.Empty when there are no rows.  A pooled list is the
// caller's alone: its capacity is the count, and clearing it changes no later call.  An even count runs on an open
// connection and an odd one on a closed one, which the methods open.  The body is RowBufferReads of the harness.
[Collection(UsesSqlServer.Name)]
public sealed class RowBufferQueryTests(SqlServerDatabases sqlServer)
{
    private readonly Reads _reads = new(() => EndToEndSources.App(sqlServer), static n => new CountedNumber(n));

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) => Reads.Rows(connection.NumbersAsArray(new(count), cancellationToken: token)),
            (connection, token) => Reads.Rows(connection.NumbersNoPoolAsArray(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsIEnumerable_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) => connection.NumbersAsIEnumerable(new(count), cancellationToken: token),
            (connection, token) => connection.NumbersNoPoolAsIEnumerable(new(count), cancellationToken: token)
        );

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsIReadOnlyCollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) =>
                Reads.Rows(connection.NumbersAsIReadOnlyCollection(new(count), cancellationToken: token)),
            (connection, token) =>
                Reads.Rows(connection.NumbersNoPoolAsIReadOnlyCollection(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsIReadOnlyList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountAsync<CountedNumber[]>(
            count,
            (connection, token) => Reads.Rows(connection.NumbersAsIReadOnlyList(new(count), cancellationToken: token)),
            (connection, token) =>
                Reads.Rows(connection.NumbersNoPoolAsIReadOnlyList(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountIntoTheCallersListAsync(
            count,
            (connection, token) => Reads.Rows(connection.NumbersAsList(new(count), cancellationToken: token)),
            (connection, token) => Reads.Rows(connection.NumbersNoPoolAsList(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsIList_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountIntoTheCallersListAsync(
            count,
            (connection, token) => Reads.Rows(connection.NumbersAsIList(new(count), cancellationToken: token)),
            (connection, token) => Reads.Rows(connection.NumbersNoPoolAsIList(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsICollection_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountIntoTheCallersListAsync(
            count,
            (connection, token) => Reads.Rows(connection.NumbersAsICollection(new(count), cancellationToken: token)),
            (connection, token) =>
                Reads.Rows(connection.NumbersNoPoolAsICollection(new(count), cancellationToken: token))
        );

    [Theory]
    [MemberData(nameof(Reads.Counts), MemberType = typeof(Reads))]
    public Task NumbersAsImmutableArray_EachCount_ReturnsOneToTheCountAsItsNoPoolTwinDoes(int count) =>
        _reads.ShouldReadOneToTheCountIntoAnImmutableArrayAsync(
            count,
            (connection, token) => connection.NumbersAsImmutableArray(new(count), cancellationToken: token),
            (connection, token) => connection.NumbersNoPoolAsImmutableArray(new(count), cancellationToken: token)
        );
}
