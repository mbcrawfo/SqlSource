using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Methods;
using Conversion = SqlSource.Generation.Methods.Conversion;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the reading of a row of a query's method from its reader: the checks of the columns read into locals first,
/// and the expression that builds the row from its columns.
/// </summary>
internal static class RowReaderWriter
{
    /// <summary>
    /// A not-null column read as <c>SqlVector&lt;float&gt;</c> is read into a local first, and its <c>Null</c> is
    /// refused as a <c>NULL</c> of any other not-null column is.
    /// </summary>
    public static void AppendVectorChecks(
        StringBuilder builder,
        string indent,
        MethodOutput output,
        QueryMethodLocals locals
    )
    {
        for (var index = 0; index < output.Columns.Count; index++)
        {
            if (locals.Values[index] is not { } value)
            {
                continue;
            }

            var column = output.Columns[index];
            _ = builder
                .Append(indent)
                .Append("var ")
                .Append(value)
                .Append(" = ")
                .Append(locals.Reader)
                .Append(".GetFieldValue<")
                .Append(column.ReadText)
                .Append(">(")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(");\n");
            QueryMethodWriter.AppendIf(builder, indent, value + ".IsNull");
            _ = builder
                .Append(indent)
                .Append(GeneratedText.Indent)
                .Append("throw new global::System.InvalidCastException(")
                .Append(SymbolDisplay.FormatLiteral("Column '" + column.Name + "' is null.", quote: true))
                .Append(");\n");
            QueryMethodWriter.AppendEnd(builder, indent);
            _ = builder.Append('\n');
        }
    }

    /// <summary>
    /// A record by its positional constructor, its arguments in the order of its properties; a class by an object
    /// initializer, in column order.
    /// </summary>
    public static void AppendRow(StringBuilder builder, MethodOutput output, QueryMethodLocals locals)
    {
        _ = builder.Append("new ").Append(output.TypeText);
        if (output.IsRecord)
        {
            _ = builder.Append('(');
            for (var index = 0; index < output.ConstructorOrder.Count; index++)
            {
                _ = builder.Append(index > 0 ? ", " : string.Empty);
                AppendColumn(builder, output, output.ConstructorOrder[index], locals);
            }

            _ = builder.Append(')');
            return;
        }

        _ = builder.Append(" { ");
        for (var index = 0; index < output.Columns.Count; index++)
        {
            _ = builder.Append(index > 0 ? ", " : string.Empty).Append(output.Columns[index].Property).Append(" = ");
            AppendColumn(builder, output, index, locals);
        }

        _ = builder.Append(" }");
    }

    // A not-null column by GetFieldValue<T> alone; a nullable one by IsDBNull first, and never by GetFieldValue<T?>,
    // which SqlClient refuses on a NULL.  A column with a converter is read as the converter's database side and
    // passed to it, so that the converter never sees a NULL; an array converted by element is read as the database's
    // array and each of its elements passed to it; and the spatial read path reads the column's bytes.
    private static void AppendColumn(StringBuilder builder, MethodOutput output, int ordinal, QueryMethodLocals locals)
    {
        var column = output.Columns[ordinal];
        var read = column.Read;
        if (locals.Values[ordinal] is { } value)
        {
            _ = read is null
                ? builder.Append(value)
                : builder.Append(read.Method).Append('(').Append(value).Append(')');
            return;
        }

        var ordinalText = ordinal.ToString(CultureInfo.InvariantCulture);
        if (column.IsNullable)
        {
            _ = builder
                .Append(locals.Reader)
                .Append(".IsDBNull(")
                .Append(ordinalText)
                .Append(") ? default(")
                .Append(column.TypeText)
                .Append("?) : ");
        }

        if (read is { IsByElement: true })
        {
            AppendByElement(builder, column, read, ordinalText, locals);
            return;
        }

        if (read is { IsSpatial: true })
        {
            AppendSpatial(builder, column, read, ordinalText, locals);
            return;
        }

        if (read is not null)
        {
            _ = builder.Append(read.Method).Append('(');
        }

        AppendGetFieldValue(builder, column, ordinalText, locals);
        if (read is not null)
        {
            _ = builder.Append(')');
        }
    }

    // Array.ConvertAll of the database's array, by a static lambda that converts each element that is not null.  A
    // NULL element of a value type throws, naming the column, which Npgsql's throw does not; one of a reference type,
    // or of an element the property writes nullable, stays null.
    private static void AppendByElement(
        StringBuilder builder,
        MethodColumn column,
        Conversion read,
        string ordinalText,
        QueryMethodLocals locals
    )
    {
        var names = locals.ConversionNames;
        _ = builder
            .Append("global::System.Array.ConvertAll<")
            .Append(read.ReadElementText)
            .Append(", ")
            .Append(read.ElementText)
            .Append(">(");
        AppendGetFieldValue(builder, column, ordinalText, locals);
        _ = builder
            .Append(", static ")
            .Append(names.Element)
            .Append(" => ")
            .Append(names.Element)
            .Append(" is { } ")
            .Append(names.Item)
            .Append(" ? ")
            .Append(read.Method)
            .Append('(')
            .Append(names.Item)
            .Append(") : ");
        if (read.ElementIsNullable)
        {
            _ = builder.Append("null");
        }
        else if (!read.ElementIsValueType)
        {
            _ = builder.Append("null!");
        }
        else
        {
            _ = builder
                .Append("throw new global::System.InvalidCastException(")
                .Append(SymbolDisplay.FormatLiteral("Column '" + column.Name + "' holds a null element.", quote: true))
                .Append(')');
        }

        _ = builder.Append(')');
    }

    // The shared reader's geometry of the column's bytes, read through GetStream, which every DbDataReader has, cast to
    // the property's type unless it is Geometry itself.
    private static void AppendSpatial(
        StringBuilder builder,
        MethodColumn column,
        Conversion read,
        string ordinalText,
        QueryMethodLocals locals
    )
    {
        if (column.TypeText != NetTopologySuiteConversions.Geometry)
        {
            _ = builder.Append('(').Append(column.TypeText).Append(')');
        }

        _ = builder
            .Append(read.Method)
            .Append(".Read(")
            .Append(locals.Reader)
            .Append(".GetStream(")
            .Append(ordinalText)
            .Append("))");
    }

    private static void AppendGetFieldValue(
        StringBuilder builder,
        MethodColumn column,
        string ordinalText,
        QueryMethodLocals locals
    ) =>
        _ = builder
            .Append(locals.Reader)
            .Append(".GetFieldValue<")
            .Append(column.ReadText)
            .Append(">(")
            .Append(ordinalText)
            .Append(')');
}
