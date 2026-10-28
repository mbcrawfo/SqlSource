using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// A describe runs every call from "prepare" to "deallocate" in one transaction, which a pooler in transaction mode
// keeps on one backend, and which is rolled back whatever happens.
[Collection(UsesPostgres.Name)]
public sealed class TransactionTests(PostgresContainer postgres)
{
    private const string Statement = "sqlsource_1";

    // The start of the transaction of the session's backend, read after each call from a connection of the test's
    // own: one value from "prepare" to the call before "deallocate", and none after it.
    [Fact]
    public async Task DescribeAsync_GatedQuery_RunsEveryCallFromPrepareToDeallocateInOneTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        int? backend = null;
        var starts = new List<(string Method, DateTime? Start)>();
        var exchange = new HookedExchange(
            after: async (method, _, hookToken) =>
            {
                if (backend is { } pid)
                {
                    starts.Add((method, await TransactionStartAsync(own, pid, hookToken)));
                }
            }
        );
        await using var session = await Sessions.OpenSessionAsync(postgres.ConnectionString, exchange);
        backend = await BackendOfAsync(session.Connection);

        var described = await session.DescribeAsync(
            DescribeRequests.Of("SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id"),
            token
        );

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        starts
            .ConvertAll(static call => call.Method)
            .ShouldBe(["prepare", "columns", "tables", "types", "spellings", "check", "explain", "deallocate"]);
        var start = starts[0].Start.ShouldNotBeNull("prepare left no transaction open");
        starts.GetRange(0, starts.Count - 1).ShouldAllBe(call => call.Start == start);
        starts[^1].Start.ShouldBeNull("deallocate left the transaction open");
    }

    // A call that fails aborts the transaction, which is rolled back to the savepoint before the answer: the next call
    // and deallocate run as they would on their own, and nothing is left behind.
    [Fact]
    public async Task LiveCalls_CallThatFails_LeavesTheNextCallAndDeallocateWorking()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(Statement, "SELECT id FROM users", []),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);

        var check = await live.CheckAsync(new PostgresCheckRequest("SELECT nope FROM users", []), token);
        var relations = await live.RelationsAsync(
            new PostgresRelationsRequest([new PostgresRelationName("public", "users")]),
            token
        );
        var deallocated = await live.DeallocateAsync(new PostgresDeallocateRequest(Statement), token);

        check.Error.ShouldNotBeNull().SqlState.ShouldBe("42703");
        relations.Error.ShouldBeNull(relations.Error?.Message);
        relations.Tables.ShouldNotBeNull().ShouldHaveSingleItem().Name.ShouldBe("users");
        deallocated.Error.ShouldBeNull(deallocated.Error?.Message);
        connection.InDescribe.ShouldBeFalse();
        (await InTransactionAsync(connection)).ShouldBeFalse();
        (await PreparedStatementsAsync(connection)).ShouldBe(0);
    }

    // "overloads", the call after "relations", rolls back to the savepoint when it fails too: the server refuses a name
    // that holds a NUL, and the next call and deallocate still run.
    [Fact]
    public async Task OverloadsAsync_NameTheServerRefuses_LeavesTheNextCallAndDeallocateWorking()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(Statement, "SELECT id FROM users", []),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);

        var overloads = await live.OverloadsAsync(new PostgresOverloadsRequest(["co\0unt"], []), token);
        var relations = await live.RelationsAsync(
            new PostgresRelationsRequest([new PostgresRelationName("public", "users")]),
            token
        );
        var deallocated = await live.DeallocateAsync(new PostgresDeallocateRequest(Statement), token);

        overloads.Error.ShouldNotBeNull().SqlState.ShouldBe("22021");
        relations.Error.ShouldBeNull(relations.Error?.Message);
        relations.Tables.ShouldNotBeNull().ShouldHaveSingleItem().Name.ShouldBe("users");
        deallocated.Error.ShouldBeNull(deallocated.Error?.Message);
        connection.InDescribe.ShouldBeFalse();
        (await InTransactionAsync(connection)).ShouldBeFalse();
        (await PreparedStatementsAsync(connection)).ShouldBe(0);
    }

    // The rollback to the savepoint after a call that fails keeps the lock that preparing took: another session's
    // drop of the table still waits for the describe.
    [Fact]
    public async Task LiveCalls_CallThatFails_KeepsTheLockOfThePrepare()
    {
        var token = TestContext.Current.CancellationToken;
        await postgres.ExecuteAsync("CREATE TABLE IF NOT EXISTS transaction_kept (id int)");
        try
        {
            await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
            var live = new PostgresLive(connection);
            var prepared = await live.PrepareAsync(
                new PostgresPrepareRequest(Statement, "SELECT id FROM transaction_kept", []),
                token
            );
            prepared.Error.ShouldBeNull(prepared.Error?.Message);
            var check = await live.CheckAsync(new PostgresCheckRequest("SELECT nope FROM users", []), token);
            check.Error.ShouldNotBeNull().SqlState.ShouldBe("42703");

            var refused = await DropTransactionKeptWithinAsync("200ms");

            refused.ShouldBe("55P03", "the drop did not wait for the describe");
            (await live.DeallocateAsync(new PostgresDeallocateRequest(Statement), token)).Error.ShouldBeNull();
            (await DropTransactionKeptWithinAsync("10s")).ShouldBeNull();
        }
        finally
        {
            await postgres.ExecuteAsync("DROP TABLE IF EXISTS transaction_kept");
        }
    }

    // Drops transaction_kept from a connection of the test's own, waiting for its lock no longer than the timeout:
    // the SQLSTATE of the server's refusal, or null when the table was dropped.
    private async Task<string?> DropTransactionKeptWithinAsync(string timeout)
    {
        var token = TestContext.Current.CancellationToken;
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = await own.OpenConnectionAsync(token);
        await using (
            var limit = new NpgsqlCommand("SELECT pg_catalog.set_config('lock_timeout', @timeout, false)", connection)
        )
        {
            _ = limit.Parameters.AddWithValue("timeout", timeout);
            _ = await limit.ExecuteScalarAsync(token);
        }

        try
        {
            await using var drop = new NpgsqlCommand("DROP TABLE transaction_kept", connection);
            _ = await drop.ExecuteNonQueryAsync(token);
            return null;
        }
        catch (PostgresException refused)
        {
            return refused.SqlState;
        }
    }

    // No deallocate follows a prepare that failed, so the prepare ends the transaction itself, and the next one
    // begins its own.
    [Fact]
    public async Task PrepareAsync_TextOfTwoStatements_LeavesNoTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        var refused = await live.PrepareAsync(new PostgresPrepareRequest(Statement, "SELECT 1; SELECT 2", []), token);

        refused.Error.ShouldNotBeNull().SqlState.ShouldBe("42601");
        connection.InDescribe.ShouldBeFalse();
        (await InTransactionAsync(connection)).ShouldBeFalse();
        var prepared = await live.PrepareAsync(new PostgresPrepareRequest(Statement, "SELECT 1", []), token);
        prepared.Error.ShouldBeNull(prepared.Error?.Message);
        connection.InDescribe.ShouldBeTrue();
        (await live.DeallocateAsync(new PostgresDeallocateRequest(Statement), token)).Error.ShouldBeNull();
    }

    // A cancelled describe asks nothing more of the server: the cancellation reaches the caller, the transaction dies
    // with the session, and the session closes.
    [Fact]
    public async Task DescribeAsync_CancelledAfterColumns_ThrowsAndTheSessionCloses()
    {
        var token = TestContext.Current.CancellationToken;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using var own = NpgsqlDataSource.Create(postgres.ConnectionString);
        var exchange = new HookedExchange(
            after: async (method, _, _) =>
            {
                if (method == "columns")
                {
                    await cancel.CancelAsync();
                }
            }
        );
        var session = await Sessions.OpenSessionAsync(postgres.ConnectionString, exchange);
        var backend = await BackendOfAsync(session.Connection);

        try
        {
            _ = await Should.ThrowAsync<OperationCanceledException>(
                session.DescribeAsync(DescribeRequests.Of("SELECT id FROM users"), cancel.Token)
            );
        }
        finally
        {
            await session.DisposeAsync();
        }

        await Eventually.TrueAsync(
            async () => !await BackendIsThereAsync(own, backend, token),
            "the cancelled session's backend is still there"
        );
    }

    [Fact]
    public async Task ExplainAsync_WithoutPrepare_Throws()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await live.ExplainAsync(new PostgresExplainRequest(Statement, 0, []), TestContext.Current.CancellationToken)
        );
    }

    private static async Task<int> BackendOfAsync(PostgresConnection connection)
    {
        var token = TestContext.Current.CancellationToken;
        await using var ask = await connection.CommandAsync("SELECT pg_catalog.pg_backend_pid()", token);
        return (await ask.ExecuteScalarAsync(token)).ShouldBeOfType<int>();
    }

    // The start of the backend's transaction, or null when it is in none.
    private static async Task<DateTime?> TransactionStartAsync(
        NpgsqlDataSource own,
        int backend,
        CancellationToken token
    )
    {
        await using var read = own.CreateCommand(
            "SELECT xact_start FROM pg_catalog.pg_stat_activity WHERE pid = @backend"
        );
        _ = read.Parameters.AddWithValue("backend", backend);
        return (await read.ExecuteScalarAsync(token)).ShouldNotBeNull("the backend is gone") switch
        {
            DBNull => null,
            var start => (DateTime)start,
        };
    }

    private static async Task<bool> BackendIsThereAsync(NpgsqlDataSource own, int backend, CancellationToken token)
    {
        await using var read = own.CreateCommand("SELECT true FROM pg_catalog.pg_stat_activity WHERE pid = @backend");
        _ = read.Parameters.AddWithValue("backend", backend);
        return await read.ExecuteScalarAsync(token) is not null;
    }

    // Whether the connection is in a transaction block: outside one, the server refuses a savepoint with 25P01.
    private static async Task<bool> InTransactionAsync(PostgresConnection connection)
    {
        var token = TestContext.Current.CancellationToken;
        try
        {
            await using var save = await connection.CommandAsync("SAVEPOINT sqlsource_probe", token);
            _ = await save.ExecuteNonQueryAsync(token);
        }
        catch (PostgresException refused) when (refused.SqlState == "25P01")
        {
            return false;
        }

        await using var release = await connection.CommandAsync("RELEASE SAVEPOINT sqlsource_probe", token);
        _ = await release.ExecuteNonQueryAsync(token);
        return true;
    }

    private static async Task<long> PreparedStatementsAsync(PostgresConnection connection)
    {
        var token = TestContext.Current.CancellationToken;
        await using var count = await connection.CommandAsync(
            "SELECT pg_catalog.count(*) FROM pg_catalog.pg_prepared_statements",
            token
        );
        return (long)(await count.ExecuteScalarAsync(token)).ShouldNotBeNull();
    }
}
