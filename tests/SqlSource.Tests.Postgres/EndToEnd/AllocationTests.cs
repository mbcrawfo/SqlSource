using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// A generated method allocates no more for each call than a hand-written ADO.NET method of the same calls, written as
// the manual's recipe for your own code writes one and as a careful developer would: it honours the same contract,
// opening a closed connection and closing it again in a finally, setting the transaction and, when given, the timeout,
// and passing the token; it binds the parameter as the generated method does, an NpgsqlParameter<int> named without
// its '@' and with its DataTypeName, since binding by the described type is decided and its cost is the driver's on
// both sides; and it reads each column by its ordinal with GetFieldValue into the same model, and the rows into an
// array, the collection the method returns.  Both run on the same open connection of one data source, with the same
// token, and ConfigureAwait(false) on each await.  GC.GetTotalAllocatedBytes counts every thread, since an awaited
// continuation runs where the driver completes it, so the class is in MeasuresAllocations, which runs alone on a
// container of its own.
[Collection(MeasuresAllocations.Name)]
public sealed class AllocationTests(PostgresContainer postgres)
{
    // The bytes for each call that the generated method may allocate beyond the hand-written one.  Two methods of the
    // same calls differ by the size of their async state machines alone, a field or two, and the jitter of Npgsql's
    // pooled buffers over 2,000 calls was below 25 bytes a call when this was set (the spike's section 8: an execution
    // of one parameter costs 296 bytes, a boxed int 24).  An extra boxed value, array or closure on each call costs 24
    // bytes or more, so a few of them exceed the margin and noise does not.  Never above 128.
    private const double MarginInBytesPerCall = 64;

    // The first calls pay for one-off work: JIT compilation, static initialisers, the pools of the driver and of the
    // runtime, and the statement's first parse.
    private const int WarmUpCalls = 200;

    private const int MeasuredCalls = 2_000;

    // The measured calls of each method are made in blocks taken in turn, and which method goes first alternates from
    // block to block, so that what the process does in the background over the run falls on both alike: the first of
    // a pair was measured up to 40 bytes a call higher than the same method second.
    private const int Blocks = 10;

    private const string GetAccountNameSql = "SELECT id, name\nFROM accounts\nWHERE id = @accountId;";

    private const string ListAccountsSql = "SELECT *\nFROM accounts\nORDER BY id;";

    [Fact]
    public async Task GetAccountName_PerCall_AllocatesNoMoreThanHandWritten()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        var parameters = new GetAccountNameArgs(1);
        (await connection.GetAccountName(parameters, cancellationToken: token)).ShouldBe([new AccountName(1, "alice")]);
        (await GetAccountNameByHand(connection, 1, cancellationToken: token)).ShouldBe([new AccountName(1, "alice")]);

        var (generated, handWritten) = await MeasureAsync(
            () => connection.GetAccountName(parameters, cancellationToken: token),
            () => GetAccountNameByHand(connection, 1, cancellationToken: token)
        );

        generated.ShouldBeGreaterThan(0);
        handWritten.ShouldBeGreaterThan(0);
        generated.ShouldBeLessThanOrEqualTo(handWritten + MarginInBytesPerCall);
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

        var (generated, handWritten) = await MeasureAsync(
            () => connection.ListAccounts(cancellationToken: token),
            () => ListAccountsByHand(connection, cancellationToken: token)
        );

        generated.ShouldBeGreaterThan(0);
        handWritten.ShouldBeGreaterThan(0);
        generated.ShouldBeLessThanOrEqualTo(handWritten + MarginInBytesPerCall);
    }

    // The bytes for each call of each method, after both are warmed up.  Each delegate is made once, before the
    // measure, and calling it allocates nothing of its own.
    private static async Task<(double Generated, double HandWritten)> MeasureAsync<T>(
        Func<Task<IReadOnlyList<T>>> generated,
        Func<Task<IReadOnlyList<T>>> handWritten
    )
    {
        _ = await AllocatedByAsync(generated, WarmUpCalls);
        _ = await AllocatedByAsync(handWritten, WarmUpCalls);

        long generatedBytes = 0;
        long handWrittenBytes = 0;
        for (var block = 0; block < Blocks; block++)
        {
            if (block % 2 == 0)
            {
                generatedBytes += await AllocatedByAsync(generated, MeasuredCalls / Blocks);
                handWrittenBytes += await AllocatedByAsync(handWritten, MeasuredCalls / Blocks);
            }
            else
            {
                handWrittenBytes += await AllocatedByAsync(handWritten, MeasuredCalls / Blocks);
                generatedBytes += await AllocatedByAsync(generated, MeasuredCalls / Blocks);
            }
        }

        var perCall = (
            Generated: generatedBytes / (double)MeasuredCalls,
            HandWritten: handWrittenBytes / (double)MeasuredCalls
        );
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Per call: generated {perCall.Generated:F1} bytes, hand-written {perCall.HandWritten:F1} bytes."
        );
        return perCall;
    }

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
}
