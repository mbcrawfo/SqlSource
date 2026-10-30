using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using Conversion = SqlSource.Generation.Methods.Conversion;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the binding of one parameter of a query's method: the lines that create a parameter of the command, set it
/// and add it.  Each engine has its own, chosen by the engine of the query's file.
/// </summary>
internal abstract class ParameterBinder
{
    private static readonly ParameterBinder Postgres = new PostgresParameterBinder();

    private static readonly ParameterBinder SqlServer = new SqlServerParameterBinder();

    /// <summary>
    /// The binder of an engine.
    /// </summary>
    public static ParameterBinder For(DatabaseEngine engine) =>
        engine == DatabaseEngine.SqlServer ? SqlServer : Postgres;

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
    /// reference type and so matched too, sends the database's array.
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
            _ = builder
                .Append(write.Method)
                .Append('(')
                .Append(context.Parameters)
                .Append('.')
                .Append(bound.Property)
                .Append(')');
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
        if (write.IsByElement)
        {
            AppendByElement(builder, write, context.Names);
        }
        else
        {
            _ = builder.Append(write.Method).Append('(').Append(context.Names.Value).Append(')');
        }

        _ = builder.Append(" : ").Append(nullText);
    }

    /// <summary>
    /// Appends <c>Array.ConvertAll</c> of the value, by a static lambda that converts each element.  An element of a
    /// reference type, or one the property writes nullable, can be null, and a null is sent as a <c>NULL</c> element,
    /// the converter not called for it; the result's element, <see cref="ConvertedElementText" />, is then nullable,
    /// of any database element, unlike the element a column is read as.
    /// </summary>
    protected static void AppendByElement(StringBuilder builder, Conversion write, ConversionNames names)
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
            .Append(names.Value)
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
