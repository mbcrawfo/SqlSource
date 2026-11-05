using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Models;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the binding of a list of a query's method: each value through the engine's <see cref="ParameterBinder" />,
/// as a parameter of the element's type, and the parameter of its null test.
/// </summary>
internal static class ListBindingWriter
{
    /// <summary>
    /// Appends, in a block of its own, the binding of each value of the list, each through the engine's binder as a
    /// parameter of the element's type named by the list's prefix and its index, with the last value bound again up
    /// to the list's size; or, for an empty or null list, of its one <c>NULL</c> of the element's type; and then, in
    /// another block, the parameter of its null test when it has one.  A list whose enumeration gives another number of
    /// values than its count throws <c>InvalidOperationException</c>.
    /// </summary>
    internal static void AppendBindings(
        in QueryMethodContext context,
        ParameterBinder binder,
        in BindingContext binding,
        ListLocal list,
        string indent
    )
    {
        var builder = context.Builder;
        var lists = context.Locals.Lists!;
        var parameter = list.Parameter;
        var prefix = parameter.ExpansionPrefix!;
        var block = GeneratedText.Deeper(indent);
        var inner = GeneratedText.Deeper(block);
        var loop = GeneratedText.Deeper(inner);
        var bound = GeneratedText.Deeper(loop);

        _ = builder.Append(indent).Append("{\n");
        QueryMethodWriter.AppendIf(builder, block, list.Values + " is null || " + list.Count + " == 0");
        var empty = EmptyOf(parameter);
        binder.Append(
            binding with
            {
                Indent = inner,
                ValueExpression = "default(" + empty.TypeText + "?)",
                NameExpression = SymbolDisplay.FormatLiteral(prefix + "0", quote: true),
                IsNull = true,
            },
            empty
        );
        var changed = ChangedOf(parameter);
        QueryMethodWriter.AppendEnd(builder, block);
        _ = builder
            .Append(block)
            .Append("else\n")
            .Append(block)
            .Append("{\n")
            .Append(inner)
            .Append("var ")
            .Append(lists.Index)
            .Append(" = 0;\n")
            .Append(inner)
            .Append("foreach (var ")
            .Append(lists.Element)
            .Append(" in ")
            .Append(list.Values)
            .Append(")\n")
            .Append(inner)
            .Append("{\n");
        AppendThrowWhen(builder, loop, lists.Index + " >= " + list.Count, changed);
        _ = builder
            .Append('\n')
            .Append(loop)
            .Append("var ")
            .Append(lists.End)
            .Append(" = ")
            .Append(lists.Index)
            .Append(" + 1 == ")
            .Append(list.Count)
            .Append(" ? ")
            .Append(list.Size)
            .Append(" : ")
            .Append(lists.Index)
            .Append(" + 1;\n")
            .Append(loop)
            .Append("for (; ")
            .Append(lists.Index)
            .Append(" < ")
            .Append(lists.End)
            .Append("; ")
            .Append(lists.Index)
            .Append("++)\n")
            .Append(loop)
            .Append("{\n");
        binder.Append(
            binding with
            {
                Indent = bound,
                ValueExpression = lists.Element,
                NameExpression =
                    SymbolDisplay.FormatLiteral(prefix, quote: true)
                    + " + "
                    + lists.Index
                    + ".ToString("
                    + ListWriter.Invariant
                    + ")",
            },
            parameter with
            {
                IsNullable = false,
                Sequence = ParameterSequence.None,
            }
        );
        QueryMethodWriter.AppendEnd(builder, loop);
        QueryMethodWriter.AppendEnd(builder, inner);
        _ = builder.Append('\n');
        AppendThrowWhen(builder, inner, lists.Index + " < " + list.Count, changed);
        QueryMethodWriter.AppendEnd(builder, block);
        QueryMethodWriter.AppendEnd(builder, indent);
        if (parameter.HasNullTest)
        {
            _ = builder.Append('\n').Append(indent).Append("{\n");
            binder.AppendNullTest(binding with { Indent = block, ValueExpression = list.Values }, parameter);
            QueryMethodWriter.AppendEnd(builder, indent);
        }
    }

    // The throw of a list whose enumeration gives another number of values than the count the SQL was built for: the
    // loop meets a value when it holds the count already, or ends before it.  Every value it binds past the count would
    // be a name the SQL never uses, and one it lacks a name the SQL uses with no value.  After the last counted value
    // the index is the size, which is at least the count, so a value after it throws too.
    private static string ChangedOf(MethodParameter list) =>
        "throw new global::System.InvalidOperationException("
        + SymbolDisplay.FormatLiteral(
            "The list '" + list.Property + "' changed after the query counted it.",
            quote: true
        )
        + ");";

    private static void AppendThrowWhen(StringBuilder builder, string indent, string condition, string changed)
    {
        QueryMethodWriter.AppendIf(builder, indent, condition);
        _ = builder.Append(indent).Append(GeneratedText.Indent).Append(changed).Append('\n');
        QueryMethodWriter.AppendEnd(builder, indent);
    }

    // The empty form's one parameter: the element, as a value that is null.  A converted element is bound by the
    // database's side of its converter, which a null never reaches, so that the value is a default and no pattern.
    private static MethodParameter EmptyOf(MethodParameter list) =>
        list.Write is { } write
            ? list with
            {
                TypeText = write.DatabaseText,
                IsValueType = write.DatabaseIsValueType,
                IsNullable = true,
                Write = null,
                Sequence = ParameterSequence.None,
            }
            : list with
            {
                IsNullable = true,
                Sequence = ParameterSequence.None,
            };
}
