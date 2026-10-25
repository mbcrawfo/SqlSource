using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

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
    public abstract void Append(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound
    );

    /// <summary>
    /// Appends the binding by value alone: the name, without the <c>@</c>, and the value, with nothing else set.  A
    /// nullable property, and any property of a reference type, which a caller can set to null all the same, sends
    /// <c>DBNull.Value</c> for null.
    /// </summary>
    protected static void AppendByValue(
        StringBuilder builder,
        string indent,
        string command,
        string parameter,
        string parameters,
        MethodParameter bound
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
        if (bound.IsNullable || !bound.IsValueType)
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
        MethodParameter bound
    ) => AppendByValue(builder, indent, command, parameter, parameters, bound);
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
        MethodParameter bound
    ) => AppendByValue(builder, indent, command, parameter, parameters, bound);
}
