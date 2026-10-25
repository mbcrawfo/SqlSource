using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd.Shapes;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The bytes that a generated method allocates for each call, against a hand-written ADO.NET method of the same calls:
// ShapeQueries.SearchAccounts, with two parameters and three rows of the seed of "app".  The hand-written method makes
// the generated method's calls, one by one: the open/close rule of the methods, the command and its arguments, the
// binding of each parameter, the reads of each column, and the same rows in the same collection, an IReadOnlyList with
// an array behind it, which it can only build by copying a List, since the count of the rows is not known until they
// are read.  GC.GetTotalAllocatedBytes(true) counts every thread, where the driver completes the continuations of
// an asynchronous call, so the test runs in MeasuresAllocations, alone and on a server of its own.
[Collection(MeasuresAllocations.Name)]
public sealed class AllocationTests(SqlServerContainer sqlServer)
{
    // The SQL of SearchAccounts, as the generated method sends it.
    private const string SearchAccountsSql =
        "SELECT id, name\nFROM dbo.accounts\nWHERE name LIKE @name OR id = @accountId\nORDER BY id;";

    // The first calls pay for one-off work: JIT compilation, the driver's static state, the pool's connection and the
    // server's plan of the query.  Fifty of each were enough on both images when the counts were set: the blocks after
    // them fell no further.
    private const int WarmUpCalls = 50;

    // Twenty pairs of blocks of fifty calls, one block of each method, alternated so that the two see the same state
    // of the process.  A call allocated about 6.7 KB on an open connection and 7.2 KB on a closed one, on SQL Server
    // 2025 and 2017 alike when the counts were set, and the median of the differences of the pairs was within 6 bytes
    // of zero; a run of the test takes some seconds.
    private const int Pairs = 20;

    private const int CallsPerBlock = 50;

    // The bytes a call of the generated method may allocate over the hand-written one's, by the median of the
    // differences of the pairs of blocks: each block of the generated method less the block of the hand-written one
    // measured beside it.  A pair shares the state of the process, so a drift of the run, such as a pool or a buffer
    // that grows, or a server that answers at another pace, which changes how many of the driver's awaits complete
    // asynchronously, cancels within the pair, where the medians of each method's blocks taken apart would carry it
    // into one of them.  The median leaves out a pair that another thread's allocation landed in, such as the pool's
    // timer, which a total would spread over every call.  The margin is below the 24 bytes of the smallest object of a
    // 64-bit process, so that any object the generated method allocates for each call and the hand-written does not
    // fails the test, and above the noise of the median, 6 bytes when it was set.  Lower it if the noise allows; never
    // raise it to make a change pass.
    private const double MarginInBytesPerCall = 16;

    // A call allocates its command, its parameters, its reader and its rows: some kilobytes on the drivers that the
    // project runs.  A median below this measured nothing.
    private const double LeastBytesPerCall = 1024;

    // A closed connection is opened and closed again by each call; an open one is left open.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchAccounts_ManyCalls_AllocatesNoMoreThanByHand(bool open)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        if (open)
        {
            await connection.OpenAsync(token);
        }

        // "%o%" matches bob and carol, and the id alice.
        var filter = new SearchAccountsFilter { Name = "%o%", AccountId = 1 };
        var generatedRows = await CallAsync(generated: true, connection, filter, token);
        generatedRows.ShouldBe([
            new SearchAccountsView(1, "alice"),
            new SearchAccountsView(2, "bob"),
            new SearchAccountsView(3, "carol"),
        ]);
        (await CallAsync(generated: false, connection, filter, token)).ShouldBe(generatedRows);

        _ = await AllocatedPerCallAsync(generated: true, connection, filter, WarmUpCalls, token);
        _ = await AllocatedPerCallAsync(generated: false, connection, filter, WarmUpCalls, token);

        var generatedBlocks = new List<double>();
        var byHandBlocks = new List<double>();
        var differences = new List<double>();
        for (var pair = 0; pair < Pairs; pair++)
        {
            // Each method goes first in every other pair.
            var generatedFirst = pair % 2 == 0;
            var perCall = new Dictionary<bool, double>();
            foreach (var generated in new[] { generatedFirst, !generatedFirst })
            {
                perCall[generated] = await AllocatedPerCallAsync(generated, connection, filter, CallsPerBlock, token);
            }

            generatedBlocks.Add(perCall[true]);
            byHandBlocks.Add(perCall[false]);
            differences.Add(perCall[true] - perCall[false]);
        }

        var generatedPerCall = Median(generatedBlocks);
        var byHandPerCall = Median(byHandBlocks);
        var differencePerCall = Median(differences);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Bytes per call: generated {generatedPerCall:F1}, by hand {byHandPerCall:F1}, "
                + $"difference of the pairs {differencePerCall:F1}."
        );

        byHandPerCall.ShouldBeGreaterThan(LeastBytesPerCall);
        generatedPerCall.ShouldBeGreaterThan(LeastBytesPerCall);
        differencePerCall.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
    }

    // The bytes that every thread allocated for each of the calls of one method.
    private static async Task<double> AllocatedPerCallAsync(
        bool generated,
        DbConnection connection,
        SearchAccountsFilter filter,
        int calls,
        CancellationToken cancellationToken
    )
    {
        var before = GC.GetTotalAllocatedBytes(precise: true);
        for (var call = 0; call < calls; call++)
        {
            _ = await CallAsync(generated, connection, filter, cancellationToken);
        }

        return (GC.GetTotalAllocatedBytes(precise: true) - before) / (double)calls;
    }

    // One call of either method, through the same path, so that the path costs the two the same.
    private static Task<IReadOnlyList<SearchAccountsView>> CallAsync(
        bool generated,
        DbConnection connection,
        SearchAccountsFilter filter,
        CancellationToken cancellationToken
    ) =>
        generated
            ? ShapeQueries.SearchAccounts(connection, filter, cancellationToken: cancellationToken)
            : SearchAccountsByHandAsync(connection, filter, cancellationToken: cancellationToken);

    private static double Median(List<double> blocks)
    {
        var sorted = blocks.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }

    // SearchAccounts by hand: the arguments of the generated method, and its calls.
    private static async Task<IReadOnlyList<SearchAccountsView>> SearchAccountsByHandAsync(
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
