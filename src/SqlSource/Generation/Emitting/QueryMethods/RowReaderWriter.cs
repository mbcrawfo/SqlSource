using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Methods;
using Conversion = SqlSource.Generation.Methods.Conversion;

namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// Writes the reading of a row of a query's method from its reader: the checks of the columns read into locals first,
/// and the expression that builds the row from its columns.
/// </summary>
internal static class RowReaderWriter
{
    /// <summary>
    /// The spelling of <see cref="MethodColumn.ReadText" /> that marks a column read as SqlClient's vector, which gives
    /// <c>SqlVector&lt;float&gt;.Null</c> on a <c>NULL</c> instead of throwing: a not-null column read as exactly this
    /// text is read into a local and refused when it <c>IsNull</c>, before a converter sees it.
    /// </summary>
    internal const string VectorType = "global::Microsoft.Data.SqlTypes.SqlVector<float>";

    /// <summary>
    /// A not-null column read as <c>SqlVector&lt;float&gt;</c> is read into a local first, and its <c>Null</c> is
    /// refused as a <c>NULL</c> of any other not-null column is.
    /// </summary>
    public static void AppendVectorChecks(in QueryMethodContext context, string indent, MethodOutput output)
    {
        var builder = context.Builder;
        var locals = context.Locals;
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
    public static void AppendRow(in QueryMethodContext context, MethodOutput output)
    {
        var builder = context.Builder;
        _ = builder.Append("new ").Append(output.TypeText);
        if (output.IsRecord)
        {
            _ = builder.Append('(');
            for (var index = 0; index < output.ConstructorOrder.Count; index++)
            {
                _ = builder.Append(index > 0 ? ", " : string.Empty);
                AppendColumn(in context, output, output.ConstructorOrder[index]);
            }

            _ = builder.Append(')');
            return;
        }

        _ = builder.Append(" { ");
        for (var index = 0; index < output.Columns.Count; index++)
        {
            _ = builder.Append(index > 0 ? ", " : string.Empty).Append(output.Columns[index].Property).Append(" = ");
            AppendColumn(in context, output, index);
        }

        _ = builder.Append(" }");
    }

    // A not-null column by GetFieldValue<T> alone; a nullable one by IsDBNull first, and never by GetFieldValue<T?>,
    // which SqlClient refuses on a NULL.  A column with a converter is read as the converter's database side and
    // passed to it, so that the converter never sees a NULL; an array converted by element is read as the database's
    // array and each of its elements passed to it; and the spatial read path reads the column's bytes.  A not-null
    // column whose read can give null for a JSON null throws on one.
    private static void AppendColumn(in QueryMethodContext context, MethodOutput output, int ordinal)
    {
        var builder = context.Builder;
        var locals = context.Locals;
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
            AppendByElement(in context, column, read, ordinalText);
            return;
        }

        if (read is null)
        {
            AppendGetFieldValue(in context, column, ordinalText);
        }
        else
        {
            // The serializer's read is a converter's call of the value; the spatial read path reads the column's bytes.
            var readsBytes = read.Kind switch
            {
                ConversionKind.Converter or ConversionKind.Serializer => false,
                ConversionKind.Spatial => true,
            };
            if (readsBytes)
            {
                AppendSpatial(in context, column, read, ordinalText);
                return;
            }

            _ = builder.Append(read.Method).Append('(');
            AppendGetFieldValue(in context, column, ordinalText);
            _ = builder.Append(')');
        }

        if (column.ThrowsOnJsonNull)
        {
            AppendJsonNullThrow(in context, column);
        }
    }

    // Array.ConvertAll of the database's array, by a static lambda that converts each element that is not null.  A
    // NULL element of a value type throws, naming the column, which Npgsql's throw does not; one of a reference type,
    // or of an element the property writes nullable, stays null.  A JSON null that the converter gives null for throws
    // too, unless the element is nullable.
    private static void AppendByElement(
        in QueryMethodContext context,
        MethodColumn column,
        Conversion read,
        string ordinalText
    )
    {
        var builder = context.Builder;
        var names = context.Locals.ConversionNames;
        _ = builder
            .Append("global::System.Array.ConvertAll<")
            .Append(read.ReadElementText)
            .Append(", ")
            .Append(read.ElementText)
            .Append(">(");
        AppendGetFieldValue(in context, column, ordinalText);
        _ = builder
            .Append(", static ")
            .Append(names.Element)
            .Append(" => ")
            .Append(names.Element)
            .Append(" is { } ")
            .Append(names.Item)
            .Append(" ? ");
        var throwsOnJsonNull = read.MayReturnNull && !read.ElementIsNullable;
        _ = builder
            .Append(throwsOnJsonNull ? "(" : string.Empty)
            .Append(read.Method)
            .Append('(')
            .Append(names.Item)
            .Append(')');
        if (throwsOnJsonNull)
        {
            AppendJsonNullThrow(in context, column);
            _ = builder.Append(')');
        }

        _ = builder.Append(" : ");
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

    // After a read that can give null for a JSON null, which a not-null property or element refuses, naming the
    // column.
    private static void AppendJsonNullThrow(in QueryMethodContext context, MethodColumn column) =>
        _ = context
            .Builder.Append(" ?? throw new global::System.InvalidCastException(")
            .Append(SymbolDisplay.FormatLiteral("Column '" + column.Name + "' holds a JSON null.", quote: true))
            .Append(')');

    // The shared reader's geometry of the column's bytes, read through GetStream, which every DbDataReader has, cast to
    // the property's type unless it is Geometry itself.
    private static void AppendSpatial(
        in QueryMethodContext context,
        MethodColumn column,
        Conversion read,
        string ordinalText
    )
    {
        var builder = context.Builder;
        if (column.TypeText != NetTopologySuiteConversions.Geometry)
        {
            _ = builder.Append('(').Append(column.TypeText).Append(')');
        }

        _ = builder
            .Append(read.Method)
            .Append(".Read(")
            .Append(context.Locals.Reader)
            .Append(".GetStream(")
            .Append(ordinalText)
            .Append("))");
    }

    private static void AppendGetFieldValue(in QueryMethodContext context, MethodColumn column, string ordinalText) =>
        _ = context
            .Builder.Append(context.Locals.Reader)
            .Append(".GetFieldValue<")
            .Append(column.ReadText)
            .Append(">(")
            .Append(ordinalText)
            .Append(')');
}
