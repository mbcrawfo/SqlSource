using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Conversion = SqlSource.Generation.Methods.Conversion;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the binding of one parameter of a query's method: the lines that create a parameter of the command, set it
/// and add it.  Each engine has its own, chosen by the engine of the query's file.  A new engine adds a subclass and
/// its arm in <see cref="For" />; see <c>docs/architecture/generator.md</c>, "Adding a database engine".
/// </summary>
internal abstract class ParameterBinder
{
    /// <summary>The call that copies a sequence into a new array, before its argument.</summary>
    protected const string ToArray = "global::System.Linq.Enumerable.ToArray(";

    private static readonly ParameterBinder Postgres = new PostgresParameterBinder();

    private static readonly ParameterBinder SqlServer = new SqlServerParameterBinder();

    /// <summary>
    /// The binder of an engine.
    /// </summary>
    public static ParameterBinder For(DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => Postgres,
            DatabaseEngine.SqlServer => SqlServer,
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    /// <summary>
    /// Appends the lines that bind the parameter.
    /// </summary>
    /// <param name="context">What every parameter of the method is bound with.</param>
    /// <param name="bound">The parameter, and the property that gives its value.</param>
    public abstract void Append(in BindingContext context, MethodParameter bound);

    /// <summary>
    /// Appends the binding by value alone: the name, without the <c>@</c>, and the value, with nothing else set.  A
    /// nullable property, and any property of a reference type, which a caller can set to null all the same, sends
    /// <c>DBNull.Value</c> for null.  A property with a converter sends the converter's result, and the converter
    /// never sees a null; an array converted by element sends the array of each element's result.
    /// </summary>
    protected static void AppendByValue(in BindingContext context, MethodParameter bound)
    {
        var builder = context.Builder;
        _ = builder
            .Append(context.Indent)
            .Append("var ")
            .Append(context.Parameter)
            .Append(" = ")
            .Append(context.Command)
            .Append(".CreateParameter();\n")
            .Append(context.Indent)
            .Append(context.Parameter)
            .Append(".ParameterName = ")
            .Append(SymbolDisplay.FormatLiteral(bound.Name, quote: true))
            .Append(";\n")
            .Append(context.Indent)
            .Append(context.Parameter)
            .Append(".Value = ");
        AppendValueOrNull(context, bound, "global::System.DBNull.Value");
        _ = builder
            .Append(";\n")
            .Append(context.Indent)
            .Append(context.Command)
            .Append(".Parameters.Add(")
            .Append(context.Parameter)
            .Append(");\n");
    }

    /// <summary>
    /// Appends the value that the parameter sends, the expression after <c>Value = </c>: the property's value, or the
    /// converter's result, with <paramref name="nullText" /> for a null of a property that can be one.
    /// </summary>
    protected static void AppendValueOrNull(in BindingContext context, MethodParameter bound, string nullText)
    {
        var builder = context.Builder;
        if (bound.Write is { } write)
        {
            AppendConverted(context, bound, write, "(object)", nullText);
        }
        else if (bound.IsNullable || !bound.IsValueType)
        {
            _ = builder
                .Append("(object?)")
                .Append(context.Parameters)
                .Append('.')
                .Append(bound.Property)
                .Append(" ?? ")
                .Append(nullText);
        }
        else
        {
            _ = builder.Append(context.Parameters).Append('.').Append(bound.Property);
        }
    }

    /// <summary>
    /// Appends the converter's result for the property's value.  A value that can be null is matched first, so that
    /// the converter never sees a null, and the result, after <paramref name="cast" />, is then the arm of a
    /// conditional whose other arm is <paramref name="nullText" />.  An array converted by element, which is of a
    /// reference type and so matched too, sends the database's array.  An array that the property holds as any
    /// sequence, <see cref="MethodParameter.IsArraySequence" />, is copied into an array first when it is not one,
    /// since the converter and <c>Array.ConvertAll</c> take an array.
    /// </summary>
    /// <param name="context">What every parameter of the method is bound with.</param>
    /// <param name="bound">The parameter, and the property that gives its value.</param>
    /// <param name="write">The parameter's conversion.</param>
    /// <param name="cast">What comes before the converted value: <c>(object)</c>, or nothing.</param>
    /// <param name="nullText">The text a null sends.</param>
    protected static void AppendConverted(
        in BindingContext context,
        MethodParameter bound,
        Conversion write,
        string cast,
        string nullText
    )
    {
        var builder = context.Builder;
        if (!bound.IsNullable && bound.IsValueType)
        {
            AppendCall(builder, write, context.Parameters + "." + bound.Property);
            return;
        }

        _ = builder
            .Append(context.Parameters)
            .Append('.')
            .Append(bound.Property)
            .Append(" is { } ")
            .Append(context.Names.Value)
            .Append(" ? ")
            .Append(cast);
        var value = ArrayOf(bound, context.Names.Value);
        if (write.IsByElement)
        {
            AppendByElement(builder, write, context.Names, value);
        }
        else
        {
            AppendCall(builder, write, value);
        }

        _ = builder.Append(" : ").Append(nullText);
    }

    // The value, the pattern variable of the property's value that is not null, as an array: as it is, unless the
    // property holds the array as any sequence, which is then copied into an array when it is not one.
    private static string ArrayOf(MethodParameter bound, string value) =>
        bound.IsArraySequence ? value + " as " + bound.TypeText + " ?? " + ToArray + value + ")" : value;

    // The converter's call of the value; for the spatial read path, the shared writer's bytes of it, as SqlBytes.
    private static void AppendCall(StringBuilder builder, Conversion write, string value)
    {
        // The serializer's write is a converter's call of the value.
        var writesBytes = write.Kind switch
        {
            ConversionKind.Converter or ConversionKind.Serializer => false,
            ConversionKind.Spatial => true,
            _ => throw UnexpectedEnumValueException.Of(write.Kind),
        };
        if (writesBytes)
        {
            _ = builder
                .Append("new ")
                .Append(NetTopologySuiteConversions.SqlBytes)
                .Append('(')
                .Append(write.Method)
                .Append(".Write(")
                .Append(value)
                .Append("))");
            return;
        }

        _ = builder.Append(write.Method).Append('(').Append(value).Append(')');
    }

    /// <summary>
    /// Appends <c>Array.ConvertAll</c> of <paramref name="value" />, an array, by a static lambda that converts each
    /// element.  An element of a reference type, or one the property writes nullable, can be null, and a null is sent
    /// as a <c>NULL</c> element, the converter not called for it; the result's element,
    /// <see cref="ConvertedElementText" />, is then nullable, of any database element, unlike the element a column is
    /// read as.
    /// </summary>
    protected static void AppendByElement(StringBuilder builder, Conversion write, ConversionNames names, string value)
    {
        var canBeNull = CanElementBeNull(write);
        var element = canBeNull && !write.ElementIsNullable ? write.ElementText + "?" : write.ElementText;
        var database = ConvertedElementText(write);
        _ = builder
            .Append("global::System.Array.ConvertAll<")
            .Append(element)
            .Append(", ")
            .Append(database)
            .Append(">(")
            .Append(value)
            .Append(", static ")
            .Append(names.Element)
            .Append(" => ");
        if (!canBeNull)
        {
            _ = builder.Append(write.Method).Append('(').Append(names.Element).Append(')');
        }
        else
        {
            _ = builder
                .Append(names.Element)
                .Append(" is { } ")
                .Append(names.Item)
                .Append(" ? ")
                .Append(write.Method)
                .Append('(')
                .Append(names.Item)
                .Append(") : null");
        }

        _ = builder.Append(')');
    }

    /// <summary>
    /// The element of the array that <see cref="AppendByElement" /> gives: the database's element, with <c>?</c> when
    /// the property's element can be null.
    /// </summary>
    protected static string ConvertedElementText(Conversion write) =>
        CanElementBeNull(write) ? write.DatabaseElementText + "?" : write.DatabaseElementText!;

    private static bool CanElementBeNull(Conversion write) => write.ElementIsNullable || !write.ElementIsValueType;
}
