using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes, beside the checks of a query method's arguments, the count of each list it sends, its size, padded under
/// <see cref="QueryMethod.PadsLists" />, and the check of the total against the engine's limit, which
/// <see cref="ListWriter" /> gives.
/// </summary>
internal static class ListCountWriter
{
    /// <summary>
    /// Appends, for each list, the local that reads it once, its count and its size, and then the check of the total
    /// against the engine's limit and the padding of each size.  It runs where the arguments are checked, before any
    /// await and before a streaming method's iterator, so that the method throws when it is called.
    /// </summary>
    internal static void AppendCounts(in QueryMethodContext context, string indent)
    {
        if (context.Locals.Lists is not { } lists)
        {
            return;
        }

        var builder = context.Builder;
        var parameters = context.Method.Input!.Parameters;
        foreach (var list in lists.Each)
        {
            _ = builder
                .Append(indent)
                .Append("var ")
                .Append(list.Values)
                .Append(" = ")
                .Append(MethodArguments.Parameters)
                .Append('.')
                .Append(list.Parameter.Property)
                .Append(";\n")
                .Append(indent)
                .Append("var ")
                .Append(list.Count)
                .Append(" = ")
                .Append(lists.CountOf)
                .Append("(ref ")
                .Append(list.Values)
                .Append(");\n")
                .Append(indent)
                .Append("var ")
                .Append(list.Size)
                .Append(" = global::System.Math.Max(")
                .Append(list.Count)
                .Append(", 1);\n");
        }

        // The parameters that are no list, and the null test of each list that has one.
        var others = 0;
        foreach (var parameter in parameters)
        {
            others += !parameter.IsList || parameter.HasNullTest ? 1 : 0;
        }

        var limit = ListWriter.LimitOf(in context);
        var limitText = limit.Parameters.ToString(CultureInfo.InvariantCulture);
        _ = builder.Append(indent).Append("var ").Append(lists.Total).Append(" = ");
        AppendSum(builder, lists, others, static list => list.Size, asLong: true);
        _ = builder.Append(";\n");
        QueryMethodWriter.AppendIf(builder, indent, lists.Total + " > " + limitText);
        AppendThrow(in context, GeneratedText.Deeper(indent), lists, limit);
        QueryMethodWriter.AppendEnd(builder, indent);
        if (context.Method.PadsLists)
        {
            _ = builder.Append('\n');
            foreach (var list in lists.Each)
            {
                _ = builder
                    .Append(indent)
                    .Append("var ")
                    .Append(list.Padded)
                    .Append(" = ")
                    .Append(lists.PaddedSize)
                    .Append('(')
                    .Append(list.Size)
                    .Append(");\n");
            }

            _ = builder.Append(indent).Append("if (");
            AppendSum(builder, lists, others, static list => list.Padded, asLong: false);
            _ = builder.Append(" <= ").Append(limitText).Append(")\n").Append(indent).Append("{\n");
            foreach (var list in lists.Each)
            {
                _ = builder
                    .Append(indent)
                    .Append(GeneratedText.Indent)
                    .Append(list.Size)
                    .Append(" = ")
                    .Append(list.Padded)
                    .Append(";\n");
            }

            QueryMethodWriter.AppendEnd(builder, indent);
        }

        // A streaming method's call of its iterator starts with a blank line of its own.
        if (!context.Streams)
        {
            _ = builder.Append('\n');
        }
    }

    // The parameters that are no list and the null tests, then each list's size or padded size: a long for the total,
    // so that no count a collection gives can wrap it around, and an int for the padded, each size of which is at
    // most the limit.
    private static void AppendSum(
        StringBuilder builder,
        ListLocals lists,
        int others,
        System.Func<ListLocal, string> part,
        bool asLong
    )
    {
        var first = true;
        if (others > 0)
        {
            _ = builder.Append(others.ToString(CultureInfo.InvariantCulture)).Append(asLong ? "L" : "");
            first = false;
        }

        foreach (var list in lists.Each)
        {
            if (first)
            {
                _ = builder.Append(asLong ? "(long)" : "").Append(part(list));
                first = false;
            }
            else
            {
                _ = builder.Append(" + ").Append(part(list));
            }
        }
    }

    // The ArgumentException of a total over the limit, which names the property of the largest list, the first of
    // those of the largest count.
    private static void AppendThrow(
        in QueryMethodContext context,
        string indent,
        ListLocals lists,
        ListWriter.EngineLimit limit
    )
    {
        var argument = GeneratedText.Deeper(indent);
        var continued = GeneratedText.Deeper(argument);
        var start = "The query '" + context.Method.QueryName + "' would send ";
        var middle =
            " parameters; "
            + limit.Engine
            + " accepts "
            + limit.Parameters.ToString(CultureInfo.InvariantCulture)
            + ".  Pass fewer values in '";
        var end = "'" + limit.Advice + ".";
        var builder = context.Builder;
        _ = builder
            .Append(indent)
            .Append("throw new global::System.ArgumentException(\n")
            .Append(argument)
            .Append(SymbolDisplay.FormatLiteral(start, quote: true))
            .Append('\n')
            .Append(continued)
            .Append("+ ")
            .Append(lists.Total)
            .Append(".ToString(")
            .Append(ListWriter.Invariant)
            .Append(")\n")
            .Append(continued)
            .Append("+ ");
        if (lists.Each.Count == 1)
        {
            _ = builder.Append(
                SymbolDisplay.FormatLiteral(middle + lists.Each[0].Parameter.Property + end, quote: true)
            );
        }
        else
        {
            _ = builder
                .Append(SymbolDisplay.FormatLiteral(middle, quote: true))
                .Append('\n')
                .Append(continued)
                .Append("+ (");
            AppendLargest(builder, lists, lists.Each.Count - 1);
            _ = builder
                .Append(")\n")
                .Append(continued)
                .Append("+ ")
                .Append(SymbolDisplay.FormatLiteral(end, quote: true));
        }

        _ = builder
            .Append(",\n")
            .Append(argument)
            .Append(SymbolDisplay.FormatLiteral(MethodArguments.Parameters, quote: true))
            .Append('\n')
            .Append(indent)
            .Append(");\n");
    }

    // The property of the largest of the lists up to last, the first of the largest: last's when its count is above
    // every earlier one's, and otherwise the largest of those before it.
    private static void AppendLargest(StringBuilder builder, ListLocals lists, int last)
    {
        var property = SymbolDisplay.FormatLiteral(lists.Each[last].Parameter.Property, quote: true);
        if (last == 0)
        {
            _ = builder.Append(property);
            return;
        }

        _ = builder.Append(lists.Each[last].Count).Append(" > ");
        AppendMax(builder, lists, last - 1);
        _ = builder.Append(" ? ").Append(property).Append(" : ");
        AppendLargest(builder, lists, last - 1);
    }

    // The largest count of the lists up to last.
    private static void AppendMax(StringBuilder builder, ListLocals lists, int last)
    {
        if (last == 0)
        {
            _ = builder.Append(lists.Each[0].Count);
            return;
        }

        _ = builder.Append("global::System.Math.Max(");
        AppendMax(builder, lists, last - 1);
        _ = builder.Append(", ").Append(lists.Each[last].Count).Append(')');
    }
}
