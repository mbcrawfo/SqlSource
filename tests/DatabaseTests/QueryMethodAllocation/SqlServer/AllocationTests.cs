using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer.Shapes;
using Xunit;

namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The bytes that a generated method allocates for each call, against a hand-written ADO.NET method of the same calls:
// ShapeQueries.SearchAccounts, with two parameters and three rows of the seed of "app".  The hand-written method makes
// the generated method's calls, one by one: the open/close rule of the methods, the command and its arguments, the
// binding of each parameter, the reads of each column, and the same rows in the same collection, an IReadOnlyList with
// an array behind it, which it can only build by copying what it read the rows into, since the count of the rows is not
// known until they are read.  It reads them into what the generated method does, so there are two pairs:
// SearchAccounts, which reads its rows into the pooled buffer, against a hand-written method that reads them into
// RowBufferByHand of this project, the same buffer by hand; and SearchAccountsUnpooled, a copy of
// the query under no-array-pool, which reads them into a List, against a hand-written method that does the same.  The
// copy is a query of its own because SearchAccounts' own list of generator parameters, sort-input, replaces the list of
// any claim's attribute, and UnpooledCopyTests holds its SQL to SearchAccounts'.  The numbers queries of
// RowBufferQueries have a pair of each kind for the List, IList, ICollection and ImmutableArray, at 3 rows: the pooled
// method against rows read into RowBufferByHand and copied by ToList or ToImmutableArray, and its twin under
// no-array-pool against a List that is returned as it is or an ImmutableArray builder that is drained, since those are
// what the generated method returns.  Three more tests hold the pooled NumbersAsArray, NumbersAsList and
// NumbersAsImmutableArray, at 300 rows, to allocating less than a List copied to an array, a List grown by doubling and
// an ImmutableArray builder that is drained, each by at least a given number of bytes, since a pooled buffer exists to
// save a list's growth arrays and the pairs above, against RowBufferByHand, cannot see what the buffer itself costs.
// The streaming StreamAccounts of StreamingQueries has a pair of its own, against a hand-written static local async
// iterator that makes the same calls and yields each row, for the whole of a call, from the first MoveNextAsync to the
// disposal.  In each pair the two make the same calls, so any object that the generated method allocates on each call
// and the hand-written one does not fails the test.  The two are compared by the commonest difference of pairs of
// calls, one of each method, which CallAllocations explains.  GC.GetTotalAllocatedBytes(true) counts every thread,
// where the driver completes the continuations of an asynchronous call, so the class is in the collection
// MeasuresSqlServer, which runs alone on a container of its own, and the assembly runs no two tests at once, and it
// skips under coverage, whose collector allocates in the process too.
[Collection(MeasuresSqlServer.Name)]
public sealed class AllocationTests(SqlServerApp sqlServer)
{
    // The SQL of SearchAccounts, as the generated method sends it.
    private const string SearchAccountsSql =
        "SELECT id, name\nFROM dbo.accounts\nWHERE name LIKE @name OR id = @accountId\nORDER BY id;";

    // The first calls pay for one-off work: JIT compilation, the driver's static state, the pool's connection and the
    // server's plan of the query.  Fifty of each were enough on both images when the counts were set: the calls after
    // them fell no further.
    private const int WarmUpCalls = 50;

    // The measured pairs of calls.  A call of SearchAccounts allocated about 6.2 to 6.7 KB on an open connection and
    // 6.6 to 7.1 KB on a closed one, and a call of a numbers query at 3 rows about 5.8 to 5.9 KB on an open one and 6.2
    // to 6.3 KB on a closed one, on SQL Server 2025 and 2017 alike when the count was set.  More than four pairs in
    // five differed by the commonest difference; a run of the test takes some seconds.
    private const int Pairs = 500;

    // The bytes a call of the generated method may allocate over the hand-written one's, by the commonest difference of
    // a pair, the same for every pair.  It was 0 for all ten pairs, SearchAccounts and SearchAccountsUnpooled and the
    // eight of the numbers queries, NumbersAsList, IList, ICollection and ImmutableArray through the pool and under
    // no-array-pool, on both connections, on every one of six runs when this was set, three on SQL Server 2025 and
    // three on 2017.  (The numbers pairs were 8 while their hand-written methods took the count and not the input model
    // that the generated methods take; given the model, they fell to 0.)  It does not move with the driver's share of
    // a call or with another thread's work.  The streaming StreamAccounts allocated about 5.0 KB a call on an open
    // connection and 5.4 KB on a closed one, the same as its hand-written iterator: 0 by the commonest difference, in
    // more than four pairs in five, on every one of four runs, three on SQL Server 2025 and one on 2017.  The margin is
    // below the 24 bytes of the smallest object of a 64-bit process, so that any object the generated method allocates
    // for each call and the hand-written does not fails the test, and an extra object did, at 24 bytes, and an array of
    // 64 bytes, at 88, failed both pairs of SearchAccounts.  It lets the generated method's state machine hold up to
    // two more fields of 8 bytes than the hand-written one's.  Never raise it to make a change pass.
    private const long MarginInBytesPerCall = 16;

    // A call allocates its command, its parameters, its reader and its rows: some kilobytes on the drivers that the
    // project runs.  A call below this measured nothing.
    private const long LeastBytesPerCall = 1024;

    // The SQL of StreamAccounts, as the generated method sends it.
    private const string StreamAccountsSql = "SELECT id, name\nFROM dbo.accounts\nORDER BY id;";

    // The SQL of the numbers queries of RowBuffers.sql, as the generated methods send it.
    private const string NumbersSql =
        "WITH digits (v) AS (SELECT v FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS d (v))\n"
        + "SELECT n\nFROM (\n    SELECT d1.v + 10 * d2.v + 100 * d3.v + 1000 * d4.v + 1 AS n\n    FROM digits AS d1\n"
        + "    CROSS JOIN digits AS d2\n    CROSS JOIN digits AS d3\n    CROSS JOIN digits AS d4\n) AS numbers\n"
        + "WHERE n <= @count\nORDER BY n;";

    // The count of the numbers queries of the pairs below: three rows, as SearchAccounts reads.
    private const int CountedRows = 3;

    // The count of the few-hundred-rows test: past the buffer's first size of 16, where a list's growth arrays matter.
    private const int FewHundredRows = 300;

    // The bytes for each call that the pooled method must save on a few hundred rows against a List copied to an array,
    // by the commonest difference of a pair of calls.  The list of 300 rows grows through arrays of 4 to 512 elements,
    // 8,352 bytes with their headers, which the pooled method does not allocate.  The commonest difference was -8,376
    // bytes, in 450 to 469 of 500 pairs, on each of six runs on both connections, three on SQL Server 2025 and three on
    // 2017; the next were -8,672 and -8,080, in some twenty pairs each, and a few of the 500 were far from any of them.
    // So 8,000 is 376 bytes below the commonest and 80 below the least of the next, room for the driver's paths and not
    // for a growth array, and far above 4,256, the saving of a pooled method that allocated even the largest growth
    // array, of 512 elements, itself.  It is raised when the pool saves more and never lowered to make a change pass.
    private const long LeastSavingInBytesPerCall = 8_000;

    // The bytes for each call that the pooled NumbersAsList must save on a few hundred rows against a List that grows
    // by doubling and is returned as it is, by the commonest difference of a pair of calls.  The hand-written list ends
    // with an array of 512 elements and has allocated the growth arrays of 4 to 256 before it, 8,352 bytes in all with
    // their headers; the pooled method allocates the one array of 300 elements that its list holds, so the saving is
    // the growth arrays less that array.  The commonest difference was -5,920 bytes, in 444 to 478 of 500 pairs, on
    // each of six runs on both connections, three on SQL Server 2025 and three on 2017; the next were -5,624 and
    // -6,216, in some ten to thirty pairs each.  So 5,500 is 420 bytes below the commonest and 124 below the least of
    // the next, room for the driver's paths and not for a buffer of 64 elements.  A ToList that did not return its last
    // buffer to the pool, the 512 elements of 4,120 bytes, saved 1,800 bytes only, at the commonest, which is far below
    // 5,500 and fails the test.  It is raised when the pool saves more and never lowered to make a change pass.
    private const long LeastListSavingInBytesPerCall = 5_500;

    // The bytes for each call that the pooled NumbersAsImmutableArray must save on a few hundred rows against an
    // ImmutableArray builder that is drained, by the commonest difference of a pair of calls.  The builder grows
    // through arrays of 16 to 512 elements, 8,208 bytes with their headers, then copies the rows into an array of 300
    // elements when it is drained; the pooled method allocates that array alone, which the immutable array wraps.  The
    // commonest difference was -8,320 bytes, in 434 to 477 of 500 pairs, on each of six runs on both connections, three
    // on SQL Server 2025 and three on 2017; the next were -8,616 and -8,024, in some ten to thirty pairs each.  So
    // 8,000 is 320 bytes below the commonest and 24 below the least of the next, room for the driver's paths and not
    // for a growth array.  A ToImmutableArray that copied the 300 rows a second time, an array of 2,424 bytes, saved
    // 5,896 bytes only, at the commonest, which is far below 8,000 and fails the test, as does one that did not return
    // its last buffer.  It is raised when the pool saves more and never lowered to make a change pass.
    private const long LeastImmutableArraySavingInBytesPerCall = 8_000;

    // "%o%" matches bob and carol, and the id alice.
    private static readonly SearchAccountsFilter Filter = new() { Name = "%o%", AccountId = 1 };

    // The rows of StreamAccounts.
    private static readonly CollectedAccount[] CollectedAccounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    private static readonly SearchAccountsView[] Rows = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // The rows of a numbers query at CountedRows.
    private static readonly CountedNumber[] CountedNumbers = [new(1), new(2), new(3)];

    private static readonly MeasureSettings Settings = new(WarmUpCalls, Pairs, LeastBytesPerCall);

    // A closed connection is opened and closed again by each call; an open one is left open.
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task SearchAccounts_ThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var generatedRows = await ShapeQueries.SearchAccounts(connection, Filter, cancellationToken: token);
        generatedRows.ShouldBe(Rows);
        (await SearchAccountsPooledByHandAsync(connection, Filter, cancellationToken: token)).ShouldBe(generatedRows);

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => ShapeQueries.SearchAccounts(connection, Filter, cancellationToken: token),
            () => SearchAccountsPooledByHandAsync(connection, Filter, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task SearchAccountsUnpooled_UnderNoArrayPool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var generatedRows = await ShapeQueries.SearchAccountsUnpooled(connection, Filter, cancellationToken: token);
        generatedRows.ShouldBe(Rows);
        (await SearchAccountsUnpooledByHandAsync(connection, Filter, cancellationToken: token)).ShouldBe(generatedRows);

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => ShapeQueries.SearchAccountsUnpooled(connection, Filter, cancellationToken: token),
            () => SearchAccountsUnpooledByHandAsync(connection, Filter, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersAsList_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersAsList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListPooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsList(parameters, cancellationToken: token),
            () => NumbersAsListPooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersAsIList_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersAsIList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListPooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsIList(parameters, cancellationToken: token),
            () => NumbersAsListPooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersAsICollection_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersAsICollection(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListPooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsICollection(parameters, cancellationToken: token),
            () => NumbersAsListPooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersAsImmutableArray_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersAsImmutableArray(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsImmutableArrayPooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsImmutableArray(parameters, cancellationToken: token),
            () => NumbersAsImmutableArrayPooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersNoPoolAsList_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersNoPoolAsList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsList(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersNoPoolAsIList_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersNoPoolAsIList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsIList(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersNoPoolAsICollection_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersNoPoolAsICollection(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsICollection(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersNoPoolAsImmutableArray_ThreeRows_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(CountedRows);
        (await connection.NumbersNoPoolAsImmutableArray(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsImmutableArrayUnpooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsImmutableArray(parameters, cancellationToken: token),
            () => NumbersAsImmutableArrayUnpooledByHandAsync(connection, parameters, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    // Many rows, where the pool matters: a list that grows by doubling to hold 300 rows allocates growth arrays of 4 to
    // 512 elements, about 8.4 KB in all, and the pooled method rents its buffers from the pool, so it allocates the
    // 300 rows and the final array alone.  So the generated method allocates less than the hand-written one, by about
    // the list's growth arrays.
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersAsArray_AFewHundredRows_AllocatesLessThanAListCopiedToAnArray(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        (await connection.NumbersAsArray(parameters, cancellationToken: token)).ShouldBe(expected);
        (await NumbersAsArrayUnpooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(expected);

        await Allocations.ShouldSaveAtLeastAsync(
            () => connection.NumbersAsArray(parameters, cancellationToken: token),
            () => NumbersAsArrayUnpooledByHandAsync(connection, parameters, cancellationToken: token),
            LeastSavingInBytesPerCall,
            Settings
        );
    }

    // Many rows, for the list: the pooled NumbersAsList rents its buffers and copies the rows into a List of the exact
    // size, so it allocates that list and its array of 300 elements.  A List that grows by doubling to hold the rows,
    // returned as it is, allocates growth arrays of 4 to 512 elements and ends with the array of 512.
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersAsList_AFewHundredRows_AllocatesLessThanAGrownList(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        (await connection.NumbersAsList(parameters, cancellationToken: token)).ShouldBe(expected);
        (await NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(expected);

        await Allocations.ShouldSaveAtLeastAsync(
            () => connection.NumbersAsList(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, parameters, cancellationToken: token),
            LeastListSavingInBytesPerCall,
            Settings
        );
    }

    // Many rows, for the immutable array: the pooled NumbersAsImmutableArray rents its buffers and wraps the exact
    // array of 300 elements that it copies the rows into, and a builder that is drained grows through arrays of 16 to
    // 512 elements, then copies the rows into an array of the exact size.
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task NumbersAsImmutableArray_AFewHundredRows_AllocatesLessThanABuilder(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        var parameters = new NumbersCount(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        (await connection.NumbersAsImmutableArray(parameters, cancellationToken: token)).ShouldBe(expected);
        (await NumbersAsImmutableArrayUnpooledByHandAsync(connection, parameters, cancellationToken: token)).ShouldBe(
            expected
        );

        await Allocations.ShouldSaveAtLeastAsync(
            () => connection.NumbersAsImmutableArray(parameters, cancellationToken: token),
            () => NumbersAsImmutableArrayUnpooledByHandAsync(connection, parameters, cancellationToken: token),
            LeastImmutableArraySavingInBytesPerCall,
            Settings
        );
    }

    // The streaming method returns an iterator and yields each row as the reader reads it, with no buffer, so a call is
    // the method, its iterator and the enumeration to the end, which both sides do: the hand-written iterator makes the
    // calls that the generated one does, so what the generated method allocates beyond it is its own state.
    [Theory]
    [InlineData(ConnectionState.Closed)]
    [InlineData(ConnectionState.Open)]
    public async Task StreamAccounts_ToTheEnd_AllocatesNoMoreThanByHand(ConnectionState state)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await Allocations.InStateAsync(connection, state, token);

        (await connection.StreamAccounts(cancellationToken: token).ToListAsync(token)).ShouldBe(CollectedAccounts);
        (await StreamAccountsByHand(connection, cancellationToken: token).ToListAsync(token)).ShouldBe(
            CollectedAccounts
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            async () =>
            {
                var sum = 0;
                await foreach (var account in connection.StreamAccounts(cancellationToken: token).ConfigureAwait(false))
                {
                    sum += account.Id;
                }

                return sum;
            },
            async () =>
            {
                var sum = 0;
                await foreach (
                    var account in StreamAccountsByHand(connection, cancellationToken: token).ConfigureAwait(false)
                )
                {
                    sum += account.Id;
                }

                return sum;
            },
            MarginInBytesPerCall,
            Settings
        );
    }

    // SearchAccounts by hand: the arguments of the generated method, and its calls, the rows into RowBufferByHand.
    private static async Task<IReadOnlyList<SearchAccountsView>> SearchAccountsPooledByHandAsync(
        DbConnection connection,
        SearchAccountsFilter parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = SearchAccountsSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // nvarchar(4000), or nvarchar(max) for a longer value, and DBNull for null, as the generated method binds.
            var name = (SqlParameter)command.CreateParameter();
            name.ParameterName = "name";
            name.SqlDbType = SqlDbType.NVarChar;
            name.Size = 4000;
            name.Value = (object?)parameters.Name ?? DBNull.Value;
            if (name.Value is string { Length: > 4000 })
            {
                name.Size = -1;
            }

            _ = command.Parameters.Add(name);

            var accountId = (SqlParameter)command.CreateParameter();
            accountId.ParameterName = "accountId";
            accountId.SqlDbType = SqlDbType.Int;
            accountId.Value = parameters.AccountId;
            _ = command.Parameters.Add(accountId);

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = new RowBufferByHand<SearchAccountsView>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The generated method's reads: a row's columns are in its buffer once ReadAsync has returned.
#pragma warning disable CA1849
                rows.Add(new SearchAccountsView(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
#pragma warning restore CA1849
            }

            // The array behind the IReadOnlyList.
            return rows.ToArray();
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    // SearchAccountsUnpooled by hand: the arguments of the generated method, and its calls, the rows into a List copied
    // to an array.
    private static async Task<IReadOnlyList<SearchAccountsView>> SearchAccountsUnpooledByHandAsync(
        DbConnection connection,
        SearchAccountsFilter parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = SearchAccountsSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // nvarchar(4000), or nvarchar(max) for a longer value, and DBNull for null, as the generated method binds.
            var name = (SqlParameter)command.CreateParameter();
            name.ParameterName = "name";
            name.SqlDbType = SqlDbType.NVarChar;
            name.Size = 4000;
            name.Value = (object?)parameters.Name ?? DBNull.Value;
            if (name.Value is string { Length: > 4000 })
            {
                name.Size = -1;
            }

            _ = command.Parameters.Add(name);

            var accountId = (SqlParameter)command.CreateParameter();
            accountId.ParameterName = "accountId";
            accountId.SqlDbType = SqlDbType.Int;
            accountId.Value = parameters.AccountId;
            _ = command.Parameters.Add(accountId);

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = new List<SearchAccountsView>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The generated method's reads: a row's columns are in its buffer once ReadAsync has returned.
#pragma warning disable CA1849
                rows.Add(new SearchAccountsView(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
#pragma warning restore CA1849
            }

            // The array behind the IReadOnlyList; a collection expression would wrap it in another object.
#pragma warning disable IDE0305
            return rows.ToArray();
#pragma warning restore IDE0305
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    // NumbersAsList of Queries/RowBuffers.sql, by hand, the rows into RowBufferByHand and copied to a List, as
    // NumbersAsIList and NumbersAsICollection read them too.
    private static async Task<List<CountedNumber>> NumbersAsListPooledByHandAsync(
        DbConnection connection,
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = new RowBufferByHand<CountedNumber>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The generated method's reads: a row's column is in its buffer once ReadAsync has returned.
#pragma warning disable CA1849
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
#pragma warning restore CA1849
            }

            return rows.ToList();
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    // NumbersAsImmutableArray of Queries/RowBuffers.sql, by hand, the rows into RowBufferByHand and copied to an
    // ImmutableArray.
    private static async Task<ImmutableArray<CountedNumber>> NumbersAsImmutableArrayPooledByHandAsync(
        DbConnection connection,
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = new RowBufferByHand<CountedNumber>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The generated method's reads: a row's column is in its buffer once ReadAsync has returned.
#pragma warning disable CA1849
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
#pragma warning restore CA1849
            }

            return rows.ToImmutableArray();
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    // NumbersNoPoolAsList of Queries/RowBuffers.sql under no-array-pool, by hand, the rows into a List that is
    // returned, as NumbersNoPoolAsIList and NumbersNoPoolAsICollection read them too.
    private static async Task<List<CountedNumber>> NumbersAsListUnpooledByHandAsync(
        DbConnection connection,
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = new List<CountedNumber>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The generated method's reads: a row's column is in its buffer once ReadAsync has returned.
#pragma warning disable CA1849
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
#pragma warning restore CA1849
            }

            return rows;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    // NumbersNoPoolAsImmutableArray of Queries/RowBuffers.sql under no-array-pool, by hand, the rows into a builder
    // that is drained.
    private static async Task<ImmutableArray<CountedNumber>> NumbersAsImmutableArrayUnpooledByHandAsync(
        DbConnection connection,
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = ImmutableArray.CreateBuilder<CountedNumber>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The generated method's reads: a row's column is in its buffer once ReadAsync has returned.
#pragma warning disable CA1849
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
#pragma warning restore CA1849
            }

            return rows.DrainToImmutable();
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    // NumbersAsArray of Queries/RowBuffers.sql, by hand, the rows into a List, copied to an array: the baseline of
    // List and ToArray() that the pooled NumbersAsArray is held against for a few hundred rows.
    private static async Task<CountedNumber[]> NumbersAsArrayUnpooledByHandAsync(
        DbConnection connection,
        NumbersCount parameters,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            await using var commandScope = command.ConfigureAwait(false);
            command.CommandText = NumbersSql;
            command.Transaction = transaction;
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }

            // int, as the generated method binds.
            var countParameter = (SqlParameter)command.CreateParameter();
            countParameter.ParameterName = "count";
            countParameter.SqlDbType = SqlDbType.Int;
            countParameter.Value = parameters.Count;
            _ = command.Parameters.Add(countParameter);

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
            await using var readerScope = reader.ConfigureAwait(false);
            var rows = new List<CountedNumber>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // The generated method's reads: a row's column is in its buffer once ReadAsync has returned.
#pragma warning disable CA1849
                rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
#pragma warning restore CA1849
            }

            // A collection expression would wrap the array in another object.
#pragma warning disable IDE0305
            return rows.ToArray();
#pragma warning restore IDE0305
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    // StreamAccounts of Queries/Streaming.sql, by hand: a method that checks its argument at the call and returns a
    // static local iterator, which opens a closed connection, yields each row inside the try and closes the connection
    // it opened in the finally.
    private static IAsyncEnumerable<CollectedAccount> StreamAccountsByHand(
        DbConnection connection,
        DbTransaction? transaction = null,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(connection);
        return Iterate(connection, transaction, commandTimeout, cancellationToken);

        static async IAsyncEnumerable<CollectedAccount> Iterate(
            DbConnection connection,
            DbTransaction? transaction,
            int? commandTimeout,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            var wasClosed = connection.State == ConnectionState.Closed;
            if (wasClosed)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = StreamAccountsSql;
                    command.Transaction = transaction;
                    if (commandTimeout.HasValue)
                    {
                        command.CommandTimeout = commandTimeout.Value;
                    }

                    var reader = await command
                        .ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                        .ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            // The generated method's reads: a row's columns are in its buffer once ReadAsync returns.
#pragma warning disable CA1849
                            yield return new CollectedAccount(
                                reader.GetFieldValue<int>(0),
                                reader.GetFieldValue<string>(1)
                            );
#pragma warning restore CA1849
                        }
                    }
                }
            }
            finally
            {
                if (wasClosed)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
