using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the statements that a query's method's <see cref="ExecutionCapabilities" /> turn on: the refusal of a
/// completed transaction, the catch that checks for a rollback, and the loops that drain the later results, with
/// the documentation of what they throw.  <see cref="QueryMethodWriter" /> keeps the structure of the buffered and
/// streamed reading, asks a flag at each point where one applies, and calls in here.  Why each flag exists is said
/// once, at <see cref="ExecutionCapabilityRules.Of" />.
/// </summary>
internal static class ExecutionCapabilityWriter
{
    // The token of a call that nothing cancels.
    private const string NoCancellation = "global::System.Threading.CancellationToken.None";

    private const string RolledBackType = "global::SqlSource.TransactionRolledBackException";

    // The exception as its documentation names it: a documentation ID, which the compiler does not bind, as
    // QueryMethodWriter names UnexpectedRowCountException.
    private const string RolledBackCref = "T:SqlSource.TransactionRolledBackException";

    private const string CompletedTransactionType = "global::System.InvalidOperationException";

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
                CompletedTransactionType,
                "The transaction has completed: it was committed or rolled back before the query ran."
            );
        }

        if (capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead))
        {
            XmlDocWriter.AppendException(
                builder,
                indent,
                RolledBackCref,
                "The query threw an exception of its own, its inner exception, and an error that the rest of the "
                    + "query raised rolled back the transaction."
            );
        }
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
            .Append(CompletedTransactionType)
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
    /// Appends the catch of a read, for an exception of the method's own: every one but the database's error, which
    /// reports itself, and the caller's cancellation.  With a transaction given, it disposes the reader, which reads
    /// the rest of the batch and drops any error there, and when such an error rolled the transaction back, which
    /// leaves its <c>Connection</c> null, it throws the exception that says so, with the method's as its inner
    /// exception; otherwise it throws the method's own as it is.  A transaction that had completed before the query
    /// ran was refused, so a null <c>Connection</c> here comes from this query's batch.  A call that does not throw
    /// runs none of it, though the <c>await</c> in the catch makes the method's state machine keep room for the
    /// exception.  <see cref="ExecutionCapabilities.ChecksRollbackAtRead" /> turns it on.
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
            .Append(blockIndent)
            .Append("await ")
            .Append(locals.Reader)
            .Append(".DisposeAsync().ConfigureAwait(false);\n")
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
    /// Appends the drain of a buffered read before it returns, followed by a blank line, as a return follows it.
    /// <see cref="ExecutionCapabilities.DrainsLaterResults" /> turns it on.
    /// </summary>
    public static void AppendDrainBeforeReturn(in QueryMethodContext context, string indent)
    {
        AppendDrainLoop(in context, indent, MethodArguments.CancellationToken);
        _ = context.Builder.Append('\n');
    }

    /// <summary>
    /// Appends the drain after a streamed read loop, after a blank line, with the caller's token, as each read takes.
    /// <see cref="ExecutionCapabilities.DrainsLaterResults" /> turns it on.
    /// </summary>
    public static void AppendDrainAfterLoop(in QueryMethodContext context, string indent)
    {
        _ = context.Builder.Append('\n');
        AppendDrainLoop(in context, indent, MethodArguments.CancellationToken);
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
    /// which drains when the flag is set.  The drain takes no token, as the reader's disposal would read the rest: a
    /// token the caller cancelled before an early exit neither throws nor cancels the command, and the drain throws
    /// only what the database raises.
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
        AppendDrainLoop(in context, innerIndent, NoCancellation);
        QueryMethodWriter.AppendEnd(builder, loopIndent);
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The loop that reads the results after the rows and does nothing with them, with the token given.
    private static void AppendDrainLoop(in QueryMethodContext context, string indent, string token)
    {
        var builder = context.Builder;
        _ = builder
            .Append(indent)
            .Append("while (await ")
            .Append(context.Locals.Reader)
            .Append(".NextResultAsync(")
            .Append(token)
            .Append(").ConfigureAwait(false))\n")
            .Append(indent)
            .Append("{\n");
        QueryMethodWriter.AppendEnd(builder, indent);
    }
}
