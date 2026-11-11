using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods.CollectionMethods;

/// <summary>
/// Writes what a collection method does with what it reads: the buffer its rows go into, or the count a rowcount
/// method sums, the running of each chunk and the reading of its result sets, one for each item, in order, and the
/// return.  A database error at an item is thrown as <c>CollectionItemException</c>, with the item's position.
/// <see cref="BatchWriter" /> writes the rest of the body, and <see cref="RowReaderWriter" /> the reading of each row.
/// </summary>
internal static class BatchRowsWriter
{
    private const string ItemExceptionType = "global::SqlSource.CollectionItemException";

    /// <summary>
    /// The return of a method given no items: an empty collection, no rows affected, or nothing.
    /// </summary>
    internal static string EmptyReturnOf(in QueryMethodContext context) =>
        context.Method.Shape switch
        {
            ResultShape.Many or ResultShape.One or ResultShape.OneOptional => "return [];",
            ResultShape.RowCount => "return 0;",
            ResultShape.None => "return;",
            _ => throw UnexpectedEnumValueException.Of(context.Method.Shape),
        };

    /// <summary>
    /// Appends the buffer the rows of every item are read into, as a <see cref="ResultShape.Many" /> query method's
    /// are, or the count a rowcount method sums; nothing for a method that returns nothing.
    /// </summary>
    internal static void AppendBuffer(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        if (context.Method.Shape == ResultShape.None)
        {
            return;
        }

        _ = builder.Append(indent).Append("var ");
        _ =
            context.Method.Shape == ResultShape.RowCount
                ? builder.Append(locals.Batch!.Affected).Append(" = 0")
                : builder
                    .Append(locals.Rows)
                    .Append(" = ")
                    .Append(RowsWriter.GetBuffer(in context, context.Method.Output!.TypeText).Buffer);
        _ = builder.Append(";\n");
    }

    /// <summary>
    /// Appends, after a blank line, the return of the collection from the buffer, as a <see cref="ResultShape.Many" />
    /// query method ends it, or of the count; nothing for a method that returns nothing.
    /// </summary>
    internal static void AppendReturn(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        if (context.Method.Shape == ResultShape.None)
        {
            return;
        }

        _ = builder.Append('\n').Append(indent).Append("return ");
        _ =
            context.Method.Shape == ResultShape.RowCount
                ? builder.Append(context.Locals.Batch!.Affected)
                : builder.Append(RowsWriter.GetBuffer(in context, context.Method.Output!.TypeText).Result);
        _ = builder.Append(";\n");
    }

    /// <summary>
    /// Appends the running of the chunk's batch and the reading of each command's result set, in a try whose catch
    /// throws a database error that names a command of the batch as <c>CollectionItemException</c>, at the position of
    /// that command's item, and, under <see cref="ExecutionCapabilities.FindsFailedItemByCompletion" />, whose second
    /// catch finds the command of an error that names none.
    /// </summary>
    internal static void AppendExecution(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var batch = context.Locals.Batch!;
        var innerIndent = GeneratedText.Deeper(indent);
        var argumentIndent = GeneratedText.Deeper(innerIndent);
        _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
        if (context.Method.Shape is ResultShape.None or ResultShape.RowCount)
        {
            AppendNonQuery(in context, innerIndent);
        }
        else
        {
            AppendResultSets(in context, innerIndent);
        }

        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder
            .Append(indent)
            .Append("catch (global::System.Data.Common.DbException ")
            .Append(batch.Error)
            .Append(") when (\n")
            .Append(innerIndent)
            .Append(batch.Error)
            .Append(" is not ")
            .Append(ItemExceptionType)
            .Append('\n')
            .Append(innerIndent)
            .Append("&& ")
            .Append(batch.Error)
            .Append(".BatchCommand is { } ")
            .Append(batch.Failed)
            .Append('\n')
            .Append(innerIndent)
            .Append("&& ")
            .Append(batch.Batch)
            .Append(".BatchCommands.Contains(")
            .Append(batch.Failed)
            .Append(")\n")
            .Append(indent)
            .Append(")\n")
            .Append(indent)
            .Append("{\n")
            .Append(innerIndent)
            .Append("throw new ")
            .Append(ItemExceptionType)
            .Append("(\n")
            .Append(argumentIndent)
            .Append(SymbolDisplay.FormatLiteral(context.Method.QueryName, quote: true))
            .Append(",\n")
            .Append(argumentIndent)
            .Append(batch.First)
            .Append(" + ")
            .Append(batch.Batch)
            .Append(".BatchCommands.IndexOf(")
            .Append(batch.Failed)
            .Append("),\n")
            .Append(argumentIndent)
            .Append(batch.Error)
            .Append('\n')
            .Append(innerIndent)
            .Append(");\n");
        QueryMethodWriter.AppendEnd(builder, indent);
        if (context.Capabilities.Has(ExecutionCapabilities.FindsFailedItemByCompletion))
        {
            ExecutionCapabilityWriter.AppendFailedCommandSearch(in context, indent);
        }
    }

    // The batch run without a reader; a rowcount method adds each command's count to its total, leaving out the -1 of a
    // command that counts nothing.
    private static void AppendNonQuery(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var batch = context.Locals.Batch!;
        _ = builder.Append(indent);
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, batch.Batch, "ExecuteNonQueryAsync")
            .Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        if (context.Method.Shape != ResultShape.RowCount)
        {
            return;
        }

        AppendResultLoopStart(in context, indent);
        _ = builder
            .Append(GeneratedText.Deeper(indent))
            .Append(batch.Affected)
            .Append(" += global::System.Math.Max(")
            .Append(batch.Batch)
            .Append(".BatchCommands[")
            .Append(batch.Result)
            .Append("].RecordsAffected, 0);\n");
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The reader of the batch, disposed before the next chunk, and each command's result set read in order, the next
    // reached with NextResultAsync.
    private static void AppendResultSets(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var batch = locals.Batch!;
        var resultIndent = GeneratedText.Deeper(indent);
        _ = builder.Append(indent).Append("var ").Append(locals.Reader).Append(" = ");
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, batch.Batch, "ExecuteReaderAsync")
            .Append("global::System.Data.CommandBehavior.Default, ")
            .Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        _ = AsyncSpelling.AppendUsing(builder, indent, locals.ReaderScope, locals.Reader);
        AppendResultLoopStart(in context, indent);
        QueryMethodWriter.AppendIf(builder, resultIndent, batch.Result + " > 0");
        _ = builder.Append(GeneratedText.Deeper(resultIndent));
        _ = AsyncSpelling
            .AppendAwaitedCall(builder, locals.Reader, "NextResultAsync")
            .Append(MethodArguments.CancellationToken);
        _ = AsyncSpelling.AppendAwaitedEnd(builder).Append(";\n");
        QueryMethodWriter.AppendEnd(builder, resultIndent);
        _ = builder.Append('\n');
        AppendResultSet(in context, resultIndent, context.Method.Output!);
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The loop over the commands of the chunk, up to the brace that opens its body.
    private static void AppendResultLoopStart(in QueryMethodContext context, string indent)
    {
        var batch = context.Locals.Batch!;
        _ = context
            .Builder.Append(indent)
            .Append("for (var ")
            .Append(batch.Result)
            .Append(" = 0; ")
            .Append(batch.Result)
            .Append(" < ")
            .Append(batch.ItemIndex)
            .Append(" - ")
            .Append(batch.First)
            .Append("; ")
            .Append(batch.Result)
            .Append("++)\n")
            .Append(indent)
            .Append("{\n");
    }

    // One command's result set, its rows added to the buffer: every row for many; the one row for one, which throws
    // for none or a second; the row when there is one for one-optional, which throws for a second.  An exception names
    // the item by its position.
    private static void AppendResultSet(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var batch = context.Locals.Batch!;
        var innerIndent = GeneratedText.Deeper(indent);
        var itemIndex = batch.First + " + " + batch.Result;
        switch (context.Method.Shape)
        {
            case ResultShape.Many:
                RowsWriter.AppendReadLoop(in context, indent, output);
                break;
            case ResultShape.One:
                _ = builder.Append(indent).Append("if (!");
                RowsWriter.AppendRead(in context);
                _ = builder.Append(")\n").Append(indent).Append("{\n");
                RowsWriter.AppendThrow(in context, innerIndent, moreThanOne: false, itemIndex);
                QueryMethodWriter.AppendEnd(builder, indent);
                _ = builder.Append('\n');
                RowsWriter.AppendAdd(in context, indent, output);
                RowsWriter.AppendSecondRowThrow(in context, indent, itemIndex);
                break;
            case ResultShape.OneOptional:
                _ = builder.Append(indent).Append("if (");
                RowsWriter.AppendRead(in context);
                _ = builder.Append(")\n").Append(indent).Append("{\n");
                RowsWriter.AppendAdd(in context, innerIndent, output);
                RowsWriter.AppendSecondRowThrow(in context, innerIndent, itemIndex);
                QueryMethodWriter.AppendEnd(builder, indent);
                break;
            // A method that reads no rows runs its batch without a reader and never reaches this switch; the shapes
            // are named so that the switch lists every member.
            case ResultShape.RowCount:
            case ResultShape.None:
                break;
            default:
                throw UnexpectedEnumValueException.Of(context.Method.Shape);
        }
    }
}
