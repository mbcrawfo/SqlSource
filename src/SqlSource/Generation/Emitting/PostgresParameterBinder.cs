using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Generation.Methods;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Binds a parameter for Npgsql: a new <c>NpgsqlParameter&lt;T&gt;</c> of the type of the value bound, its
/// <c>DataTypeName</c> the canonical name of the type the entry describes the parameter by, and its <c>TypedValue</c>
/// the value, which sets a value type's without boxing it.
/// </summary>
/// <remarks>
/// <c>T</c> is the property's type, with <c>?</c> for a nullable value type; for a converted property, the database
/// side of its converter, with <c>?</c> when it is a value type and the property can be null; and for an array
/// converted by element, the array <c>Array.ConvertAll</c> gives.  A null is bound as <c>TypedValue = null</c>, never
/// <c>DBNull.Value</c>, which the setter of <c>NpgsqlParameter&lt;T&gt;.Value</c> refuses.  No <c>NpgsqlDbType</c> is
/// named, so the binding compiles and binds alike on every supported Npgsql.
/// <para>
/// The name is the type's canonical internal name, <c>schema.internalName</c> unquoted, as <c>pg_catalog.int4</c>,
/// since Npgsql resolves a display name such as <c>integer</c> with more work on every new parameter.  Npgsql refuses a
/// domain over a domain by its own name, so such a domain is bound by its innermost base's.  The <c>bindName</c> is
/// written as the entry holds it where no canonical name can be: for an array of a domain over a domain, which Npgsql 8
/// refuses by its own name, for a kind this version does not know, and for a schema or a name that holds a dot.
/// </para>
/// </remarks>
internal sealed class PostgresParameterBinder : ParameterBinder
{
    /// <inheritdoc />
    public override void Append(in BindingContext context, MethodParameter bound)
    {
        var builder = context.Builder;
        _ = builder
            .Append(context.Indent)
            .Append("var ")
            .Append(context.Parameter)
            .Append(" = new global::Npgsql.NpgsqlParameter<");
        AppendBoundType(builder, bound);
        _ = builder
            .Append(">();\n")
            .Append(context.Indent)
            .Append(context.Parameter)
            .Append(".ParameterName = ")
            .Append(SymbolDisplay.FormatLiteral(bound.Name, quote: true))
            .Append(";\n");
        if (DataTypeNameOf(bound.Described) is { } dataTypeName)
        {
            _ = builder
                .Append(context.Indent)
                .Append(context.Parameter)
                .Append(".DataTypeName = ")
                .Append(SymbolDisplay.FormatLiteral(dataTypeName, quote: true))
                .Append(";\n");
        }

        _ = builder.Append(context.Indent).Append(context.Parameter).Append(".TypedValue = ");
        AppendValue(context, bound);
        _ = builder
            .Append(";\n")
            .Append(context.Indent)
            .Append(context.Command)
            .Append(".Parameters.Add(")
            .Append(context.Parameter)
            .Append(");\n");
    }

    /// <summary>
    /// The name the parameter of a type is bound by: null when the type has no <c>bindName</c>, and otherwise the
    /// type's canonical name, or its <c>bindName</c> where none can be derived.
    /// </summary>
    internal static string? DataTypeNameOf(SidecarType? described)
    {
        if (described?.BindName is not { } bindName)
        {
            return null;
        }

        if (described is not PostgresType type || !type.TryGetKind(out var kind))
        {
            return bindName;
        }

        if (kind == PostgresTypeKind.Array)
        {
            return IsDomainOverADomain(type.Element) ? bindName : CanonicalName(type) ?? bindName;
        }

        if (IsDomainOverADomain(type))
        {
            var innermost = type.Base;
            while (innermost is not null && IsDomain(innermost))
            {
                innermost = innermost.Base;
            }

            return innermost is null ? bindName : CanonicalName(innermost) ?? bindName;
        }

        return CanonicalName(type) ?? bindName;
    }

    // Whether the type is a domain whose base is a domain.
    private static bool IsDomainOverADomain(PostgresType? type) =>
        type is not null && IsDomain(type) && IsDomain(type.Base);

    private static bool IsDomain(PostgresType? type) =>
        type is not null && type.TryGetKind(out var kind) && kind == PostgresTypeKind.Domain;

    // schema.internalName, or null when either holds a dot, which would make the name another type's.
    private static string? CanonicalName(PostgresType type) =>
        type.Schema.IndexOf('.') >= 0 || type.InternalName.IndexOf('.') >= 0
            ? null
            : type.Schema + "." + type.InternalName;

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

    // The property's value, or its converter's result, by a conditional typed by TypedValue, whose null is the null
    // of T.
    private static void AppendValue(in BindingContext context, MethodParameter bound)
    {
        if (bound.Write is { } write)
        {
            AppendConverted(context, bound, write, "", "null");
        }
        else
        {
            _ = context.Builder.Append(context.Parameters).Append('.').Append(bound.Property);
        }
    }
}
