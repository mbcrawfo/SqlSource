using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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
    // What a fresh database's name is masked as in a message.
    private const string FreshDatabase = "<fresh database>";

    // For each of Async then Sync: a fresh database when Setup.Writes; a connection from SourceOf, opened when
    // Setup.Connection is Open (also with a caller's transaction); Prepare run; a transaction begun when Begun, and
    // rolled back too when Completed; the call, given the transaction unless Setup.PassTransaction is false;
    // ConnectionState and TransactionState read; each Setup.Tables SELECT run on the call's connection in its
    // transaction when both still work, else on a new connection; the transaction rolled back and everything disposed,
    // then the fresh database dropped.  ItemIndex is read from the exception's "ItemIndex" property when it has one,
    // and the fresh database's name is masked in the message.  Returns Verdicts.Of(...).
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
                var outcome = await RunOnAsync(engine, pair, method, scenario, source).ConfigureAwait(false);
                return database is null ? outcome : WithoutTheName(outcome, database);
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

    // The outcome with the fresh database's name in its failure's message masked, since each run has a copy of its own
    // and a server may name it, as SQL Server's error of a constraint does.
    private static Outcome WithoutTheName(Outcome outcome, string database) =>
        outcome.Failure is { } failure
            ? outcome with
            {
                Failure = failure with
                {
                    Message = failure.Message.Replace(database, FreshDatabase, StringComparison.Ordinal),
                },
            }
            : outcome;

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
            var open = setup.Connection == ConnectionStart.Open || setup.Transaction != CallerTransaction.None;
            if (open || setup.Prepare is not null)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            if (setup.Prepare is not null)
            {
                await using var prepare = CommandOf(connection, null, setup.Prepare);
                _ = await prepare.ExecuteNonQueryAsync().ConfigureAwait(false);
                if (!open)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }

            var transaction =
                setup.Transaction == CallerTransaction.None
                    ? null
                    : await connection.BeginTransactionAsync().ConfigureAwait(false);
            if (setup.Transaction == CallerTransaction.Completed)
            {
                await transaction!.RollbackAsync().ConfigureAwait(false);
            }

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
            var given = scenario.Setup.PassTransaction ? transaction : null;
            var value = await pair.InvokeAsync(method, instance, connection, given, scenario).ConfigureAwait(false);
            result = IsVoid(method) ? "<void>" : Rendering.Of(value, engine.Render);
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
            tables.Add(await TableAsync(engine, source, connection, transaction, select).ConfigureAwait(false));
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
        IEngine engine,
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
                return await ReadTableAsync(engine, connection, transaction, select).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // The transaction is unusable, as after a failed statement: read what is committed.
            }
        }

        var other = await source.OpenConnectionAsync().ConfigureAwait(false);
        await using (other.ConfigureAwait(false))
        {
            return await ReadTableAsync(engine, other, null, select).ConfigureAwait(false);
        }
    }

    // A command of the connection, since a command of a data source refuses a connection, as Npgsql's does.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a scenario's, written in a test."
    )]
    private static DbCommand CommandOf(DbConnection connection, DbTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    // The columns' names, then each row, with the values as Rendering writes them through the engine's Render.
    private static async Task<string> ReadTableAsync(
        IEngine engine,
        DbConnection connection,
        DbTransaction? transaction,
        string select
    )
    {
        await using var command = CommandOf(connection, transaction, select);
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
                                .Select(i =>
                                    Rendering.Of(reader.IsDBNull(i) ? null : reader.GetValue(i), engine.Render)
                                )
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
