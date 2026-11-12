using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

// Runs the two methods of a pair on the same scenario, each on its own connection and, when the scenario writes, its
// own copy of the database, and compares what they did.
internal static class PairRunner
{
    // For each of Async then Sync: a fresh database when Setup.Writes; a connection from SourceOf, opened when
    // Setup.Connection is Open (also when Transaction is Begun); Prepare run; a transaction begun when Begun; the call;
    // ConnectionState and TransactionState read; each Setup.Tables SELECT run on the call's connection in its
    // transaction when both still work, else on a new connection; the transaction rolled back and everything
    // disposed, then the fresh database dropped.  ItemIndex is read from the exception's "ItemIndex" property when
    // it has one.  Returns Verdicts.Of(...).
    public static async Task<PairVerdict> RunAsync(
        IEngine engine,
        MethodPair pair,
        Scenario scenario,
        IReadOnlyList<KnownDifference> known
    )
    {
        var asyncOutcome = await OutcomeOfAsync(engine, pair, pair.Async, scenario).ConfigureAwait(false);
        var syncOutcome = await OutcomeOfAsync(engine, pair, pair.Sync, scenario).ConfigureAwait(false);
        return Verdicts.Of(scenario, asyncOutcome, syncOutcome, known);
    }

    private static async Task<Outcome> OutcomeOfAsync(
        IEngine engine,
        MethodPair pair,
        MethodInfo method,
        Scenario scenario
    )
    {
        var setup = scenario.Setup;
        var database = setup.Writes ? await engine.FreshDatabaseAsync(setup.Source).ConfigureAwait(false) : null;
        try
        {
            var source = engine.SourceOf(setup.Source, database);
            try
            {
                return await RunOnAsync(engine, pair, method, scenario, source).ConfigureAwait(false);
            }
            finally
            {
                if (database is not null)
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (database is not null)
            {
                await engine.DropDatabaseAsync(database).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Outcome> RunOnAsync(
        IEngine engine,
        MethodPair pair,
        MethodInfo method,
        Scenario scenario,
        DbDataSource source
    )
    {
        var setup = scenario.Setup;
        var connection = source.CreateConnection();
        await using (connection.ConfigureAwait(false))
        {
            var open = setup.Connection == ConnectionStart.Open || setup.Transaction == CallerTransaction.Begun;
            if (open || setup.Prepare is not null)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            if (setup.Prepare is not null)
            {
                await using var prepare = source.CreateCommand(setup.Prepare);
                prepare.Connection = connection;
                _ = await prepare.ExecuteNonQueryAsync().ConfigureAwait(false);
                if (!open)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }

            var transaction =
                setup.Transaction == CallerTransaction.Begun
                    ? await connection.BeginTransactionAsync().ConfigureAwait(false)
                    : null;
            try
            {
                return await CallAsync(engine, pair, method, scenario, source, connection, transaction)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (transaction is not null)
                {
                    await RollBackAsync(transaction).ConfigureAwait(false);
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task<Outcome> CallAsync(
        IEngine engine,
        MethodPair pair,
        MethodInfo method,
        Scenario scenario,
        DbDataSource source,
        DbConnection connection,
        DbTransaction? transaction
    )
    {
        string? result = null;
        Failure? failure = null;
        try
        {
            var instance = pair.IsInstance
                ? (
                    scenario.Instance
                    ?? throw new InvalidOperationException($"{scenario.Name}: a repository method needs an Instance.")
                )(connection, source)
                : null;
            var value = await pair.InvokeAsync(method, instance, connection, transaction, scenario)
                .ConfigureAwait(false);
            result = IsVoid(method) ? "<void>" : Rendering.Of(value);
        }
#pragma warning disable CA1031 // Any exception the method throws is the outcome that is compared.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            failure = FailureOf(engine, exception);
        }

        var connectionState = connection.State.ToString();
        var transactionState = "none";
        if (transaction is not null)
        {
            try
            {
                transactionState = await engine.TransactionStateAsync(connection, transaction).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is DbException or InvalidOperationException)
            {
                transactionState = "unreadable: " + exception.GetType().Name;
            }
        }

        var tables = new List<string>();
        foreach (var select in scenario.Setup.Tables ?? [])
        {
            tables.Add(await TableAsync(source, connection, transaction, select).ConfigureAwait(false));
        }

        return new Outcome(result, failure, connectionState, transactionState, tables);
    }

    private static bool IsVoid(MethodInfo method) =>
        method.ReturnType == typeof(void) || method.ReturnType == typeof(Task);

    private static Failure FailureOf(IEngine engine, Exception exception)
    {
        var database = Chain(exception).OfType<DbException>().FirstOrDefault();
        var item = exception.GetType().GetProperty("ItemIndex")?.GetValue(exception);
        return new Failure(
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.InnerException?.GetType().FullName,
            database is null ? null : engine.CodeOf(database),
            item is int index ? index : null,
            Rendering.Masked(exception.Message)
        );
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    // The call's connection and transaction when they still work, else a new connection of the source.
    private static async Task<string> TableAsync(
        DbDataSource source,
        DbConnection connection,
        DbTransaction? transaction,
        string select
    )
    {
        if (connection.State == ConnectionState.Open && (transaction is null || transaction.Connection is not null))
        {
            try
            {
                return await ReadTableAsync(source, connection, transaction, select).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // The transaction is unusable, as after a failed statement: read what is committed.
            }
        }

        var other = await source.OpenConnectionAsync().ConfigureAwait(false);
        await using (other.ConfigureAwait(false))
        {
            return await ReadTableAsync(source, other, null, select).ConfigureAwait(false);
        }
    }

    // The columns' names, then each row, with the values as Rendering writes them.
    private static async Task<string> ReadTableAsync(
        DbDataSource source,
        DbConnection connection,
        DbTransaction? transaction,
        string select
    )
    {
        await using var command = source.CreateCommand(select);
        command.Connection = connection;
        command.Transaction = transaction;
        var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            var text = new StringBuilder();
            _ = text.Append(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                _ = text.Append('\n')
                    .Append(
                        string.Join(
                            " | ",
                            Enumerable
                                .Range(0, reader.FieldCount)
                                .Select(i => Rendering.Of(reader.IsDBNull(i) ? null : reader.GetValue(i)))
                        )
                    );
            }

            return text.ToString();
        }
    }

    private static async Task RollBackAsync(DbTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            // Already ended, or the connection is gone: nothing is left to roll back.
        }
    }
}
