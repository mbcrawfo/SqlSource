using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Binds a parameter for Npgsql: a new <c>NpgsqlParameter&lt;T&gt;</c> of the type of the value bound, its
/// <c>DataTypeName</c> the <c>bindName</c> the entry describes the parameter by, and its <c>TypedValue</c> the value,
/// which sets a value type's without boxing it.
/// </summary>
/// <remarks>
/// <c>T</c> is the property's type, with <c>?</c> for a nullable value type; for a converted property, the database
/// side of its converter, with <c>?</c> when it is a value type and the property can be null; and for an array
/// converted by element, the array <c>Array.ConvertAll</c> gives.  A null is bound as <c>TypedValue = null</c>, never
/// <c>DBNull.Value</c>, which the setter of <c>NpgsqlParameter&lt;T&gt;.Value</c> refuses.  No <c>NpgsqlDbType</c> is
/// named, and a <c>bindName</c> is written as the entry holds it, so the binding compiles and binds alike on every
/// supported Npgsql.
/// </remarks>
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
    )
    {
        _ = builder.Append(indent).Append("var ").Append(parameter).Append(" = new global::Npgsql.NpgsqlParameter<");
        AppendBoundType(builder, bound);
        _ = builder
            .Append(">();\n")
            .Append(indent)
            .Append(parameter)
            .Append(".ParameterName = ")
            .Append(SymbolDisplay.FormatLiteral(bound.Name, quote: true))
            .Append(";\n");
        if (bound.Described?.BindName is { } bindName)
        {
            _ = builder
                .Append(indent)
                .Append(parameter)
                .Append(".DataTypeName = ")
                .Append(SymbolDisplay.FormatLiteral(bindName, quote: true))
                .Append(";\n");
        }

        _ = builder.Append(indent).Append(parameter).Append(".TypedValue = ");
        AppendValue(builder, parameters, bound, names);
        _ = builder
            .Append(";\n")
            .Append(indent)
            .Append(command)
            .Append(".Parameters.Add(")
            .Append(parameter)
            .Append(");\n");
    }

    // The T of the parameter: the type of the value bound.
    private static void AppendBoundType(StringBuilder builder, MethodParameter bound)
    {
        switch (bound.Write)
        {
            case null:
                _ = builder.Append(bound.TypeText).Append(bound.IsNullable && bound.IsValueType ? "?" : "");
                break;
            case { IsByElement: false } write:
                var canBeNull = bound.IsNullable || !bound.IsValueType;
                _ = builder.Append(write.DatabaseText).Append(write.DatabaseIsValueType && canBeNull ? "?" : "");
                break;
            case var write:
                _ = builder.Append(ConvertedElementText(write)).Append("[]");
                break;
        }
    }

    // The property's value, or its converter's result; a value that can be null is matched first, so that the
    // converter never sees a null, and a null is bound as the null of T, by a conditional typed by TypedValue.
    private static void AppendValue(
        StringBuilder builder,
        string parameters,
        MethodParameter bound,
        ConversionNames names
    )
    {
        if (bound.Write is not { } write)
        {
            _ = builder.Append(parameters).Append('.').Append(bound.Property);
            return;
        }

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
            .Append(" ? ");
        if (write.IsByElement)
        {
            AppendByElement(builder, write, names);
        }
        else
        {
            _ = builder.Append(write.Method).Append('(').Append(names.Value).Append(')');
        }

        _ = builder.Append(" : null");
    }
}
