using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// A generated method allocates no more for each call than a hand-written ADO.NET method, written as the manual's
// recipe for your own code writes one and as a careful developer would: it honours the same contract, opening a closed
// connection and closing it again in a finally, setting the transaction and, when given, the timeout, and passing the
// token; it binds the parameter as the generated method does, an NpgsqlParameter<int> named without its '@' and with
// its DataTypeName, since binding by the described type is decided and its cost is the driver's on both sides; and it
// reads each column by its ordinal with GetFieldValue into the same model, and the rows into a List that it copies to
// an array, the collection the method returns.  The generated method reads its rows through its pooled row buffer
// instead, which allocates less than the List, so it stays within the margin.  Both run on the same open connection
// of one data source, with the same token, and ConfigureAwait(false) on each await.  GC.GetTotalAllocatedBytes counts
// every thread, since an awaited continuation runs where the driver completes it, so the class is in
// MeasuresAllocations, which runs alone on a container of its own.  That keeps other tests out of the count, but not
// the process's own background work, which the measure leaves out by comparing the methods in pairs of blocks.
[Collection(MeasuresAllocations.Name)]
public sealed class AllocationTests(PostgresContainer postgres)
{
    // The bytes for each call that the generated method may allocate beyond the hand-written one.  A call of these
    // queries allocated about 1.7 to 2.7 KB here when this was set, and the two methods, which then both read the rows
    // into a List, differed by the size of their async state machines alone, within about 25 bytes a call either way
    // over a run; a boxed value is 24 bytes.  The binding's own cost, the driver's for each parameter and its
    // DataTypeName, is in both sides alike.  So an extra boxed value, array or closure on each call shows, a few of
    // them exceed the margin, and noise does not.  Never above 128.
    private const double MarginInBytesPerCall = 64;

    // The first calls pay for one-off work: JIT compilation, static initialisers, the pools of the driver and of the
    // runtime, and the statement's first parse.
    private const int WarmUpCalls = 200;

    // The measured calls are made in pairs of blocks, one block of each method, one after the other, and which method
    // goes first alternates from pair to pair.  What the generated method allocates beyond the hand-written one is the
    // median of the pairs' differences.  A block that nothing else disturbs allocates the same to the byte each time,
    // but what another thread allocates in the meantime is counted in the block it falls in: the test platform sends
    // its telemetry over HTTPS while the tests run, which added up to 66 KB to one block and failed a sum of the
    // blocks.  A burst of such work moves the few pairs it falls in and not their median, and work that goes on
    // throughout falls on both blocks of a pair alike.  A cost that the generated method pays on each call is in
    // every pair.
    private const int CallsPerBlock = 100;

    // Enough pairs, of about 25 ms each, that a burst of a few hundred milliseconds moves fewer than half of them.
    private const int Pairs = 20;

    private const string GetAccountNameSql = "SELECT id, name\nFROM accounts\nWHERE id = @accountId;";

    private const string ListAccountsSql = "SELECT *\nFROM accounts\nORDER BY id;";

    private const string ListClosedBeforeSql =
        "SELECT id, name, closed_at\nFROM accounts\n"
        + "WHERE @closedBefore IS NULL OR closed_at < @closedBefore\nORDER BY id;";

    private static readonly DateTimeOffset BobClosedAt = new(2024, 9, 1, 17, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAccountName_PerCall_AllocatesNoMoreThanHandWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var parameters = new GetAccountNameArgs(1);
        (await connection.GetAccountName(parameters, cancellationToken: token)).ShouldBe([new AccountName(1, "alice")]);
        (await GetAccountNameByHand(connection, 1, cancellationToken: token)).ShouldBe([new AccountName(1, "alice")]);

        var perCall = await MeasureAsync(
            () => connection.GetAccountName(parameters, cancellationToken: token),
            () => GetAccountNameByHand(connection, 1, cancellationToken: token)
        );

        perCall.Generated.ShouldBeGreaterThan(0);
        perCall.HandWritten.ShouldBeGreaterThan(0);
        perCall.GeneratedBeyondHandWritten.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
    }

    [Fact]
    public async Task ListAccounts_PerCall_AllocatesNoMoreThanHandWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var accounts = await connection.ListAccounts(cancellationToken: token);
        accounts.Count.ShouldBe(3);
        (await ListAccountsByHand(connection, cancellationToken: token)).ShouldBe(accounts);

        var perCall = await MeasureAsync(
            () => connection.ListAccounts(cancellationToken: token),
            () => ListAccountsByHand(connection, cancellationToken: token)
        );

        perCall.Generated.ShouldBeGreaterThan(0);
        perCall.HandWritten.ShouldBeGreaterThan(0);
        perCall.GeneratedBeyondHandWritten.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
    }

    // A query with a parameter and a few rows: a null date reads every account, a NULL among them.
    [Fact]
    public async Task ListClosedBefore_PerCall_AllocatesNoMoreThanHandWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var parameters = new ListClosedBeforeArgs(null);
        ListClosedBeforeDto[] expected = [new(1, "alice", null), new(2, "bob", BobClosedAt), new(3, "carol", null)];
        (await connection.ListClosedBefore(parameters, cancellationToken: token)).ShouldBe(expected);
        (await ListClosedBeforeByHand(connection, null, cancellationToken: token)).ShouldBe(expected);

        var perCall = await MeasureAsync(
            () => connection.ListClosedBefore(parameters, cancellationToken: token),
            () => ListClosedBeforeByHand(connection, null, cancellationToken: token)
        );

        perCall.Generated.ShouldBeGreaterThan(0);
        perCall.HandWritten.ShouldBeGreaterThan(0);
        perCall.GeneratedBeyondHandWritten.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
    }

    // The bytes for each call, after both methods are warmed up: of each method, the median of its blocks, and of the
    // generated method beyond the hand-written one, the median of the pairs.  Each delegate is made once, before the
    // measure, and calling it allocates nothing of its own.
    private static async Task<PerCallBytes> MeasureAsync<T>(
        Func<Task<IReadOnlyList<T>>> generated,
        Func<Task<IReadOnlyList<T>>> handWritten
    )
    {
        _ = await AllocatedByAsync(generated, WarmUpCalls);
        _ = await AllocatedByAsync(handWritten, WarmUpCalls);

        var generatedBlocks = new long[Pairs];
        var handWrittenBlocks = new long[Pairs];
        for (var pair = 0; pair < Pairs; pair++)
        {
            if (pair % 2 == 0)
            {
                generatedBlocks[pair] = await AllocatedByAsync(generated, CallsPerBlock);
                handWrittenBlocks[pair] = await AllocatedByAsync(handWritten, CallsPerBlock);
            }
            else
            {
                handWrittenBlocks[pair] = await AllocatedByAsync(handWritten, CallsPerBlock);
                generatedBlocks[pair] = await AllocatedByAsync(generated, CallsPerBlock);
            }
        }

        var differences = generatedBlocks.Zip(handWrittenBlocks, (g, h) => g - h).ToArray();
        var perCall = new PerCallBytes(
            MedianPerCall(generatedBlocks),
            MedianPerCall(handWrittenBlocks),
            MedianPerCall(differences)
        );
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Per call: generated {perCall.Generated:F1} bytes, hand-written {perCall.HandWritten:F1} bytes, generated "
                + $"beyond hand-written {perCall.GeneratedBeyondHandWritten:F1} bytes, the medians of {Pairs} pairs of "
                + $"blocks of {CallsPerBlock} calls.  Each pair, generated: {PerCallOf(generatedBlocks)}; "
                + $"hand-written: {PerCallOf(handWrittenBlocks)}."
        );
        return perCall;
    }

    private static double MedianPerCall(long[] blocks)
    {
        var sorted = blocks.Order().ToArray();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2.0 : sorted[middle];
        return median / CallsPerBlock;
    }

    private static string PerCallOf(long[] blocks) =>
        string.Join(
            ", ",
            blocks.Select(bytes => (bytes / (double)CallsPerBlock).ToString("F1", CultureInfo.InvariantCulture))
        );

    private static async Task<long> AllocatedByAsync<T>(Func<Task<IReadOnlyList<T>>> method, int calls)
    {
        var before = GC.GetTotalAllocatedBytes(precise: true);
        for (var call = 0; call < calls; call++)
        {
            _ = await method();
        }

        return GC.GetTotalAllocatedBytes(precise: true) - before;
    }

    // GetAccountName of Queries/Accounts.sql, by hand.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<AccountName>> GetAccountNameByHand(
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

    // ListAccounts of Queries/Accounts.sql, by hand: closed_at and name_length may be NULL.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<AccountsDto>> ListAccountsByHand(
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

    // ListClosedBefore of Queries/Accounts.sql, by hand: a null date is the parameter's own null, and closed_at may be
    // NULL.
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "The row is in memory once ReadAsync returns, as the generated method reads it."
    )]
    private static async Task<IReadOnlyList<ListClosedBeforeDto>> ListClosedBeforeByHand(
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

    private sealed record PerCallBytes(double Generated, double HandWritten, double GeneratedBeyondHandWritten);
}
