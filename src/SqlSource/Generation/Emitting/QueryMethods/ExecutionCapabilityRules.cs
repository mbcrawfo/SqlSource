using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Decides the <see cref="ExecutionCapabilities" /> of a query's method, from its engine, its shape, whether it
/// streams, whether it takes a transaction and whether it is a collection method.  It runs in the output step, as the
/// method is written, and its result is never cached.
/// </summary>
internal static class ExecutionCapabilityRules
{
    // The flags that check or refuse the transaction a method takes.
    private const ExecutionCapabilities TransactionFlags =
        ExecutionCapabilities.RefusesCompletedTransaction
        | ExecutionCapabilities.ChecksRollbackAtRead
        | ExecutionCapabilities.ChecksRollbackAfterDatabaseError;

    /// <summary>
    /// The method's capabilities, from one switch over its engine.  A new engine adds its row here, and a new flag only
    /// for a new behaviour, named for the behaviour; see <c>docs/architecture/generator.md</c>, "Adding a database
    /// engine".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A PostgreSQL query is one statement, so its reader has no later result, and Npgsql's disposal of the reader
    /// throws an error the rest of the query raised, so it needs none of them.
    /// </para>
    /// <para>
    /// SQL Server, <see cref="ExecutionCapabilities.RefusesCompletedTransaction" />, on every method: a transaction
    /// that has completed, whose <c>Connection</c> is null, given to a command runs it outside any transaction, which
    /// commits on its own, and the rollback check would blame the query for a rollback it did not cause.  A streaming
    /// method refuses it when its loop starts, so a transaction that completes between the call and the loop is
    /// refused too.
    /// </para>
    /// <para>
    /// SQL Server, <see cref="ExecutionCapabilities.DrainsLaterResults" />, on a method that reads rows: SqlClient
    /// raises an error that a later statement of the batch holds only when <c>NextResultAsync</c> reaches it, and
    /// drops it when the reader is disposed.  The method's own throws do not drain, so that a later statement's error
    /// never replaces one.  A method that reads no rows runs <c>ExecuteNonQueryAsync</c>, which reads every result.
    /// </para>
    /// <para>
    /// SQL Server, <see cref="ExecutionCapabilities.ChecksRollbackAtRead" />, on a method that reads rows: an exception
    /// of the method's own leaves the rest of the batch to the reader's disposal, which drops an error there that rolls
    /// back the transaction unannounced, so a catch reports it.
    /// </para>
    /// <para>
    /// SQL Server, <see cref="ExecutionCapabilities.ChecksRollbackAfterDatabaseError" />, on a method that reads rows:
    /// a database error at a row, or one that a drain of the later results throws, before a return, after a stream's
    /// loop or at its early exit, leaves the rest of the batch unread, and a later error there can roll the
    /// transaction back.  The transaction's <c>Connection</c> is still set when
    /// the read throws, even when that error itself rolled it back, so a catch reads the rest of the batch, disposes
    /// the reader, and reports the rollback only when a later error was raised and the <c>Connection</c> is then
    /// null; otherwise the database's error is thrown as it is.
    /// </para>
    /// <para>
    /// SQL Server, <see cref="ExecutionCapabilities.DrainsWhenLeftAtARow" />, on a method that streams: a break, a
    /// throw in the loop's body or a disposal disposes the iterator at its yield, and the rest of the batch can hold
    /// an error that the caller is to see, which the reader's disposal would drop.
    /// </para>
    /// <para>
    /// A collection method, whose <see cref="QueryMethod.Batch" /> is set, on SQL Server, has
    /// <see cref="ExecutionCapabilities.RefusesCompletedTransaction" /> alone: each result set of its batch is an
    /// item's, so it has no later result to drain, and it never streams.  What it checks of a transaction is written by
    /// its body, <see cref="BatchWriter" />, not by these flags.
    /// </para>
    /// <para>
    /// A repository's method, whose <see cref="QueryMethod.Source" /> gives its connection, takes no transaction, so it
    /// has none of the flags about one, <see cref="TransactionFlags" />; it keeps the drains.
    /// </para>
    /// </remarks>
    public static ExecutionCapabilities Of(QueryMethod method)
    {
        var capabilities = OfEngine(method);
        return method.Source is null ? capabilities : capabilities & ~TransactionFlags;
    }

    private static ExecutionCapabilities OfEngine(QueryMethod method)
    {
        var readsRows = method.Shape is ResultShape.Many or ResultShape.One or ResultShape.OneOptional;
        return method.Engine switch
        {
            DatabaseEngine.PostgreSql => ExecutionCapabilities.None,
            DatabaseEngine.SqlServer when method.Batch is not null => ExecutionCapabilities.RefusesCompletedTransaction,
            DatabaseEngine.SqlServer => ExecutionCapabilities.RefusesCompletedTransaction
                | (
                    readsRows
                        ? ExecutionCapabilities.DrainsLaterResults
                            | ExecutionCapabilities.ChecksRollbackAtRead
                            | ExecutionCapabilities.ChecksRollbackAfterDatabaseError
                        : ExecutionCapabilities.None
                )
                | (method.Streams ? ExecutionCapabilities.DrainsWhenLeftAtARow : ExecutionCapabilities.None),
            _ => throw UnexpectedEnumValueException.Of(method.Engine),
        };
    }

    /// <summary>
    /// Whether <paramref name="capabilities" /> holds <paramref name="flag" />, without the boxing of
    /// <c>Enum.HasFlag</c> on <c>netstandard2.0</c>.
    /// </summary>
    public static bool Has(this ExecutionCapabilities capabilities, ExecutionCapabilities flag) =>
        (capabilities & flag) == flag;
}
