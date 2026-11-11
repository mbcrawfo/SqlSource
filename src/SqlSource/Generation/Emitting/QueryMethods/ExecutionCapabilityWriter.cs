using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the statements that a query's method's <see cref="ExecutionCapabilities" /> turn on: the refusal of a
/// completed transaction, the catches that check for a rollback, the loops that drain the later results, and a
/// collection method's search for its failed command and checks of its result sets, with the documentation of what
/// they throw.  The phases of <see cref="QueryMethodWriter" /> keep the structure of the buffered and streamed reading,
/// ask a flag at each point where one applies, and call in here.  Why each flag exists is said once, at
/// <see cref="ExecutionCapabilityRules.Of" />.
/// </summary>
internal static class ExecutionCapabilityWriter
{
    // The token of a call that nothing cancels.
    private const string NoCancellation = "global::System.Threading.CancellationToken.None";

    private const string RolledBackType = "global::SqlSource.TransactionRolledBackException";

    // The exception as its documentation names it: a documentation ID, which the compiler does not bind, as
    // HeaderWriter names UnexpectedRowCountException.
    private const string RolledBackCref = "T:SqlSource.TransactionRolledBackException";

    private const string InvalidOperationType = "global::System.InvalidOperationException";

    /// <summary>
    /// Appends the <c>&lt;exception&gt;</c> of each exception that the capabilities throw.
    /// </summary>
    public static void AppendDocumentation(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var capabilities = context.Capabilities;
        if (capabilities.Has(ExecutionCapabilities.RefusesCompletedTransaction))
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                InvalidOperationType,
                "The transaction has completed: it was committed or rolled back before the query ran."
            );
        }

        if (capabilities.Has(ExecutionCapabilities.ChecksRollbackAfterDatabaseError))
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                RolledBackCref,
                "The query threw an exception of its own, or the database raised an error, its inner exception, and an "
                    + "error that the rest of the query raised then rolled back the transaction."
            );
        }
        else if (capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead))
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                RolledBackCref,
                "The query threw an exception of its own, its inner exception, and an error that the rest of the "
                    + "query raised rolled back the transaction."
            );
        }

        if (capabilities.Has(ExecutionCapabilities.ChecksResultSetsMatchItems))
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                InvalidOperationType,
                "A statement of the query returned a result set that is not an item's, or an item returned none: a "
                    + "query whose statements return more than one result set, or none for an item, cannot run over a "
                    + "collection."
            );
        }
    }

    /// <summary>
    /// Appends the move to a chunk's next result set, after the first, and the check that it has the output's columns:
    /// a command whose statements gave no result set, or a result set of other columns, which a statement of the query
    /// returned, throws, naming the items from the chunk's first to the one whose result set was read, since a reader
    /// skips a command without a result set and cannot tell which item's is missing or extra.
    /// <see cref="ExecutionCapabilities.ChecksResultSetsMatchItems" /> turns it on, in place of the plain move.
    /// </summary>
    public static void AppendResultSetCheck(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var batch = locals.Batch!;
        var columns = context.Method.Output!.Columns.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ = builder.Append(indent).Append("if ((").Append(batch.Result).Append(" > 0 && !");
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, locals.Reader, "NextResultAsync")
            .Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling
            .AppendAwaitedEnd(builder)
            .Append(")\n")
            .Append(GeneratedText.Deeper(indent))
            .Append("|| ")
            .Append(locals.Reader)
            .Append(".FieldCount != ")
            .Append(columns)
            .Append(")\n")
            .Append(indent)
            .Append("{\n");
        AppendResultSetThrow(in context, GeneratedText.Deeper(indent), "(" + batch.First + " + " + batch.Result + ")");
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    /// <summary>
    /// Appends, after a blank line, the check that no result set follows the chunk's last command's, which a statement
    /// of the query returned; it names the chunk's items, from its first to its last.
    /// <see cref="ExecutionCapabilities.ChecksResultSetsMatchItems" /> turns it on.
    /// </summary>
    public static void AppendNoLaterResultSetCheck(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        _ = builder.Append('\n').Append(indent).Append("if (");
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, context.Locals.Reader, "NextResultAsync")
            .Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(")\n").Append(indent).Append("{\n");
        AppendResultSetThrow(in context, GeneratedText.Deeper(indent), "(" + context.Locals.Batch!.ItemIndex + " - 1)");
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    /// <summary>
    /// Appends the catch of a chunk's database error that names none of the batch's commands, after the catch of one
    /// that names one: the failed item is that of the first command whose statement type Npgsql has not set, as it
    /// sets it when a command completes, and an error after every command completed, which no item raised, is thrown
    /// as it is.  A batch whose commands are not Npgsql's, which a connection that wraps Npgsql's makes, has no
    /// statement type to read, so its error is thrown as it is too; a chunk holds at least one command, all made by
    /// one batch, so the first tells.  <see cref="ExecutionCapabilities.FindsFailedItemByCompletion" /> turns it on.
    /// </summary>
    public static void AppendFailedCommandSearch(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var batch = context.Locals.Batch!;
        var body = GeneratedText.Deeper(indent);
        var condition = GeneratedText.Deeper(body);
        var commands = batch.Batch + ".BatchCommands";
        _ = builder
            .Append(indent)
            .Append("catch (global::Npgsql.PostgresException ")
            .Append(batch.Error)
            .Append(") when (")
            .Append(commands)
            .Append("[0] is global::Npgsql.NpgsqlBatchCommand)\n")
            .Append(indent)
            .Append("{\n")
            .Append(body)
            .Append("var ")
            .Append(batch.Failed)
            .Append(" = 0;\n")
            .Append(body)
            .Append("while (\n")
            .Append(condition)
            .Append(batch.Failed)
            .Append(" < ")
            .Append(commands)
            .Append(".Count\n")
            .Append(condition)
            .Append("&& ((global::Npgsql.NpgsqlBatchCommand)")
            .Append(commands)
            .Append('[')
            .Append(batch.Failed)
            .Append("]).StatementType\n")
            .Append(GeneratedText.Deeper(condition))
            .Append("!= global::Npgsql.StatementType.Unknown\n")
            .Append(body)
            .Append(")\n")
            .Append(body)
            .Append("{\n")
            .Append(condition)
            .Append(batch.Failed)
            .Append("++;\n");
        QueryMethodWriter.AppendEnd(builder, body);
        _ = builder.Append('\n');
        QueryMethodWriter.AppendIf(builder, body, batch.Failed + " == " + commands + ".Count");
        _ = builder.Append(condition).Append("throw;\n");
        QueryMethodWriter.AppendEnd(builder, body);
        _ = builder
            .Append('\n')
            .Append(body)
            .Append("throw new global::SqlSource.CollectionItemException(")
            .Append(SymbolDisplay.FormatLiteral(context.Method.QueryName, quote: true))
            .Append(", ")
            .Append(batch.First)
            .Append(" + ")
            .Append(batch.Failed)
            .Append(", ")
            .Append(batch.Error)
            .Append(");\n");
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    /// <summary>
    /// Appends the refusal of a transaction whose <c>Connection</c> is null, which has completed, and a blank line,
    /// as the SQL follows it.  <see cref="ExecutionCapabilities.RefusesCompletedTransaction" /> turns it on.
    /// </summary>
    public static void AppendCompletedTransactionCheck(in QueryMethodContext context, string indent)
    {
        // The condition is appended in its parts, as the transaction's name is not a constant.
        var builder = context.Builder;
        var transaction = context.Locals.Transaction;
        _ = builder
            .Append(indent)
            .Append("if (")
            .Append(transaction)
            .Append(" is not null && ")
            .Append(transaction)
            .Append(".Connection is null)\n")
            .Append(indent)
            .Append("{\n")
            .Append(indent)
            .Append(GeneratedText.Indent)
            .Append("throw new ")
            .Append(InvalidOperationType)
            .Append('(')
            .Append(
                SymbolDisplay.FormatLiteral(
                    "The transaction given to the query '"
                        + context.Method.QueryName
                        + "' has completed; it is no longer usable.",
                    quote: true
                )
            )
            .Append(");\n");
        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append('\n');
    }

    /// <summary>
    /// Appends the catch of a read, for an exception of the method's own: every one but the database's error, which is
    /// <see cref="AppendDatabaseErrorCheck" />'s around each read that can raise it, and the caller's cancellation.
    /// With a transaction given, it disposes the reader, which reads the rest of the batch and drops any error there,
    /// and when such an error rolled the transaction back, which leaves its <c>Connection</c> null, it throws the
    /// exception that says so, with the method's as its inner exception; otherwise it throws the method's own as it
    /// is.  A transaction that had completed before the query ran was refused, so a null <c>Connection</c> here comes
    /// from this query's batch.  A call that does not throw runs none of it, though the <c>await</c> in the catch makes
    /// the method's state machine keep room for the exception.
    /// <see cref="ExecutionCapabilities.ChecksRollbackAtRead" /> turns it on.
    /// </summary>
    public static void AppendRollbackCheck(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var blockIndent = GeneratedText.Deeper(indent);
        _ = builder
            .Append(indent)
            .Append("catch (global::System.Exception ")
            .Append(locals.Exception)
            .Append(") when (")
            .Append(locals.Transaction)
            .Append(" is not null && ")
            .Append(locals.Exception)
            .Append(" is not (global::System.Data.Common.DbException or global::System.OperationCanceledException))\n")
            .Append(indent)
            .Append("{\n")
            .Append(blockIndent);
        _ = AsyncSpelling.AppendAwaitedCall(builder, locals.Reader, "DisposeAsync");
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        _ = builder
            .Append(blockIndent)
            .Append("if (")
            .Append(locals.Transaction)
            .Append(".Connection is null)\n")
            .Append(blockIndent)
            .Append("{\n")
            .Append(blockIndent)
            .Append(GeneratedText.Indent)
            .Append("throw new ")
            .Append(RolledBackType)
            .Append('(')
            .Append(SymbolDisplay.FormatLiteral(context.Method.QueryName, quote: true))
            .Append(", ")
            .Append(locals.Exception)
            .Append(");\n");
        QueryMethodWriter.AppendEnd(builder, blockIndent);
        _ = builder.Append('\n').Append(blockIndent).Append("throw;\n");
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    /// <summary>
    /// Appends the catch of a read or a drain, for the database's error, which comes before
    /// <see cref="AppendRollbackCheck" />'s when a <c>try</c> has both.
    /// With a transaction given, it reads the rest of the batch without the caller's token, as the reader's disposal
    /// would, and notes whether an error was raised there; any other exception of that read, such as a cancellation,
    /// is dropped, as the disposal would drop it.  It then disposes the reader, and when a later error was raised and
    /// the transaction's <c>Connection</c> is null, it throws the exception that says so, with the database's error as
    /// its inner exception; otherwise it throws the database's error as it is.  The <c>Connection</c> is still set
    /// when the read throws, even when that error itself rolled the transaction back, and a database error that rolls
    /// it back ends the batch, so a later error is what tells that the rest of the batch caused the rollback.
    /// <see cref="ExecutionCapabilities.ChecksRollbackAfterDatabaseError" /> turns it on.
    /// </summary>
    public static void AppendDatabaseErrorCheck(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var blockIndent = GeneratedText.Deeper(indent);
        var innerIndent = GeneratedText.Deeper(blockIndent);
        _ = builder
            .Append(indent)
            .Append("catch (global::System.Data.Common.DbException ")
            .Append(locals.Exception)
            .Append(") when (")
            .Append(locals.Transaction)
            .Append(" is not null)\n")
            .Append(indent)
            .Append("{\n")
            .Append(blockIndent)
            .Append("var ")
            .Append(locals.RaisedLater)
            .Append(" = false;\n")
            .Append(blockIndent)
            .Append("try\n")
            .Append(blockIndent)
            .Append("{\n");
        AppendDrainLoop(in context, innerIndent, NoCancellation);
        QueryMethodWriter.AppendEnd(builder, blockIndent);
        _ = builder
            .Append(blockIndent)
            .Append("catch (global::System.Data.Common.DbException)\n")
            .Append(blockIndent)
            .Append("{\n")
            .Append(innerIndent)
            .Append(locals.RaisedLater)
            .Append(" = true;\n");
        QueryMethodWriter.AppendEnd(builder, blockIndent);
        _ = builder.Append(blockIndent).Append("catch (global::System.Exception)\n").Append(blockIndent).Append("{\n");
        QueryMethodWriter.AppendEnd(builder, blockIndent);
        _ = builder.Append('\n').Append(blockIndent);
        _ = AsyncSpelling.AppendAwaitedCall(builder, locals.Reader, "DisposeAsync");
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        _ = builder
            .Append(blockIndent)
            .Append("if (")
            .Append(locals.RaisedLater)
            .Append(" && ")
            .Append(locals.Transaction)
            .Append(".Connection is null)\n")
            .Append(blockIndent)
            .Append("{\n")
            .Append(innerIndent)
            .Append("throw new ")
            .Append(RolledBackType)
            .Append('(')
            .Append(SymbolDisplay.FormatLiteral(context.Method.QueryName, quote: true))
            .Append(", ")
            .Append(locals.Exception)
            .Append(");\n");
        QueryMethodWriter.AppendEnd(builder, blockIndent);
        _ = builder.Append('\n').Append(blockIndent).Append("throw;\n");
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    /// <summary>
    /// Appends the drain of a buffered read before it returns, followed by a blank line, as a return follows it.
    /// <see cref="ExecutionCapabilities.DrainsLaterResults" /> turns it on.
    /// </summary>
    public static void AppendDrainBeforeReturn(in QueryMethodContext context, string indent)
    {
        AppendDrainLoop(in context, indent, MethodArguments.CancellationToken);
        _ = context.Builder.Append('\n');
    }

    /// <summary>
    /// Appends the drain after a streamed read loop, after a blank line, with the caller's token, as each read takes,
    /// in a <c>try</c> whose <see cref="AppendDatabaseErrorCheck" /> follows it when the capabilities have
    /// <see cref="ExecutionCapabilities.ChecksRollbackAfterDatabaseError" />.
    /// <see cref="ExecutionCapabilities.DrainsLaterResults" /> turns it on.
    /// </summary>
    public static void AppendDrainAfterLoop(in QueryMethodContext context, string indent)
    {
        _ = context.Builder.Append('\n');
        AppendCheckedDrain(in context, indent, MethodArguments.CancellationToken);
    }

    /// <summary>
    /// Appends the flag of a row being yielded and the start of the <c>try</c> around a streamed read loop, whose
    /// <see cref="AppendLeftAtARowEnd" /> drains when the loop was left at a row.
    /// <see cref="ExecutionCapabilities.DrainsWhenLeftAtARow" /> turns it on.
    /// </summary>
    public static void AppendLeftAtARowStart(in QueryMethodContext context, string indent) =>
        _ = context
            .Builder.Append(indent)
            .Append("var ")
            .Append(context.Locals.AtRow)
            .Append(" = false;\n")
            .Append(indent)
            .Append("try\n")
            .Append(indent)
            .Append("{\n");

    /// <summary>
    /// Appends the yield of the row, built already, with the flag set around it, so that a disposal at the yield
    /// drains, and an exception of the iterator's own, from a read, a check or a converter, does not.
    /// </summary>
    public static void AppendYieldAtARow(in QueryMethodContext context, string indent) =>
        _ = context
            .Builder.Append(indent)
            .Append(context.Locals.AtRow)
            .Append(" = true;\n")
            .Append(indent)
            .Append("yield return ")
            .Append(context.Locals.Row)
            .Append(";\n")
            .Append(indent)
            .Append(context.Locals.AtRow)
            .Append(" = false;\n");

    /// <summary>
    /// Appends the end of the <c>try</c> that <see cref="AppendLeftAtARowStart" /> opened, and its <c>finally</c>,
    /// which drains when the flag is set, in a <c>try</c> whose <see cref="AppendDatabaseErrorCheck" /> follows it when
    /// the capabilities have <see cref="ExecutionCapabilities.ChecksRollbackAfterDatabaseError" />.  The drain takes no
    /// token, as the reader's disposal would read the rest: a token the caller cancelled before an early exit neither
    /// throws nor cancels the command, and the drain throws only what the database raises.
    /// </summary>
    /// <param name="context">The method being written.</param>
    /// <param name="indent">The indent of the <c>try</c>.</param>
    /// <param name="loopIndent">The indent of the loop inside it, one deeper.</param>
    /// <param name="innerIndent">The indent of the loop's body, one deeper again.</param>
    public static void AppendLeftAtARowEnd(
        in QueryMethodContext context,
        string indent,
        string loopIndent,
        string innerIndent
    )
    {
        var builder = context.Builder;
        _ = builder.Append(indent).Append("}\n").Append(indent).Append("finally\n").Append(indent).Append("{\n");
        QueryMethodWriter.AppendIf(builder, loopIndent, context.Locals.AtRow);
        AppendCheckedDrain(in context, innerIndent, NoCancellation);
        QueryMethodWriter.AppendEnd(builder, loopIndent);
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The throw of a result set that is not an item's, naming the query and the items from the chunk's first to the
    // position given, an expression in parentheses: the item whose result set was read when the mismatch was found,
    // since any item of them may have returned the result set that is missing or extra.
    private static void AppendResultSetThrow(in QueryMethodContext context, string indent, string lastItemIndex)
    {
        var argumentIndent = GeneratedText.Deeper(indent);
        var continued = GeneratedText.Deeper(argumentIndent);
        _ = context
            .Builder.Append(indent)
            .Append("throw new ")
            .Append(InvalidOperationType)
            .Append("(\n")
            .Append(argumentIndent)
            .Append(
                SymbolDisplay.FormatLiteral(
                    "The query '"
                        + context.Method.QueryName
                        + "' returned a result set of another shape, or none, for one of the items from ",
                    quote: true
                )
            )
            .Append('\n')
            .Append(continued)
            .Append("+ ")
            .Append(context.Locals.Batch!.First)
            .Append(".ToString(")
            .Append(ListWriter.Invariant)
            .Append(")\n")
            .Append(continued)
            .Append("+ \" to \"\n")
            .Append(continued)
            .Append("+ ")
            .Append(lastItemIndex)
            .Append(".ToString(")
            .Append(ListWriter.Invariant)
            .Append(")\n")
            .Append(continued)
            .Append(
                "+ \", so it cannot run over a collection: a query whose statements return more than one result set, "
                    + "or none for an item, cannot.\"\n"
            )
            .Append(indent)
            .Append(");\n");
    }

    // The drain loop with the token given, in a try followed by the catch of the database's error when the
    // capabilities check after one.
    private static void AppendCheckedDrain(in QueryMethodContext context, string indent, string token)
    {
        if (!context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAfterDatabaseError))
        {
            AppendDrainLoop(in context, indent, token);
            return;
        }

        var builder = context.Builder;
        _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
        AppendDrainLoop(in context, GeneratedText.Deeper(indent), token);
        QueryMethodWriter.AppendEnd(builder, indent);
        AppendDatabaseErrorCheck(in context, indent);
    }

    // The loop that reads the results after the rows and does nothing with them, with the token given.
    private static void AppendDrainLoop(in QueryMethodContext context, string indent, string token)
    {
        var builder = context.Builder;
        _ = builder.Append(indent).Append("while (");
        _ = AsyncSpelling.AppendAwaitedCall(builder, context.Locals.Reader, "NextResultAsync").Append(token);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(")\n").Append(indent).Append("{\n");
        QueryMethodWriter.AppendEnd(builder, indent);
    }
}
