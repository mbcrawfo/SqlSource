using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// The differences between a SQL Server method and its twin that are Microsoft.Data.SqlClient's limits, each with its
// reason, in the order they are applied.
internal static class KnownDifferences
{
    // site/generator.md, "Give the method the transaction": SqlClient refuses a command on a connection with a pending
    // transaction that the command was not given, from both methods, and names the call that refused it, so the
    // asynchronous method's message says BeginExecuteReader where the synchronous one's says ExecuteReader.
    public static KnownDifference SqlClientPendingTransactionMessage { get; } =
        new(
            nameof(SqlClientPendingTransactionMessage),
            "Microsoft.Data.SqlClient refuses a command on a connection that has a transaction the command was not "
                + "given, and throws InvalidOperationException (\"... requires the command to have a transaction when "
                + "the connection assigned to the command is in a pending local transaction ...\") before anything is "
                + "sent (site/generator.md, \"Give the method the transaction\"): the message names the call that "
                + "refused it, BeginExecuteReader or BeginExecuteNonQuery from the asynchronous method, and "
                + "ExecuteReader or ExecuteNonQuery from the synchronous one.",
            static (_, asyncRun, syncRun) =>
                asyncRun.Failure is { Type: InvalidOperation } asyncFailure
                && syncRun.Failure is { Type: InvalidOperation } syncFailure
                && asyncFailure.Message != syncFailure.Message
                && SyncWordsOf(asyncFailure.Message) == syncFailure.Message
                    ? (asyncRun, syncRun with { Failure = syncFailure with { Message = asyncFailure.Message } })
                    : (asyncRun, syncRun)
        );

    public static IReadOnlyList<KnownDifference> All { get; } = [SqlClientPendingTransactionMessage];

    private const string InvalidOperation = "System.InvalidOperationException";

    // The asynchronous message with the synchronous calls' names in place of the asynchronous ones'.
    private static string SyncWordsOf(string message) =>
        message
            .Replace("BeginExecuteReader", "ExecuteReader", StringComparison.Ordinal)
            .Replace("BeginExecuteNonQuery", "ExecuteNonQuery", StringComparison.Ordinal);
}
