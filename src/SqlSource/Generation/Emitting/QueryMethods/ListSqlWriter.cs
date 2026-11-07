using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Parsing.Tokens;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes each use of a list in the SQL that a query's method builds with <c>string.Create</c>, for
/// <see cref="MethodWriter.AppendCreate" />: the list's values as <c>(@ids_0, @ids_1, …)</c> up to its size, or, when
/// it is empty or null, <c>(@ids_0)</c> for a use whose segment is <see cref="SqlSegment.EmptyIsNull" /> and
/// <c>(SELECT @ids_0 WHERE 1 = 0)</c> for any other.  The state of <c>string.Create</c> holds each list's count and
/// size after the tokens' text, and the length counts each use's text from them.
/// </summary>
internal static class ListSqlWriter
{
    /// <summary>
    /// The state's values after the tokens: each list's count and size.
    /// </summary>
    internal static List<string> StateOf(List<string> tokens, ListLocals lists)
    {
        var state = new List<string>(tokens.Count + (2 * lists.Each.Count));
        state.AddRange(tokens);
        foreach (var list in lists.Each)
        {
            state.Add(list.Count);
            state.Add(list.Size);
        }

        return state;
    }

    /// <summary>
    /// The place in the state, from 0, of the count of the list; its size is the next.
    /// </summary>
    internal static int CountPlaceOf(List<string> tokens, int list) => tokens.Count + (2 * list);

    /// <summary>
    /// Appends the length of the uses of a list in one empty form, ` + (...)`, with ` * uses` when there is more than
    /// one: the empty form's for a count of 0, and otherwise a parenthesis and a name for each value, a comma and a
    /// space between two, and the digits of every index, which <see cref="ListLocals.DigitsUpTo" /> gives.
    /// </summary>
    internal static void AppendLength(StringBuilder builder, ListLocals lists, int list, int uses, bool emptyIsNull)
    {
        var locals = lists.Each[list];
        var prefix = locals.Parameter.ExpansionPrefix!;
        _ = builder
            .Append(" + (")
            .Append(locals.Count)
            .Append(" == 0 ? ")
            .Append(EmptyOf('@', prefix, emptyIsNull).Length.ToString(CultureInfo.InvariantCulture))
            .Append(" : ")
            .Append(locals.Size)
            .Append(" * ")
            .Append((prefix.Length + 3).ToString(CultureInfo.InvariantCulture))
            .Append(" + ")
            .Append(lists.DigitsUpTo)
            .Append('(')
            .Append(locals.Size)
            .Append("))");
        if (uses > 1)
        {
            _ = builder.Append(" * ").Append(uses.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Appends the steps that write one use of a list, and after them, unless it is the last segment, the slice that
    /// moves the span past it.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each step.</param>
    /// <param name="lists">The method's lists.</param>
    /// <param name="list">The list's index in <see cref="ListLocals.Each" />.</param>
    /// <param name="use">The use as the SQL writes it, whose first character is the parameter's prefix.</param>
    /// <param name="emptyIsNull">Whether the use is written empty as one <c>NULL</c>.</param>
    /// <param name="span">The name of the lambda's span.</param>
    /// <param name="count">The state's value of the list's count.</param>
    /// <param name="size">The state's value of the list's size.</param>
    /// <param name="isLast">Whether the use is the last segment of the SQL.</param>
    internal static void AppendSteps(
        StringBuilder builder,
        string indent,
        ListLocals lists,
        int list,
        string use,
        bool emptyIsNull,
        string span,
        string count,
        string size,
        bool isLast
    )
    {
        var prefix = lists.Each[list].Parameter.ExpansionPrefix!;
        var empty = EmptyOf(use[0], prefix, emptyIsNull);
        var first = "(" + use[0] + prefix + "0";
        var next = ", " + use[0] + prefix;
        var block = GeneratedText.Deeper(indent);
        var loop = GeneratedText.Deeper(block);
        var index = lists.Index;
        QueryMethodWriter.AppendIf(builder, indent, count + " == 0");
        AppendCopy(builder, block, span, empty, isLast);
        QueryMethodWriter.AppendEnd(builder, indent);
        _ = builder.Append(indent).Append("else\n").Append(indent).Append("{\n");
        AppendCopy(builder, block, span, first, isLast: false);
        _ = builder
            .Append(block)
            .Append("for (var ")
            .Append(index)
            .Append(" = 1; ")
            .Append(index)
            .Append(" < ")
            .Append(size)
            .Append("; ")
            .Append(index)
            .Append("++)\n")
            .Append(block)
            .Append("{\n")
            .Append(loop)
            .Append(SymbolDisplay.FormatLiteral(next, quote: true))
            .Append(".CopyTo(")
            .Append(span)
            .Append(");\n")
            .Append(loop)
            .Append(index)
            .Append(".TryFormat(")
            .Append(span)
            .Append(".Slice(")
            .Append(next.Length.ToString(CultureInfo.InvariantCulture))
            .Append("), out var ")
            .Append(lists.Written)
            .Append(", default, ")
            .Append(ListWriter.Invariant)
            .Append(");\n")
            .Append(loop)
            .Append(span)
            .Append(" = ")
            .Append(span)
            .Append(".Slice(")
            .Append(next.Length.ToString(CultureInfo.InvariantCulture))
            .Append(" + ")
            .Append(lists.Written)
            .Append(");\n");
        QueryMethodWriter.AppendEnd(builder, block);
        _ = builder.Append('\n').Append(block).Append(span).Append("[0] = ')';\n");
        if (!isLast)
        {
            _ = builder.Append(block).Append(span).Append(" = ").Append(span).Append(".Slice(1);\n");
        }

        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The empty forms.  The subquery has no row, so IN matches no row and NOT IN every row.  One NULL makes IN
    // unknown, which matches no row where no NOT reaches it, and SQL Server takes it where it refuses a subquery: in
    // an aggregate, a GROUP BY and an OUTPUT clause.
    private static string EmptyOf(char marker, string prefix, bool emptyIsNull) =>
        emptyIsNull ? "(" + marker + prefix + "0)" : "(SELECT " + marker + prefix + "0 WHERE 1 = 0)";

    private static void AppendCopy(StringBuilder builder, string indent, string span, string text, bool isLast)
    {
        _ = builder
            .Append(indent)
            .Append(SymbolDisplay.FormatLiteral(text, quote: true))
            .Append(".CopyTo(")
            .Append(span)
            .Append(");\n");
        if (!isLast)
        {
            _ = builder
                .Append(indent)
                .Append(span)
                .Append(" = ")
                .Append(span)
                .Append(".Slice(")
                .Append(text.Length.ToString(CultureInfo.InvariantCulture))
                .Append(");\n");
        }
    }
}
