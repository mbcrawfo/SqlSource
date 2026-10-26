using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using SqlSource.Tests.SqlServer.EndToEnd.Shapes;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The bytes that a generated method allocates for each call, against a hand-written ADO.NET method of the same calls:
// ShapeQueries.SearchAccounts, with two parameters and three rows of the seed of "app".  The hand-written method makes
// the generated method's calls, one by one: the open/close rule of the methods, the command and its arguments, the
// binding of each parameter, the reads of each column, and the same rows in the same collection, an IReadOnlyList with
// an array behind it, which it can only build by copying what it read the rows into, since the count of the rows is
// not known until they are read.  It reads them into what the generated method does, so there are two pairs:
// SearchAccounts, which reads its rows into the pooled buffer, against a hand-written method that reads them into
// RowBufferByHand of tests/SqlSource.Testing, the same buffer by hand; and SearchAccountsUnpooled, a copy of the query
// under no-array-pool, which reads them into a List, against a hand-written method that does the same.  The copy is a
// query of its own because SearchAccounts' own list of generator parameters, sort-input, replaces the list of any
// claim's attribute.  In each pair the two make the same calls, so any object that the generated method allocates on
// each call and the hand-written one does not fails the test.  The two are compared by the commonest difference of
// pairs of calls, one of each method, which CallAllocations explains.  GC.GetTotalAllocatedBytes(true) counts every
// thread, where the driver completes the continuations of an asynchronous call, so the test runs in
// MeasuresAllocations, alone and on a server of its own.
[Collection(MeasuresAllocations.Name)]
public sealed class AllocationTests(SqlServerContainer sqlServer)
{
    // The SQL of SearchAccounts, as the generated method sends it.
    private const string SearchAccountsSql =
        "SELECT id, name\nFROM dbo.accounts\nWHERE name LIKE @name OR id = @accountId\nORDER BY id;";

    // The first calls pay for one-off work: JIT compilation, the driver's static state, the pool's connection and the
    // server's plan of the query.  Fifty of each were enough on both images when the counts were set: the calls after
    // them fell no further.
    private const int WarmUpCalls = 50;

    // The measured pairs of calls.  A call allocated about 6.2 to 6.7 KB on an open connection and 6.6 to 7.1 KB on a
    // closed one, on SQL Server 2025 and 2017 alike when the count was set, and more than four pairs in five differed
    // by the commonest difference; a run of the test takes some seconds.
    private const int Pairs = 500;

    // The bytes a call of the generated method may allocate over the hand-written one's, by the commonest difference of
    // a pair, the same for both pairs.  It was 0 for both, on both connections and both images, when this was set, and
    // it does not move with the driver's share of a call or with another thread's work.  The margin is below the 24
    // bytes of the smallest object of a 64-bit process, so that any object the generated method allocates for each call
    // and the hand-written does not fails the test, and an extra object did, at 24 bytes, and an array of 64 bytes, at
    // 88, failed both pairs.  It lets the generated method's state machine hold up to two more fields of 8 bytes than
    // the hand-written one's.  Never raise it to make a change pass.
    private const long MarginInBytesPerCall = 16;

    // A call allocates its command, its parameters, its reader and its rows: some kilobytes on the drivers that the
    // project runs.  A call below this measured nothing.
    private const long LeastBytesPerCall = 1024;

    // "%o%" matches bob and carol, and the id alice.
    private static readonly SearchAccountsFilter Filter = new() { Name = "%o%", AccountId = 1 };

    private static readonly SearchAccountsView[] Rows = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // A closed connection is opened and closed again by each call; an open one is left open.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchAccounts_ManyCallsThroughThePool_AllocatesNoMoreThanByHand(bool open)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        if (open)
        {
            await connection.OpenAsync(token);
        }

        var generatedRows = await ShapeQueries.SearchAccounts(connection, Filter, cancellationToken: token);
        generatedRows.ShouldBe(Rows);
        (await SearchAccountsPooledByHandAsync(connection, Filter, cancellationToken: token)).ShouldBe(generatedRows);

        var pairs = await CallAllocations.MeasureAsync(
            () => ShapeQueries.SearchAccounts(connection, Filter, cancellationToken: token),
            () => SearchAccountsPooledByHandAsync(connection, Filter, cancellationToken: token),
            WarmUpCalls,
            Pairs
        );
        TestContext.Current.TestOutputHelper?.WriteLine($"SearchAccounts, of {Pairs} pairs of calls, {pairs}.");

        pairs.ByHand.Bytes.ShouldBeGreaterThan(LeastBytesPerCall);
        pairs.Generated.Bytes.ShouldBeGreaterThan(LeastBytesPerCall);
        pairs.GeneratedBeyondByHand.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchAccounts_ManyCallsUnderNoArrayPool_AllocatesNoMoreThanByHand(bool open)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        if (open)
        {
            await connection.OpenAsync(token);
        }

        var generatedRows = await ShapeQueries.SearchAccountsUnpooled(connection, Filter, cancellationToken: token);
        generatedRows.ShouldBe(Rows);
        (await SearchAccountsUnpooledByHandAsync(connection, Filter, cancellationToken: token)).ShouldBe(generatedRows);

        var pairs = await CallAllocations.MeasureAsync(
            () => ShapeQueries.SearchAccountsUnpooled(connection, Filter, cancellationToken: token),
            () => SearchAccountsUnpooledByHandAsync(connection, Filter, cancellationToken: token),
            WarmUpCalls,
            Pairs
        );
        TestContext.Current.TestOutputHelper?.WriteLine($"SearchAccountsUnpooled, of {Pairs} pairs of calls, {pairs}.");

        pairs.ByHand.Bytes.ShouldBeGreaterThan(LeastBytesPerCall);
        pairs.Generated.Bytes.ShouldBeGreaterThan(LeastBytesPerCall);
        pairs.GeneratedBeyondByHand.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
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
}
