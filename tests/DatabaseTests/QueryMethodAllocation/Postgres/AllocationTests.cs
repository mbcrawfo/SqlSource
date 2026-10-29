using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres;

// A generated method allocates no more for each call than a hand-written ADO.NET method of the same calls, written as
// the manual's recipe for your own code writes one and as a careful developer would: it honours the same contract,
// opening a closed connection and closing it again in a finally, setting the transaction and, when given, the timeout,
// and passing the token; it binds the parameter as the generated method does, an NpgsqlParameter<int> named without
// its '@' and with its DataTypeName, since binding by the described type is decided and its cost is the driver's on
// both sides; and it reads each column by its ordinal with GetFieldValue into the same model, and the rows into an
// array, the collection the method returns, through what the generated method reads them into.  So each query has two
// pairs: its method of AccountQueries, which reads its rows into the pooled buffer, against a hand-written method that
// reads them into RowBufferByHand of this project, the same buffer by hand; and its method of
// UnpooledAccountQueries, the same query under no-array-pool, which reads them into a List and copies it, against a
// hand-written method that does the same.  The numbers queries of RowBufferQueries have a pair of each kind for the
// List, IList, ICollection and ImmutableArray, at 3 rows: the pooled method against rows read into RowBufferByHand and
// copied by ToList or ToImmutableArray, and its twin under no-array-pool against a List that is returned as it is or an
// ImmutableArray builder that is drained, since those are what the generated method returns.  Three more tests hold
// the pooled NumbersAsArray, NumbersAsList and NumbersAsImmutableArray, at 300 rows, to allocating less than a List
// copied to an array, a List grown by doubling and an ImmutableArray builder that is drained, each by at least a given
// number of bytes, since a pooled buffer exists to save a list's growth arrays and the pairs above, against
// RowBufferByHand, cannot see what the buffer itself costs.  The streaming StreamAccounts of StreamingQueries has a
// pair of its own, against a hand-written static local async iterator that makes the same calls and yields each row,
// for the whole of a call, from the first MoveNextAsync to the disposal.  In each pair the two make the same calls, so
// what the generated method allocates beyond the hand-written one is the difference of their own state, which the
// margin holds, and an object that it allocates on each call shows.  Both run on the same open connection of one data
// source, with the same token, and ConfigureAwait(false) on each await.  The two are compared by the commonest
// difference of pairs of calls, one of each method, which CallAllocations explains.  GC.GetTotalAllocatedBytes counts
// every thread, since an awaited continuation runs where the driver completes it, so the class is in
// the collection MeasuresPostgres, which runs alone on a container of its own, and the assembly runs no two tests at
// once.  That keeps other tests out of the count, but not the process's own background work, which the commonest
// difference leaves out, and the class skips under coverage, whose collector allocates in the process too often for it
// to leave out.
[Collection(MeasuresPostgres.Name)]
public sealed class AllocationTests(PostgresApp postgres)
{
    // The bytes for each call that the generated method may allocate beyond the hand-written one, by the commonest
    // difference of a pair of calls, the same for every pair.  A call of the accounts queries allocated about 1.6 to
    // 2.7 KB here when this was set, and the two methods of each pair differed by less than an object, the difference
    // of their own state, on every run: through the pool, the generated GetAccountName allocated 16 bytes more than the
    // hand-written one, ListAccounts the same and ListClosedBefore 8 bytes less; under no-array-pool, GetAccountName 8
    // bytes more, ListAccounts 8 bytes less and ListClosedBefore 16 bytes less.  The numbers queries at 3 rows
    // allocated about 1.6 to 2.3 KB a call, and the generated method of each of their eight pairs, NumbersAsList,
    // IList, ICollection and ImmutableArray through the pool and under no-array-pool, allocated 16 bytes more than the
    // hand-written one on every run.  The streaming StreamAccounts allocated about 2.1 KB a call, the same as its
    // hand-written iterator on every run.  A boxed value is 24 bytes.  The binding's own cost, the driver's for each
    // parameter and its DataTypeName, is in both sides alike.  So an extra boxed value, array or closure on each call
    // shows, a few of them exceed the margin, and the commonest difference does not move with the driver's share of a
    // call: an array of 64 bytes, 88 with its header, on each call of the generated methods failed every test of both
    // pairs, at differences of 72 to 104 bytes.  Never above 128.
    private const long MarginInBytesPerCall = 64;

    // The first calls pay for one-off work: JIT compilation, static initialisers, the pools of the driver and of the
    // runtime, and the statement's first parse.
    private const int WarmUpCalls = 200;

    // The measured pairs of calls.  When the count was set, more than four pairs in five differed by the commonest
    // difference.
    private const int Pairs = 500;

    private const string GetAccountNameSql = "SELECT id, name\nFROM accounts\nWHERE id = @accountId;";

    private const string StreamAccountsSql = "SELECT id, name FROM accounts ORDER BY id;";

    private const string ListAccountsSql = "SELECT *\nFROM accounts\nORDER BY id;";

    private const string ListClosedBeforeSql =
        "SELECT id, name, closed_at\nFROM accounts\n"
        + "WHERE @closedBefore IS NULL OR closed_at < @closedBefore\nORDER BY id;";

    // The count of the few-hundred-rows test: past the buffer's first size of 16, where a list's growth arrays matter.
    private const int FewHundredRows = 300;

    // The bytes for each call that the pooled method must save on a few hundred rows against a List copied to an array,
    // by the commonest difference of a pair of calls.  The list of 300 rows grows through arrays of 4 to 512 elements,
    // 8,352 bytes with their headers, which the pooled method does not allocate, and the generated method allocates 16
    // bytes more than the hand-written one besides, as in the pairs above.  The commonest difference was -8,368 bytes,
    // in 499 or 500 of 500 pairs, on each of five runs.  So 8,000 is 368 bytes below what was measured, room for the
    // driver's paths and not for a growth array, and far above 4,248, the saving of a pooled method that allocated even
    // the largest growth array, of 512 elements, itself.  It is raised when the pool saves more and never lowered to
    // make a change pass.
    private const long LeastSavingInBytesPerCall = 8_000;

    // The bytes for each call that the pooled NumbersAsList must save on a few hundred rows against a List that grows
    // by doubling and is returned as it is, by the commonest difference of a pair of calls.  The hand-written list ends
    // with an array of 512 elements and has allocated the growth arrays of 4 to 256 before it, 8,352 bytes in all with
    // their headers; the pooled method allocates the one array of 300 elements that its list holds, so the saving is
    // the growth arrays less that array.  The commonest difference was -5,904 bytes, in 496 to 500 of 500 pairs, on
    // each of five runs.  So 5,500 is 404 bytes below what was measured, room for the driver's paths and not for a
    // buffer of 64 elements.  A ToList that did not return its last buffer to the pool, the 512 elements of 4,120
    // bytes, saved 1,784 bytes only, which is far below 5,500 and fails the test.  It is raised when the pool saves
    // more and never lowered to make a change pass.
    private const long LeastListSavingInBytesPerCall = 5_500;

    // The bytes for each call that the pooled NumbersAsImmutableArray must save on a few hundred rows against an
    // ImmutableArray builder that is drained, by the commonest difference of a pair of calls.  The builder grows
    // through arrays of 16 to 512 elements, 8,208 bytes with their headers, then copies the rows into an array of 300
    // elements when it is drained; the pooled method allocates that array alone, which the immutable array wraps.  The
    // commonest difference was -8,304 bytes, in 494 to 500 of 500 pairs, on four of five runs, and -8,368, in 489, on
    // the fifth.  So 8,000 is 304 bytes below the least measured, room for the driver's paths and not for a growth
    // array.  A ToImmutableArray that copied the 300 rows a second time, an array of 2,424 bytes, saved 5,880 bytes
    // only, which is far below 8,000 and fails the test, as does one that did not return its last buffer.  It is
    // raised when the pool saves more and never lowered to make a change pass.
    private const long LeastImmutableArraySavingInBytesPerCall = 8_000;

    private const string NumbersSql = "SELECT n FROM generate_series(1, @count) AS n ORDER BY n;";

    // The count of the numbers queries of the pairs below: three rows, as the accounts queries read.
    private const int CountedRows = 3;

    // The rows of a numbers query at CountedRows.
    private static readonly CountedNumber[] CountedNumbers = [new(1), new(2), new(3)];

    // The rows of ListClosedBefore for a null date: every account, a NULL among them.
    private static readonly ListClosedBeforeDto[] ClosedBeforeRows =
    [
        new(1, "alice", null),
        new(2, "bob", new DateTimeOffset(2024, 9, 1, 17, 45, 0, TimeSpan.Zero)),
        new(3, "carol", null),
    ];

    // Each side must allocate something, or the measure is not of the call at all.
    private const long LeastBytesPerCall = 0;

    // The margin, the counts and the Least...Saving constants were measured on an open connection alone, so a closed
    // one would add rows that nothing has measured.
    private const ConnectionState State = ConnectionState.Open;

    private static readonly MeasureSettings Settings = new(WarmUpCalls, Pairs, LeastBytesPerCall);

    [Fact]
    public async Task GetAccountName_ThroughThePool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new GetAccountNameArgs(1);
        (await connection.GetAccountName(parameters, cancellationToken: token)).ShouldBe([new AccountName(1, "alice")]);
        (await GetAccountNamePooledByHandAsync(connection, 1, cancellationToken: token)).ShouldBe([
            new AccountName(1, "alice"),
        ]);

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.GetAccountName(parameters, cancellationToken: token),
            () => GetAccountNamePooledByHandAsync(connection, 1, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task GetAccountName_UnderNoArrayPool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new GetAccountNameArgs(1);
        (await UnpooledAccountQueries.GetAccountName(connection, parameters, cancellationToken: token)).ShouldBe([
            new AccountName(1, "alice"),
        ]);
        (await GetAccountNameUnpooledByHandAsync(connection, 1, cancellationToken: token)).ShouldBe([
            new AccountName(1, "alice"),
        ]);

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => UnpooledAccountQueries.GetAccountName(connection, parameters, cancellationToken: token),
            () => GetAccountNameUnpooledByHandAsync(connection, 1, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task ListAccounts_ThroughThePool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var accounts = await connection.ListAccounts(cancellationToken: token);
        accounts.Count.ShouldBe(3);
        (await ListAccountsPooledByHandAsync(connection, cancellationToken: token)).ShouldBe(accounts);

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.ListAccounts(cancellationToken: token),
            () => ListAccountsPooledByHandAsync(connection, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task ListAccounts_UnderNoArrayPool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var accounts = await UnpooledAccountQueries.ListAccounts(connection, cancellationToken: token);
        accounts.Count.ShouldBe(3);
        (await ListAccountsUnpooledByHandAsync(connection, cancellationToken: token)).ShouldBe(accounts);

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => UnpooledAccountQueries.ListAccounts(connection, cancellationToken: token),
            () => ListAccountsUnpooledByHandAsync(connection, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    // A query with a parameter and a few rows.
    [Fact]
    public async Task ListClosedBefore_ThroughThePool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new ListClosedBeforeArgs(null);
        (await connection.ListClosedBefore(parameters, cancellationToken: token)).ShouldBe(ClosedBeforeRows);
        (await ListClosedBeforePooledByHandAsync(connection, null, cancellationToken: token)).ShouldBe(
            ClosedBeforeRows
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.ListClosedBefore(parameters, cancellationToken: token),
            () => ListClosedBeforePooledByHandAsync(connection, null, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task ListClosedBefore_UnderNoArrayPool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new ListClosedBeforeArgs(null);
        (await UnpooledAccountQueries.ListClosedBefore(connection, parameters, cancellationToken: token)).ShouldBe(
            ClosedBeforeRows
        );
        (await ListClosedBeforeUnpooledByHandAsync(connection, null, cancellationToken: token)).ShouldBe(
            ClosedBeforeRows
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => UnpooledAccountQueries.ListClosedBefore(connection, parameters, cancellationToken: token),
            () => ListClosedBeforeUnpooledByHandAsync(connection, null, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersAsList_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersAsListArgs(CountedRows);
        (await connection.NumbersAsList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListPooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsList(parameters, cancellationToken: token),
            () => NumbersAsListPooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersAsIList_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersAsIListArgs(CountedRows);
        (await connection.NumbersAsIList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListPooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsIList(parameters, cancellationToken: token),
            () => NumbersAsListPooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersAsICollection_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersAsICollectionArgs(CountedRows);
        (await connection.NumbersAsICollection(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListPooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsICollection(parameters, cancellationToken: token),
            () => NumbersAsListPooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersAsImmutableArray_ThreeRowsThroughThePool_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersAsImmutableArrayArgs(CountedRows);
        (await connection.NumbersAsImmutableArray(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsImmutableArrayPooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersAsImmutableArray(parameters, cancellationToken: token),
            () => NumbersAsImmutableArrayPooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersNoPoolAsList_ThreeRows_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersNoPoolAsListArgs(CountedRows);
        (await connection.NumbersNoPoolAsList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListUnpooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsList(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersNoPoolAsIList_ThreeRows_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersNoPoolAsIListArgs(CountedRows);
        (await connection.NumbersNoPoolAsIList(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListUnpooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsIList(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersNoPoolAsICollection_ThreeRows_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersNoPoolAsICollectionArgs(CountedRows);
        (await connection.NumbersNoPoolAsICollection(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsListUnpooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsICollection(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    [Fact]
    public async Task NumbersNoPoolAsImmutableArray_ThreeRows_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersNoPoolAsImmutableArrayArgs(CountedRows);
        (await connection.NumbersNoPoolAsImmutableArray(parameters, cancellationToken: token)).ShouldBe(CountedNumbers);
        (await NumbersAsImmutableArrayUnpooledByHandAsync(connection, CountedRows, cancellationToken: token)).ShouldBe(
            CountedNumbers
        );

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            () => connection.NumbersNoPoolAsImmutableArray(parameters, cancellationToken: token),
            () => NumbersAsImmutableArrayUnpooledByHandAsync(connection, CountedRows, cancellationToken: token),
            MarginInBytesPerCall,
            Settings
        );
    }

    // Many rows, where the pool matters: a list that grows by doubling to hold 300 rows allocates growth arrays of 4 to
    // 512 elements, about 8.4 KB in all, and the pooled method rents its buffers from the pool, so it allocates the
    // 300 rows and the final array alone.  So the generated method allocates less than the hand-written one, by about
    // the list's growth arrays.
    [Fact]
    public async Task NumbersAsArray_AFewHundredRows_AllocatesLessThanAListCopiedToAnArray()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersAsArrayArgs(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        (await connection.NumbersAsArray(parameters, cancellationToken: token)).ShouldBe(expected);
        (await NumbersAsArrayUnpooledByHandAsync(connection, FewHundredRows, cancellationToken: token)).ShouldBe(
            expected
        );

        await Allocations.ShouldSaveAtLeastAsync(
            () => connection.NumbersAsArray(parameters, cancellationToken: token),
            () => NumbersAsArrayUnpooledByHandAsync(connection, FewHundredRows, cancellationToken: token),
            LeastSavingInBytesPerCall,
            Settings
        );
    }

    // Many rows, for the list: the pooled NumbersAsList rents its buffers and copies the rows into a List of the exact
    // size, so it allocates that list and its array of 300 elements.  A List that grows by doubling to hold the rows,
    // returned as it is, allocates growth arrays of 4 to 512 elements and ends with the array of 512.
    [Fact]
    public async Task NumbersAsList_AFewHundredRows_AllocatesLessThanAGrownList()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersAsListArgs(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        (await connection.NumbersAsList(parameters, cancellationToken: token)).ShouldBe(expected);
        (await NumbersAsListUnpooledByHandAsync(connection, FewHundredRows, cancellationToken: token)).ShouldBe(
            expected
        );

        await Allocations.ShouldSaveAtLeastAsync(
            () => connection.NumbersAsList(parameters, cancellationToken: token),
            () => NumbersAsListUnpooledByHandAsync(connection, FewHundredRows, cancellationToken: token),
            LeastListSavingInBytesPerCall,
            Settings
        );
    }

    // Many rows, for the immutable array: the pooled NumbersAsImmutableArray rents its buffers and wraps the exact
    // array of 300 elements that it copies the rows into, and a builder that is drained grows through arrays of 16 to
    // 512 elements, then copies the rows into an array of the exact size.
    [Fact]
    public async Task NumbersAsImmutableArray_AFewHundredRows_AllocatesLessThanABuilder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        var parameters = new NumbersAsImmutableArrayArgs(FewHundredRows);
        var expected = Enumerable.Range(1, FewHundredRows).Select(n => new CountedNumber(n)).ToArray();
        (await connection.NumbersAsImmutableArray(parameters, cancellationToken: token)).ShouldBe(expected);
        (
            await NumbersAsImmutableArrayUnpooledByHandAsync(connection, FewHundredRows, cancellationToken: token)
        ).ShouldBe(expected);

        await Allocations.ShouldSaveAtLeastAsync(
            () => connection.NumbersAsImmutableArray(parameters, cancellationToken: token),
            () => NumbersAsImmutableArrayUnpooledByHandAsync(connection, FewHundredRows, cancellationToken: token),
            LeastImmutableArraySavingInBytesPerCall,
            Settings
        );
    }

    // The streaming method returns an iterator and yields each row as the reader reads it, with no buffer, so a call
    // is the method, its iterator and the enumeration to the end, which both sides do: the hand-written iterator makes
    // the calls that the generated one does, so what the generated method allocates beyond it is its own state.
    [Fact]
    public async Task StreamAccounts_ToTheEnd_AllocatesNoMoreThanByHand()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = source.CreateConnection();
        await Allocations.InStateAsync(connection, State, token);
        (await connection.StreamAccounts(cancellationToken: token).ToListAsync(token)).Count.ShouldBe(3);
        (await StreamAccountsByHand(connection, cancellationToken: token).ToListAsync(token)).Count.ShouldBe(3);

        await Allocations.ShouldAllocateNoMoreThanByHandAsync(
            async () =>
            {
                var rows = 0;
                await foreach (var account in connection.StreamAccounts(cancellationToken: token).ConfigureAwait(false))
                {
                    rows += account.Id;
                }

                return rows;
            },
            async () =>
            {
                var rows = 0;
                await foreach (
                    var account in StreamAccountsByHand(connection, cancellationToken: token).ConfigureAwait(false)
                )
                {
                    rows += account.Id;
                }

                return rows;
            },
            MarginInBytesPerCall,
            Settings
        );
    }

    // GetAccountName of Queries/Accounts.sql, by hand: the rows into RowBufferByHand.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<AccountName>> GetAccountNamePooledByHandAsync(
        DbConnection connection,
        int accountId,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = GetAccountNameSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<int>
                    {
                        ParameterName = "accountId",
                        DataTypeName = "integer",
                        TypedValue = accountId,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new RowBufferByHand<AccountName>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new AccountName(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
                    }

                    return rows.ToArray();
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

    // GetAccountName of Queries/Accounts.sql under no-array-pool, by hand: the rows into a List, copied to an array.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<AccountName>> GetAccountNameUnpooledByHandAsync(
        DbConnection connection,
        int accountId,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = GetAccountNameSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<int>
                    {
                        ParameterName = "accountId",
                        DataTypeName = "integer",
                        TypedValue = accountId,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new List<AccountName>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new AccountName(reader.GetFieldValue<int>(0), reader.GetFieldValue<string>(1)));
                    }

                    AccountName[] array = [.. rows];
                    return array;
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

    // ListAccounts of Queries/Accounts.sql, by hand, the rows into RowBufferByHand: closed_at and name_length may be
    // NULL.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<AccountsDto>> ListAccountsPooledByHandAsync(
        DbConnection connection,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = ListAccountsSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new RowBufferByHand<AccountsDto>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(
                            new AccountsDto(
                                reader.GetFieldValue<int>(0),
                                reader.GetFieldValue<string>(1),
                                reader.GetFieldValue<string>(2),
                                reader.GetFieldValue<string>(3),
                                reader.GetFieldValue<DateTimeOffset>(4),
                                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                                reader.IsDBNull(6) ? null : reader.GetFieldValue<int>(6)
                            )
                        );
                    }

                    return rows.ToArray();
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

    // ListAccounts of Queries/Accounts.sql under no-array-pool, by hand: closed_at and name_length may be NULL.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<AccountsDto>> ListAccountsUnpooledByHandAsync(
        DbConnection connection,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = ListAccountsSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new List<AccountsDto>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(
                            new AccountsDto(
                                reader.GetFieldValue<int>(0),
                                reader.GetFieldValue<string>(1),
                                reader.GetFieldValue<string>(2),
                                reader.GetFieldValue<string>(3),
                                reader.GetFieldValue<DateTimeOffset>(4),
                                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                                reader.IsDBNull(6) ? null : reader.GetFieldValue<int>(6)
                            )
                        );
                    }

                    AccountsDto[] array = [.. rows];
                    return array;
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

    // ListClosedBefore of Queries/Accounts.sql, by hand, the rows into RowBufferByHand: a null date is the parameter's
    // own null, and closed_at may be NULL.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<ListClosedBeforeDto>> ListClosedBeforePooledByHandAsync(
        DbConnection connection,
        DateTimeOffset? closedBefore,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = ListClosedBeforeSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<DateTimeOffset?>
                    {
                        ParameterName = "closedBefore",
                        DataTypeName = "timestamp with time zone",
                        TypedValue = closedBefore,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new RowBufferByHand<ListClosedBeforeDto>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(
                            new ListClosedBeforeDto(
                                reader.GetFieldValue<int>(0),
                                reader.GetFieldValue<string>(1),
                                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
                            )
                        );
                    }

                    return rows.ToArray();
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

    // ListClosedBefore of Queries/Accounts.sql under no-array-pool, by hand: a null date is the parameter's own null,
    // and closed_at may be NULL.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<ListClosedBeforeDto>> ListClosedBeforeUnpooledByHandAsync(
        DbConnection connection,
        DateTimeOffset? closedBefore,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = ListClosedBeforeSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<DateTimeOffset?>
                    {
                        ParameterName = "closedBefore",
                        DataTypeName = "timestamp with time zone",
                        TypedValue = closedBefore,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new List<ListClosedBeforeDto>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(
                            new ListClosedBeforeDto(
                                reader.GetFieldValue<int>(0),
                                reader.GetFieldValue<string>(1),
                                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
                            )
                        );
                    }

                    ListClosedBeforeDto[] array = [.. rows];
                    return array;
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

    // NumbersAsList of Queries/RowBuffers.sql, by hand, the rows into RowBufferByHand and copied to a List, as
    // NumbersAsIList and NumbersAsICollection read them too.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<List<CountedNumber>> NumbersAsListPooledByHandAsync(
        DbConnection connection,
        int count,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = NumbersSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<int>
                    {
                        ParameterName = "count",
                        DataTypeName = "integer",
                        TypedValue = count,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new RowBufferByHand<CountedNumber>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                    }

                    return rows.ToList();
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

    // NumbersAsImmutableArray of Queries/RowBuffers.sql, by hand, the rows into RowBufferByHand and copied to an
    // ImmutableArray.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<ImmutableArray<CountedNumber>> NumbersAsImmutableArrayPooledByHandAsync(
        DbConnection connection,
        int count,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = NumbersSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<int>
                    {
                        ParameterName = "count",
                        DataTypeName = "integer",
                        TypedValue = count,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new RowBufferByHand<CountedNumber>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                    }

                    return rows.ToImmutableArray();
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

    // NumbersNoPoolAsList of Queries/RowBuffers.sql under no-array-pool, by hand, the rows into a List that is
    // returned, as NumbersNoPoolAsIList and NumbersNoPoolAsICollection read them too.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<List<CountedNumber>> NumbersAsListUnpooledByHandAsync(
        DbConnection connection,
        int count,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = NumbersSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<int>
                    {
                        ParameterName = "count",
                        DataTypeName = "integer",
                        TypedValue = count,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new List<CountedNumber>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                    }

                    return rows;
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

    // NumbersNoPoolAsImmutableArray of Queries/RowBuffers.sql under no-array-pool, by hand, the rows into a builder
    // that is drained.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<ImmutableArray<CountedNumber>> NumbersAsImmutableArrayUnpooledByHandAsync(
        DbConnection connection,
        int count,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = NumbersSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<int>
                    {
                        ParameterName = "count",
                        DataTypeName = "integer",
                        TypedValue = count,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = ImmutableArray.CreateBuilder<CountedNumber>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                    }

                    return rows.DrainToImmutable();
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

    // NumbersAsArray of Queries/RowBuffers.sql, by hand, the rows into a List, copied to an array: the baseline of
    // List and ToArray() that the pooled NumbersAsArray is held against for a few hundred rows.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<CountedNumber[]> NumbersAsArrayUnpooledByHandAsync(
        DbConnection connection,
        int count,
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
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = NumbersSql;
                command.Transaction = transaction;
                if (commandTimeout.HasValue)
                {
                    command.CommandTimeout = commandTimeout.Value;
                }

                _ = command.Parameters.Add(
                    new NpgsqlParameter<int>
                    {
                        ParameterName = "count",
                        DataTypeName = "integer",
                        TypedValue = count,
                    }
                );

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var rows = new List<CountedNumber>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new CountedNumber(reader.IsDBNull(0) ? null : reader.GetFieldValue<int>(0)));
                    }

                    CountedNumber[] array = [.. rows];
                    return array;
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

    // StreamAccounts of Queries/Streaming.sql, by hand: a method that checks its argument at the call and returns a
    // static local iterator, which opens a closed connection, yields each row inside the try and closes the connection
    // it opened in the finally.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
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
                            yield return new CollectedAccount(
                                reader.GetFieldValue<int>(0),
                                reader.GetFieldValue<string>(1)
                            );
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
