using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Decides the <see cref="ExecutionCapabilities" /> of a query's method, from its engine, its shape and whether it
/// streams.  It runs in the output step, as the method is written, and its result is never cached.
/// </summary>
internal static class ExecutionCapabilityRules
{
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
    /// back the transaction unannounced, so a catch reports it.  A database error at a row is thrown as it is, so a
    /// later error of the batch that rolls the transaction back goes unreported (TD-0090).
    /// </para>
    /// <para>
    /// SQL Server, <see cref="ExecutionCapabilities.DrainsWhenLeftAtARow" />, on a method that streams: a break, a
    /// throw in the loop's body or a disposal disposes the iterator at its yield, and the rest of the batch can hold
    /// an error that the caller is to see, which the reader's disposal would drop.
    /// </para>
    /// </remarks>
    public static ExecutionCapabilities Of(QueryMethod method)
    {
        var readsRows = method.Shape is ResultShape.Many or ResultShape.One or ResultShape.OneOptional;
        return method.Engine switch
        {
            DatabaseEngine.PostgreSql => ExecutionCapabilities.None,
            DatabaseEngine.SqlServer => ExecutionCapabilities.RefusesCompletedTransaction
                | (
                    readsRows
                        ? ExecutionCapabilities.DrainsLaterResults | ExecutionCapabilities.ChecksRollbackAtRead
                        : ExecutionCapabilities.None
                )
                | (method.Streams ? ExecutionCapabilities.DrainsWhenLeftAtARow : ExecutionCapabilities.None),
        };
    }

    /// <summary>
    /// Whether <paramref name="capabilities" /> holds <paramref name="flag" />, without the boxing of
    /// <c>Enum.HasFlag</c> on <c>netstandard2.0</c>.
    /// </summary>
    public static bool Has(this ExecutionCapabilities capabilities, ExecutionCapabilities flag) =>
        (capabilities & flag) == flag;
}
