using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the execution of a query's command and the reading of its rows, the last of
/// <see cref="QueryMethodWriter" />'s phases, inside the <c>try</c> that <see cref="CommandWriter" /> writes: the rows
/// read into a buffer and the collection returned, the one row read and returned, or each row yielded as the loop asks
/// for it.  <see cref="RowReaderWriter" /> writes the reading of each row.
/// </summary>
internal static class RowsWriter
{
    private const string ExceptionType = "global::SqlSource.UnexpectedRowCountException";

    internal static void AppendExecution(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var method = context.Method;
        var locals = context.Locals;
        var spelling = context.Spelling;
        if (method.Shape is ResultShape.None or ResultShape.RowCount)
        {
            _ = builder.Append(indent).Append(method.Shape == ResultShape.RowCount ? "return " : string.Empty);
            _ = spelling.AppendCallStart(builder, locals.Command, DriverCall.ExecuteNonQuery);
            _ = spelling.AppendToken(builder, MethodArguments.CancellationToken, afterArgument: false);
            _ = spelling.AppendCallEnd(builder).Append(";\n");
            return;
        }

        var output = method.Output!;
        _ = builder.Append(indent).Append("var ").Append(locals.Reader).Append(" = ");
        _ = spelling
            .AppendCallStart(builder, locals.Command, DriverCall.ExecuteReader)
            .Append("global::System.Data.CommandBehavior.Default");
        _ = spelling.AppendToken(builder, MethodArguments.CancellationToken, afterArgument: true);
        _ = spelling.AppendCallEnd(builder).Append(";\n");
        _ = spelling.AppendUsing(builder, indent, locals.ReaderScope, locals.Reader);

        // A streaming method yields each row as it reads it, where any other reads them into its buffer and returns
        // the collection.
        if (context.Streams)
        {
            AppendStream(in context, indent, output);
            return;
        }

        // A method that checks for a rollback has the read, its drain and its returns in a try whose catches check: the
        // database's error first, then an exception of the method's own.
        var checksAfterDatabaseError = context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAfterDatabaseError);
        var checksAtRead = context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead);
        if (!checksAfterDatabaseError && !checksAtRead)
        {
            AppendRows(in context, indent, output);
            return;
        }

        _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
        AppendRows(in context, GeneratedText.Deeper(indent), output);
        QueryMethodWriter.AppendEnd(builder, indent);
        if (checksAfterDatabaseError)
        {
            ExecutionCapabilityWriter.AppendDatabaseErrorCheck(in context, indent);
        }

        if (checksAtRead)
        {
            ExecutionCapabilityWriter.AppendRollbackCheck(in context, indent);
        }
    }

    // The rows read into the buffer and the collection returned, or the one row read and returned, each return after
    // the drain when the capabilities have it.
    private static void AppendRows(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var innerIndent = GeneratedText.Deeper(indent);
        if (context.Method.Shape == ResultShape.Many)
        {
            var (buffer, result) = GetBuffer(in context, output.TypeText);
            _ = builder.Append(indent).Append("var ").Append(locals.Rows).Append(" = ").Append(buffer).Append(";\n");
            AppendReadLoop(in context, indent, output);
            _ = builder.Append('\n');
            AppendDrain(in context, indent);
            _ = builder.Append(indent).Append("return ").Append(result).Append(";\n");
            return;
        }

        _ = builder.Append(indent).Append("if (!");
        AppendRead(in context);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        if (context.Method.Shape == ResultShape.One)
        {
            AppendThrow(in context, innerIndent, moreThanOne: false);
        }
        else
        {
            AppendDrain(in context, innerIndent);
            _ = builder.Append(innerIndent).Append("return null;\n");
        }

        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append('\n');
        RowReaderWriter.AppendVectorChecks(in context, indent, output);
        _ = builder.Append(indent).Append("var ").Append(locals.Row).Append(" = ");
        RowReaderWriter.AppendRow(in context, output);
        _ = builder.Append(";\n");
        AppendSecondRowThrow(in context, indent);
        _ = builder.Append('\n');
        AppendDrain(in context, indent);
        _ = builder.Append(indent).Append("return ").Append(locals.Row).Append(";\n");
    }

    // The iterator's loop, which yields each row as it reads it.  A method that drains when its loop is left at a row
    // has the loop, and the drain after it, in a try whose finally drains then.  C# allows no catch on a try that holds
    // a yield return, so a method that checks for a rollback at its read builds each row in a try of its own, whose
    // catch checks when the row's checks or construction throw, and one that checks after a database error reads each
    // row in a try of its own, whose catch takes the database's error, as each drain's does.  A method that checks at
    // its read or tracks the row builds the row into a local before it yields it.
    private static void AppendStream(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var tracksRow = context.Capabilities.Has(ExecutionCapabilities.DrainsWhenLeftAtARow);
        var checksRollback = context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead);
        var loopIndent = tracksRow ? GeneratedText.Deeper(indent) : indent;
        var innerIndent = GeneratedText.Deeper(loopIndent);
        if (tracksRow)
        {
            ExecutionCapabilityWriter.AppendLeftAtARowStart(in context, indent);
        }

        if (context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAfterDatabaseError))
        {
            AppendCheckedRead(in context, loopIndent);
        }
        else
        {
            _ = builder.Append(loopIndent).Append("while (");
            AppendRead(in context);
            _ = builder.Append(")\n").Append(loopIndent).Append("{\n");
        }

        if (!tracksRow && !checksRollback)
        {
            RowReaderWriter.AppendVectorChecks(in context, innerIndent, output);
            _ = builder.Append(innerIndent).Append("yield return ");
            RowReaderWriter.AppendRow(in context, output);
            _ = builder.Append(";\n");
        }
        else
        {
            AppendRowIntoLocal(in context, innerIndent, output);
            _ = builder.Append('\n');
            if (tracksRow)
            {
                ExecutionCapabilityWriter.AppendYieldAtARow(in context, innerIndent);
            }
            else
            {
                _ = builder.Append(innerIndent).Append("yield return ").Append(locals.Row).Append(";\n");
            }
        }

        QueryMethodWriter.AppendEnd(builder, loopIndent);
        if (context.Capabilities.Has(ExecutionCapabilities.DrainsLaterResults))
        {
            ExecutionCapabilityWriter.AppendDrainAfterLoop(in context, loopIndent);
        }

        if (tracksRow)
        {
            ExecutionCapabilityWriter.AppendLeftAtARowEnd(in context, indent, loopIndent, innerIndent);
        }
    }

    // The start of a loop that reads its row in a try of its own, followed by the catch of the database's error, and
    // leaves when the read finds no row, up to the blank line before the row.
    private static void AppendCheckedRead(in QueryMethodContext context, string indent)
    {
        var builder = context.Builder;
        var hasRow = context.Locals.HasRow;
        var innerIndent = GeneratedText.Deeper(indent);
        var readIndent = GeneratedText.Deeper(innerIndent);
        _ = builder
            .Append(indent)
            .Append("while (true)\n")
            .Append(indent)
            .Append("{\n")
            .Append(innerIndent)
            .Append("bool ")
            .Append(hasRow)
            .Append(";\n")
            .Append(innerIndent)
            .Append("try\n")
            .Append(innerIndent)
            .Append("{\n")
            .Append(readIndent)
            .Append(hasRow)
            .Append(" = ");
        AppendRead(in context);
        _ = builder.Append(";\n");
        QueryMethodWriter.AppendEnd(builder, innerIndent);
        ExecutionCapabilityWriter.AppendDatabaseErrorCheck(in context, innerIndent);
        _ = builder.Append('\n').Append(innerIndent).Append("if (!").Append(hasRow).Append(")\n");
        _ = builder.Append(innerIndent).Append("{\n").Append(readIndent).Append("break;\n");
        QueryMethodWriter.AppendEnd(builder, innerIndent);
        _ = builder.Append('\n');
    }

    // The row's local, declared, then its checks and its construction, in a try whose catch checks for a rollback when
    // the method has it.
    private static void AppendRowIntoLocal(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var locals = context.Locals;
        var checksRollback = context.Capabilities.Has(ExecutionCapabilities.ChecksRollbackAtRead);
        _ = builder.Append(indent).Append(output.TypeText).Append(' ').Append(locals.Row).Append(";\n");
        var rowIndent = indent;
        if (checksRollback)
        {
            _ = builder.Append(indent).Append("try\n").Append(indent).Append("{\n");
            rowIndent = GeneratedText.Deeper(indent);
        }

        RowReaderWriter.AppendVectorChecks(in context, rowIndent, output);
        _ = builder.Append(rowIndent).Append(locals.Row).Append(" = ");
        RowReaderWriter.AppendRow(in context, output);
        _ = builder.Append(";\n");
        if (checksRollback)
        {
            QueryMethodWriter.AppendEnd(builder, indent);
            ExecutionCapabilityWriter.AppendRollbackCheck(in context, indent);
        }
    }

    // The drain before a buffered read returns, when the capabilities have it.
    private static void AppendDrain(in QueryMethodContext context, string indent)
    {
        if (context.Capabilities.Has(ExecutionCapabilities.DrainsLaterResults))
        {
            ExecutionCapabilityWriter.AppendDrainBeforeReturn(in context, indent);
        }
    }

    // The loop that reads every row into the buffer, after its checks: a many query method's, or a collection
    // method's of each result set.
    internal static void AppendReadLoop(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        _ = builder.Append(indent).Append("while (");
        AppendRead(in context);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        AppendAdd(in context, GeneratedText.Deeper(indent), output);
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The row's checks, then its addition to the buffer.
    internal static void AppendAdd(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        RowReaderWriter.AppendVectorChecks(in context, indent, output);
        _ = builder.Append(indent).Append(context.Locals.Rows).Append(".Add(");
        RowReaderWriter.AppendRow(in context, output);
        _ = builder.Append(");\n");
    }

    // The read of a second row, which throws UnexpectedRowCountException, with the item's position in a collection
    // method.
    internal static void AppendSecondRowThrow(in QueryMethodContext context, string indent, string? itemIndex = null)
    {
        var builder = context.Builder;
        _ = builder.Append(indent).Append("if (");
        AppendRead(in context);
        _ = builder.Append(")\n").Append(indent).Append("{\n");
        AppendThrow(in context, GeneratedText.Deeper(indent), moreThanOne: true, itemIndex);
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // A read of the next row, as a condition: a query method's, or a collection method's of each result set.
    internal static void AppendRead(in QueryMethodContext context)
    {
        var spelling = context.Spelling;
        _ = spelling.AppendCallStart(context.Builder, context.Locals.Reader, DriverCall.Read);
        _ = spelling.AppendToken(context.Builder, MethodArguments.CancellationToken, afterArgument: false);
        _ = spelling.AppendCallEnd(context.Builder);
    }

    // The throw of UnexpectedRowCountException, with the item's position, an expression, in a collection method.
    internal static void AppendThrow(
        in QueryMethodContext context,
        string indent,
        bool moreThanOne,
        string? itemIndex = null
    )
    {
        _ = context
            .Builder.Append(indent)
            .Append("throw new ")
            .Append(ExceptionType)
            .Append('(')
            .Append(SymbolDisplay.FormatLiteral(context.Method.QueryName, quote: true))
            .Append(moreThanOne ? ", true" : ", false");
        _ = itemIndex is null ? context.Builder : context.Builder.Append(", ").Append(itemIndex);
        _ = context.Builder.Append(");\n");
    }

    // The type the task gives, or a synchronous method returns, or for a streaming method the IAsyncEnumerable it
    // returns; null for a task that gives nothing, or a method that returns void.
    internal static string? GetResultType(in QueryMethodContext context) =>
        context.Method.Shape switch
        {
            ResultShape.Many => GetCollectionType(context.Method.Collection, context.Method.Output!.TypeText),
            ResultShape.One or ResultShape.OneOptional when context.Method.Batch is not null => GetCollectionType(
                context.Method.Collection,
                context.Method.Output!.TypeText
            ),
            ResultShape.One => context.Method.Output!.TypeText,
            ResultShape.OneOptional => context.Method.Output!.TypeText + "?",
            ResultShape.RowCount => "int",
            ResultShape.None => null,
            _ => throw UnexpectedEnumValueException.Of(context.Method.Shape),
        };

    private static string GetCollectionType(CollectionKind collection, string row) =>
        collection switch
        {
            CollectionKind.Array => row + "[]",
            CollectionKind.IEnumerable => "global::System.Collections.Generic.IEnumerable<" + row + ">",
            CollectionKind.ICollection => "global::System.Collections.Generic.ICollection<" + row + ">",
            CollectionKind.IReadOnlyCollection => "global::System.Collections.Generic.IReadOnlyCollection<" + row + ">",
            CollectionKind.IList => "global::System.Collections.Generic.IList<" + row + ">",
            CollectionKind.IReadOnlyList => "global::System.Collections.Generic.IReadOnlyList<" + row + ">",
            CollectionKind.List => "global::System.Collections.Generic.List<" + row + ">",
            CollectionKind.ImmutableArray => "global::System.Collections.Immutable.ImmutableArray<" + row + ">",
            CollectionKind.ImmutableList => "global::System.Collections.Immutable.ImmutableList<" + row + ">",
            CollectionKind.IAsyncEnumerable => "global::System.Collections.Generic.IAsyncEnumerable<" + row + ">",
            CollectionKind.IImmutableList => "global::System.Collections.Immutable.IImmutableList<" + row + ">",
            _ => throw UnexpectedEnumValueException.Of(collection),
        };

    // The buffer the rows are read into, and the expression that gives the collection from it: a many query method's,
    // or a collection method's of any shape that reads rows.
    internal static (string Buffer, string Result) GetBuffer(in QueryMethodContext context, string row)
    {
        var method = context.Method;
        var rows = context.Locals.Rows;
        if (RowBuffer.IsUsedBy(method))
        {
            var toResult = method.Collection switch
            {
                CollectionKind.List or CollectionKind.IList or CollectionKind.ICollection => ".ToList()",
                CollectionKind.ImmutableArray => ".ToImmutableArray()",
                // The two immutable lists and IAsyncEnumerable never reach this switch, since RowBuffer.IsUsedBy leaves
                // them out; they are named so that the switch lists every member.
                CollectionKind.Array
                or CollectionKind.IEnumerable
                or CollectionKind.IReadOnlyCollection
                or CollectionKind.IReadOnlyList
                or CollectionKind.ImmutableList
                or CollectionKind.IImmutableList
                or CollectionKind.IAsyncEnumerable => ".ToArray()",
                _ => throw UnexpectedEnumValueException.Of(method.Collection),
            };
            return ("new " + RowBuffer.TypeText + "<" + row + ">()", rows + toResult);
        }

        return method.Collection switch
        {
            CollectionKind.List or CollectionKind.IList or CollectionKind.ICollection => (
                "new global::System.Collections.Generic.List<" + row + ">()",
                rows
            ),
            CollectionKind.ImmutableArray => (
                "global::System.Collections.Immutable.ImmutableArray.CreateBuilder<" + row + ">()",
                rows + ".DrainToImmutable()"
            ),
            CollectionKind.ImmutableList or CollectionKind.IImmutableList => (
                "global::System.Collections.Immutable.ImmutableList.CreateBuilder<" + row + ">()",
                rows + ".ToImmutable()"
            ),
            // A streaming method buffers nothing and never reaches this switch; IAsyncEnumerable is named so that the
            // switch lists every member.
            CollectionKind.Array
            or CollectionKind.IEnumerable
            or CollectionKind.IReadOnlyCollection
            or CollectionKind.IReadOnlyList
            or CollectionKind.IAsyncEnumerable => (
                "new global::System.Collections.Generic.List<" + row + ">()",
                rows + ".ToArray()"
            ),
            _ => throw UnexpectedEnumValueException.Of(method.Collection),
        };
    }
}
