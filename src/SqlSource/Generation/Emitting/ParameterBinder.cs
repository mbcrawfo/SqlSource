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
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line.</param>
    /// <param name="command">The name of the command's local.</param>
    /// <param name="parameter">The name of the parameter's local.</param>
    /// <param name="parameters">The name of the argument that holds the input model.</param>
    /// <param name="bound">The parameter, and the property that gives its value.</param>
    /// <param name="value">
    /// The name of the pattern variable that holds the value of a converted parameter, which no argument and no local
    /// of the method has; empty when the method has no converted parameter.
    /// </param>
    public abstract void Append(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound,
        string value
    );

    /// <summary>
    /// Appends the binding by value alone: the name, without the <c>@</c>, and the value, with nothing else set.  A
    /// nullable property, and any property of a reference type, which a caller can set to null all the same, sends
    /// <c>DBNull.Value</c> for null.  A property with a converter sends the converter's result, and the converter
    /// never sees a null.
    /// </summary>
    protected static void AppendByValue(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound,
        string value
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
        if (bound.Write is { IsByElement: false } write)
        {
            AppendConverted(builder, parameters, bound, write, value);
        }
        else if (bound.IsNullable || !bound.IsValueType)
        {
            _ = builder
                .Append("(object?)")
                .Append(parameters)
                .Append('.')
                .Append(bound.Property)
                .Append(" ?? global::System.DBNull.Value");
        }
        else
        {
            _ = builder.Append(parameters).Append('.').Append(bound.Property);
        }

        _ = builder
            .Append(";\n")
            .Append(indent)
            .Append(command)
            .Append(".Parameters.Add(")
            .Append(parameter)
            .Append(");\n");
    }

    // The converter of the property's value; a value that can be null is matched first, and null sends DBNull.Value.
    // A conversion by element is not written yet, and such a parameter is bound as without a converter.
    private static void AppendConverted(
        StringBuilder builder,
        string parameters,
        MethodParameter bound,
        Conversion write,
        string value
    )
    {
        if (bound.IsNullable || !bound.IsValueType)
        {
            _ = builder
                .Append(parameters)
                .Append('.')
                .Append(bound.Property)
                .Append(" is { } ")
                .Append(value)
                .Append(" ? (object)")
                .Append(write.Method)
                .Append('(')
                .Append(value)
                .Append(") : global::System.DBNull.Value");
            return;
        }

        _ = builder.Append(write.Method).Append('(').Append(parameters).Append('.').Append(bound.Property).Append(')');
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
        string value
    ) => AppendByValue(builder, indent, command, parameter, parameters, bound, value);
}

/// <summary>
/// Binds a parameter for Microsoft.Data.SqlClient.
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
        string value
    ) => AppendByValue(builder, indent, command, parameter, parameters, bound, value);
}
