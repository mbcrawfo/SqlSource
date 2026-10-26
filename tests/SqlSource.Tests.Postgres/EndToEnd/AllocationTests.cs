using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// A generated method allocates no more for each call than a hand-written ADO.NET method of the same calls, written as
// the manual's recipe for your own code writes one and as a careful developer would: it honours the same contract,
// opening a closed connection and closing it again in a finally, setting the transaction and, when given, the timeout,
// and passing the token; it binds the parameter as the generated method does, an NpgsqlParameter<int> named without
// its '@' and with its DataTypeName, since binding by the described type is decided and its cost is the driver's on
// both sides; and it reads each column by its ordinal with GetFieldValue into the same model, and the rows into an
// array, the collection the method returns.  Both run on the same open connection of one data source, with the same
// token, and ConfigureAwait(false) on each await.  The two are compared by the commonest difference of pairs of calls,
// one of each method, which CallAllocations explains.  GC.GetTotalAllocatedBytes counts every thread, since an awaited
// continuation runs where the driver completes it, so the class is in MeasuresAllocations, which runs alone on a
// container of its own.  That keeps other tests out of the count, but not the process's own background work, which
// the commonest difference leaves out.
[Collection(MeasuresAllocations.Name)]
public sealed class AllocationTests(PostgresContainer postgres)
{
    // The bytes for each call that the generated method may allocate beyond the hand-written one, by the commonest
    // difference of a pair of calls.  A call of these queries allocated about 1.6 to 2.7 KB here when this was set, and
    // the two methods of the same calls differed by less than an object, the difference of their own state: the
    // generated GetAccountName allocated 8 bytes more than the hand-written one, ListClosedBefore 16 bytes less and
    // ListAccounts the same, on every run.  A boxed value is 24 bytes.  The binding's own cost, the driver's for each
    // parameter and its DataTypeName, is in both sides alike.  So an extra boxed value, array or closure on each call
    // shows, a few of them exceed the margin, and the commonest difference does not move with the driver's share of a
    // call.  Never above 128.
    private const long MarginInBytesPerCall = 64;

    // The first calls pay for one-off work: JIT compilation, static initialisers, the pools of the driver and of the
    // runtime, and the statement's first parse.
    private const int WarmUpCalls = 200;

    // The measured pairs of calls.  When the count was set, more than four pairs in five differed by the commonest
    // difference.
    private const int Pairs = 500;

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

        var pairs = await MeasureAsync(
            () => connection.GetAccountName(parameters, cancellationToken: token),
            () => GetAccountNameByHand(connection, 1, cancellationToken: token)
        );

        pairs.Generated.Bytes.ShouldBeGreaterThan(0);
        pairs.ByHand.Bytes.ShouldBeGreaterThan(0);
        pairs.GeneratedBeyondByHand.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
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

        var pairs = await MeasureAsync(
            () => connection.ListAccounts(cancellationToken: token),
            () => ListAccountsByHand(connection, cancellationToken: token)
        );

        pairs.Generated.Bytes.ShouldBeGreaterThan(0);
        pairs.ByHand.Bytes.ShouldBeGreaterThan(0);
        pairs.GeneratedBeyondByHand.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
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

        var pairs = await MeasureAsync(
            () => connection.ListClosedBefore(parameters, cancellationToken: token),
            () => ListClosedBeforeByHand(connection, null, cancellationToken: token)
        );

        pairs.Generated.Bytes.ShouldBeGreaterThan(0);
        pairs.ByHand.Bytes.ShouldBeGreaterThan(0);
        pairs.GeneratedBeyondByHand.ShouldBeLessThanOrEqualTo(MarginInBytesPerCall);
    }

    // The pairs of calls of the two methods, after both are warmed up, written to the test's output.
    private static async Task<CallPairs> MeasureAsync<T>(
        Func<Task<IReadOnlyList<T>>> generated,
        Func<Task<IReadOnlyList<T>>> handWritten
    )
    {
        var pairs = await CallAllocations.MeasureAsync(generated, handWritten, WarmUpCalls, Pairs);
        TestContext.Current.TestOutputHelper?.WriteLine($"Of {Pairs} pairs of calls, {pairs}.");
        return pairs;
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
}
