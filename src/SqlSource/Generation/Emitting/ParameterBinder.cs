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
    /// What a null of a parameter sends, but for a SQL Server vector's.
    /// </summary>
    internal const string DbNull = "global::System.DBNull.Value";

    /// <summary>
    /// The binder of an engine.
    /// </summary>
    public static ParameterBinder For(DatabaseEngine engine) =>
        engine == DatabaseEngine.SqlServer ? SqlServer : Postgres;

    /// <summary>
    /// Appends the lines that bind the parameter.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line.</param>
    /// <param name="command">The name of the command's local.</param>
    /// <param name="parameter">The name of the parameter's local.</param>
    /// <param name="parameters">The name of the argument that holds the input model.</param>
    /// <param name="bound">The parameter, and the property that gives its value.</param>
    /// <param name="names">
    /// The names of what a converted parameter's binding declares, which no argument and no local of the method has.
    /// </param>
    public abstract void Append(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound,
        ConversionNames names
    );

    /// <summary>
    /// Appends the binding by value alone: the name, without the <c>@</c>, and the value, with nothing else set.  A
    /// nullable property, and any property of a reference type, which a caller can set to null all the same, sends
    /// <c>DBNull.Value</c> for null.  A property with a converter sends the converter's result, and the converter
    /// never sees a null; an array converted by element sends the array of each element's result.
    /// </summary>
    internal static void AppendByValue(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound,
        ConversionNames names
    )
    {
        _ = builder
            .Append(indent)
            .Append("var ")
            .Append(parameter)
            .Append(" = ")
            .Append(command)
            .Append(".CreateParameter();\n")
            .Append(indent)
            .Append(parameter)
            .Append(".ParameterName = ")
            .Append(SymbolDisplay.FormatLiteral(bound.Name, quote: true))
            .Append(";\n")
            .Append(indent)
            .Append(parameter)
            .Append(".Value = ");
        AppendValue(builder, parameters, bound, names, DbNull);
        _ = builder
            .Append(";\n")
            .Append(indent)
            .Append(command)
            .Append(".Parameters.Add(")
            .Append(parameter)
            .Append(");\n");
    }

    /// <summary>
    /// Appends the value that the parameter sends, the expression after <c>Value = </c>: the property's value, or the
    /// converter's result, with <paramref name="nullText" /> for a null of a property that can be one.
    /// </summary>
    internal static void AppendValue(
        StringBuilder builder,
        string parameters,
        MethodParameter bound,
        ConversionNames names,
        string nullText
    )
    {
        if (bound.Write is { } write)
        {
            AppendConverted(builder, parameters, bound, write, names, nullText);
        }
        else if (bound.IsNullable || !bound.IsValueType)
        {
            _ = builder
                .Append("(object?)")
                .Append(parameters)
                .Append('.')
                .Append(bound.Property)
                .Append(" ?? ")
                .Append(nullText);
        }
        else
        {
            _ = builder.Append(parameters).Append('.').Append(bound.Property);
        }
    }

    // The converter of the property's value; a value that can be null is matched first, and null sends the null text.
    // An array converted by element, which is of a reference type and so matched too, sends the database's array.
    private static void AppendConverted(
        StringBuilder builder,
        string parameters,
        MethodParameter bound,
        Conversion write,
        ConversionNames names,
        string nullText
    )
    {
        if (!bound.IsNullable && bound.IsValueType)
        {
            _ = builder
                .Append(write.Method)
                .Append('(')
                .Append(parameters)
                .Append('.')
                .Append(bound.Property)
                .Append(')');
            return;
        }

        _ = builder
            .Append(parameters)
            .Append('.')
            .Append(bound.Property)
            .Append(" is { } ")
            .Append(names.Value)
            .Append(" ? (object)");
        if (write.IsByElement)
        {
            AppendByElement(builder, write, names);
        }
        else
        {
            _ = builder.Append(write.Method).Append('(').Append(names.Value).Append(')');
        }

        _ = builder.Append(" : ").Append(nullText);
    }

    // Array.ConvertAll of the value, by a static lambda that converts each element.  An element of a reference type,
    // or one the property writes nullable, can be null, and a null is sent as a NULL element, the converter not called
    // for it; the result's element is then nullable, of any database element, unlike the element a column is read as.
    private static void AppendByElement(StringBuilder builder, Conversion write, ConversionNames names)
    {
        var canBeNull = write.ElementIsNullable || !write.ElementIsValueType;
        var element = canBeNull && !write.ElementIsNullable ? write.ElementText + "?" : write.ElementText;
        var database = canBeNull ? write.DatabaseElementText + "?" : write.DatabaseElementText;
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
}

/// <summary>
/// Binds a parameter for Npgsql.
/// </summary>
internal sealed class PostgresParameterBinder : ParameterBinder
{
    /// <inheritdoc />
    public override void Append(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound,
        ConversionNames names
    ) => AppendByValue(builder, indent, command, parameter, parameters, bound, names);
}

/// <summary>
/// Binds a parameter for Microsoft.Data.SqlClient, as its described type.
/// </summary>
internal sealed class SqlServerParameterBinder : ParameterBinder
{
    /// <inheritdoc />
    public override void Append(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound,
        ConversionNames names
    ) => SqlServerBinding.Append(builder, indent, command, parameter, parameters, bound, names);
}
