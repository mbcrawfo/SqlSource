using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods.CollectionMethods;

/// <summary>
/// Writes the body of a collection method, whose <see cref="QueryMethod.Batch" /> is set, after the checks that
/// <see cref="HeaderWriter" /> writes: it walks the items once, returns at once when there are none, opens the
/// connection as <see cref="CommandWriter" /> does, begins a transaction of its own unless the caller has one, and
/// runs the items in chunks of <see cref="ChunkSize" />, each a <c>DbBatch</c> of a command for each item, built and
/// bound as a query method's command is.  <see cref="BatchRowsWriter" /> writes the running of each chunk and the
/// reading of its result sets.  Its own transaction is committed after the last chunk, rolled back when anything
/// throws, and disposed.
/// </summary>
/// <remarks>
/// <para>
/// A transaction the caller gave, or an ambient one, <c>System.Transactions.Transaction.Current</c>, is the caller's:
/// the method begins, commits and rolls back none, and runs each chunk in the caller's.  Whether there is an ambient
/// one is read before anything is awaited, as the method starts.  Under <see cref="TransactionMode.Off" /> it begins
/// none either.
/// </para>
/// <para>
/// A rollback is skipped when the transaction's <c>Connection</c> is null, which is how SqlClient leaves one that the
/// server rolled back, and a rollback that fails is ignored, so that the first exception is the one the method throws.
/// </para>
/// <para>
/// Each item is checked as it is added to its chunk: a null item throws <c>ArgumentNullException</c> with its
/// position, and its lists are counted and checked as a query method's are, by <see cref="ListCountWriter" />.  The SQL
/// is written once, before the loop, unless lists make each item's SQL its own.
/// </para>
/// </remarks>
internal static class BatchWriter
{
    /// <summary>The number of items a chunk holds, so that a batch of many items does not hold them all.</summary>
    public const int ChunkSize = 1000;

    /// <summary>
    /// Appends the body, at the indent of the method's body.
    /// </summary>
    public static void AppendBody(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var batch = locals.Batch!;
        var ownsTransaction = context.Method.Batch!.Transaction != TransactionMode.Off;
        var tryIndent = GeneratedText.Deeper(indent);
        var loopIndent = ownsTransaction ? GeneratedText.Deeper(tryIndent) : tryIndent;
        if (context.Capabilities.Has(ExecutionCapabilities.RefusesCompletedTransaction))
        {
            ExecutionCapabilityWriter.AppendCompletedTransactionCheck(in context, indent);
        }

        _ = builder
            .Append(indent)
            .Append("using var ")
            .Append(batch.Items)
            .Append(" = ")
            .Append(MethodArguments.Parameters)
            .Append(".GetEnumerator();\n");
        QueryMethodWriter.AppendIf(builder, indent, "!" + batch.Items + ".MoveNext()");
        _ = builder.Append(GeneratedText.Deeper(indent)).Append(BatchRowsWriter.EmptyReturnOf(in context)).Append('\n');
        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append('\n');
        if (locals.Lists is null)
        {
            CommandWriter.AppendSql(in context, indent);
        }

        if (ownsTransaction)
        {
            AppendOwnsTransaction(in context, indent);
        }

        CommandWriter.AppendOpen(in context, indent);
        if (ownsTransaction)
        {
            AppendBegin(in context, tryIndent);
        }

        BatchRowsWriter.AppendBuffer(in context, loopIndent);
        AppendChunks(in context, loopIndent, ownsTransaction);
        if (ownsTransaction)
        {
            _ = builder.Append('\n');
            AppendCommit(in context, loopIndent);
        }

        BatchRowsWriter.AppendReturn(in context, loopIndent);
        if (ownsTransaction)
        {
            AppendRollback(in context, tryIndent);
        }

        CommandWriter.AppendClose(in context, indent);
    }

    // Whether the method begins its own transaction: it has none of the caller's, given or ambient.  A repository's
    // method takes no transaction, so only an ambient one is the caller's.
    private static void AppendOwnsTransaction(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        _ = builder.Append(indent).Append("var ").Append(context.Locals.Batch!.OwnsTransaction).Append(" = ");
        if (context.Arguments.TakesConnection)
        {
            _ = builder.Append(context.Locals.Transaction).Append(" is null && ");
        }

        _ = builder.Append("global::System.Transactions.Transaction.Current is null;\n");
    }

    // The transaction the method begins when it owns one, at the isolation level of its mode, and the start of the
    // try whose catch rolls it back and whose finally disposes it.
    private static void AppendBegin(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var batch = context.Locals.Batch!;
        var continued = GeneratedText.Deeper(indent);
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(batch.OwnTransaction)
            .Append(" = ")
            .Append(batch.OwnsTransaction)
            .Append('\n')
            .Append(continued)
            .Append("? ");
        _ = AsyncSpelling.AppendAwaitedCall(builder, context.Locals.Connection, "BeginTransactionAsync");
        if (IsolationLevelOf(context.Method.Batch!.Transaction) is { } level)
        {
            _ = builder.Append("global::System.Data.IsolationLevel.").Append(level).Append(", ");
        }

        _ = builder.Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append('\n').Append(continued).Append(": null;\n");
        _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
    }

    // The member of System.Data.IsolationLevel that a mode begins its transaction at, or null for the database's
    // default.
    private static string? IsolationLevelOf(TransactionMode mode) =>
        mode switch
        {
            // Off begins no transaction and never reaches this switch; it is named so that the switch lists every
            // member.
            TransactionMode.On or TransactionMode.Off => null,
            TransactionMode.ReadUncommitted => "ReadUncommitted",
            TransactionMode.ReadCommitted => "ReadCommitted",
            TransactionMode.RepeatableRead => "RepeatableRead",
            TransactionMode.Serializable => "Serializable",
            TransactionMode.Snapshot => "Snapshot",
            _ => throw UnexpectedEnumValueException.Of(mode),
        };

    // The loop over the chunks: each a batch of the connection, in the transaction and with the timeout, filled with a
    // command for each item up to the chunk's size, run, and disposed before the next.  The place of the chunk's first
    // item is kept for the exceptions that name an item.
    private static void AppendChunks(in QueryMethodContext context, string indent, bool ownsTransaction)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var batch = locals.Batch!;
        var chunkIndent = GeneratedText.Deeper(indent);
        var itemIndent = GeneratedText.Deeper(chunkIndent);
        var chunkSize = ChunkSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(batch.ItemIndex)
            .Append(" = 0;\n")
            .Append(indent)
            .Append("bool ")
            .Append(batch.More)
            .Append(";\n")
            .Append(indent)
            .Append("do\n")
            .Append(indent)
            .Append("{\n")
            .Append(chunkIndent)
            .Append("var ")
            .Append(batch.First)
            .Append(" = ")
            .Append(batch.ItemIndex)
            .Append(";\n")
            .Append(chunkIndent)
            .Append("var ")
            .Append(batch.Batch)
            .Append(" = ")
            .Append(locals.Connection)
            .Append(".CreateBatch();\n");
        _ = AsyncSpelling.AppendUsing(builder, chunkIndent, batch.BatchScope, batch.Batch);
        AppendBatchTransaction(in context, chunkIndent, ownsTransaction);
        if (context.Arguments.TakesConnection)
        {
            QueryMethodWriter.AppendIf(builder, chunkIndent, MethodArguments.CommandTimeout + ".HasValue");
            _ = builder
                .Append(itemIndent)
                .Append(batch.Batch)
                .Append(".Timeout = ")
                .Append(MethodArguments.CommandTimeout)
                .Append(".Value;\n");
            QueryMethodWriter.AppendEnd(builder, chunkIndent);
        }

        _ = builder.Append('\n').Append(chunkIndent).Append("do\n").Append(chunkIndent).Append("{\n");
        AppendItem(in context, itemIndent);
        _ = builder
            .Append(chunkIndent)
            .Append("}\n")
            .Append(chunkIndent)
            .Append("while (")
            .Append(batch.ItemIndex)
            .Append(" - ")
            .Append(batch.First)
            .Append(" < ")
            .Append(chunkSize)
            .Append(" && ")
            .Append(batch.Items)
            .Append(".MoveNext());\n\n");
        BatchRowsWriter.AppendExecution(in context, chunkIndent);
        // A chunk that is not full ended because the items did, so only a full one asks for another item.
        _ = builder
            .Append('\n')
            .Append(chunkIndent)
            .Append(batch.More)
            .Append(" = ")
            .Append(batch.ItemIndex)
            .Append(" - ")
            .Append(batch.First)
            .Append(" == ")
            .Append(chunkSize)
            .Append(" && ")
            .Append(batch.Items)
            .Append(".MoveNext();\n")
            .Append(indent)
            .Append("}\n")
            .Append(indent)
            .Append("while (")
            .Append(batch.More)
            .Append(");\n");
    }

    // The batch's transaction: the one the method began, else the caller's.  A repository's method under off has
    // neither, and leaves the batch's own.
    private static void AppendBatchTransaction(in QueryMethodContext context, string indent, bool ownsTransaction)
    {
        var batch = context.Locals.Batch!;
        var takesTransaction = context.Arguments.TakesConnection;
        if (!ownsTransaction && !takesTransaction)
        {
            return;
        }

        var builder = context.Builder;
        _ = builder.Append(indent).Append(batch.Batch).Append(".Transaction = ");
        _ = (ownsTransaction, takesTransaction) switch
        {
            (true, true) => builder.Append(batch.OwnTransaction).Append(" ?? ").Append(context.Locals.Transaction),
            (true, false) => builder.Append(batch.OwnTransaction),
            _ => builder.Append(context.Locals.Transaction),
        };
        _ = builder.Append(";\n");
    }

    // One item, the enumerator's current: checked, its lists counted, its SQL written when its lists make it its own,
    // and its command made, bound from it and added to the batch.
    private static void AppendItem(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var batch = locals.Batch!;
        var throwIndent = GeneratedText.Deeper(indent);
        var argumentIndent = GeneratedText.Deeper(throwIndent);
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.Input)
            .Append(" = ")
            .Append(batch.Items)
            .Append(".Current;\n");
        QueryMethodWriter.AppendIf(builder, indent, locals.Input + " is null");
        _ = builder
            .Append(throwIndent)
            .Append("throw new global::System.ArgumentNullException(\n")
            .Append(argumentIndent)
            .Append(SymbolDisplay.FormatLiteral(MethodArguments.Parameters, quote: true))
            .Append(",\n")
            .Append(argumentIndent)
            .Append("\"Item \" + ")
            .Append(batch.ItemIndex)
            .Append(".ToString(")
            .Append(ListWriter.Invariant)
            .Append(") + \" of the collection is null.\"\n")
            .Append(throwIndent)
            .Append(");\n");
        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append('\n');
        if (locals.Lists is not null)
        {
            ListCountWriter.AppendCounts(in context, indent);
            CommandWriter.AppendSql(in context, indent);
        }

        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(locals.Command)
            .Append(" = ")
            .Append(batch.Batch)
            .Append(".CreateBatchCommand();\n");
        CommandWriter.AppendCommandText(in context, indent);
        _ = builder.Append('\n');
        CommandWriter.AppendBindings(in context, indent);
        _ = builder
            .Append(indent)
            .Append(batch.Batch)
            .Append(".BatchCommands.Add(")
            .Append(locals.Command)
            .Append(");\n")
            .Append(indent)
            .Append(batch.ItemIndex)
            .Append("++;\n");
    }

    // The commit of the method's own transaction, after the last chunk.
    private static void AppendCommit(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var ownTransaction = context.Locals.Batch!.OwnTransaction;
        QueryMethodWriter.AppendIf(builder, indent, ownTransaction + " is not null");
        _ = builder.Append(GeneratedText.Deeper(indent));
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, ownTransaction, "CommitAsync")
            .Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The end of AppendBegin's try: its catch, which rolls the method's own transaction back when the server has not,
    // ignores a rollback that fails and throws the first exception again, and its finally, which disposes it.
    private static void AppendRollback(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var ownTransaction = context.Locals.Batch!.OwnTransaction;
        var catchIndent = GeneratedText.Deeper(indent);
        var ifIndent = GeneratedText.Deeper(catchIndent);
        var tryIndent = GeneratedText.Deeper(ifIndent);
        _ = builder.Append(indent).Append("}\n").Append(indent).Append("catch\n").Append(indent).Append("{\n");
        QueryMethodWriter.AppendIf(builder, catchIndent, ownTransaction + "?.Connection is not null");
        _ = builder.Append(ifIndent).Append("try\n").Append(ifIndent).Append("{\n").Append(tryIndent);
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, ownTransaction, "RollbackAsync")
            .Append("global::System.Threading.CancellationToken.None");
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        QueryMethodWriter.AppendEnd(builder, ifIndent);
        _ = builder
            .Append(ifIndent)
            .Append("catch\n")
            .Append(ifIndent)
            .Append("{\n")
            .Append(tryIndent)
            .Append("// The first exception is the one the method throws.\n");
        QueryMethodWriter.AppendEnd(builder, ifIndent);
        QueryMethodWriter.AppendEnd(builder, catchIndent);
        _ = builder.Append('\n').Append(catchIndent).Append("throw;\n");
        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append(indent).Append("finally\n").Append(indent).Append("{\n");
        QueryMethodWriter.AppendIf(builder, catchIndent, ownTransaction + " is not null");
        _ = builder.Append(ifIndent);
        _ = AsyncSpelling.AppendAwaitedCall(builder, ownTransaction, "DisposeAsync");
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        QueryMethodWriter.AppendEnd(builder, catchIndent);
        QueryMethodWriter.AppendEnd(builder, indent);
    }
}
