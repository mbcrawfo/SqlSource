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
/// converted by element, the array <c>Array.ConvertAll</c> gives.  An array that the property holds as any sequence,
/// <see cref="MethodParameter.IsArraySequence" />, is bound as an <c>IList&lt;E&gt;</c> of its element, or of the
/// database's element when it is converted by element, since Npgsql sends an array from an <c>E[]</c> or an
/// <c>IList&lt;E&gt;</c> alone and finds how from <c>T</c>, not from the value: a value that is an
/// <c>IList&lt;E&gt;</c> is bound as it is, and any other sequence is copied once into an array.  A converter of the
/// whole array is bound as its result is.  A null is bound as <c>TypedValue = null</c>, never
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
    private const string IList = "global::System.Collections.Generic.IList<";

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
            .Append(context.NameOf(bound))
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

    /// <inheritdoc />
    public override void AppendNullTest(in BindingContext context, MethodParameter list) =>
        _ = context
            .Builder.Append(context.Indent)
            .Append("var ")
            .Append(context.Parameter)
            .Append(" = new global::Npgsql.NpgsqlParameter<bool?>();\n")
            .Append(context.Indent)
            .Append(context.Parameter)
            .Append(".ParameterName = ")
            .Append(context.NameOf(list))
            .Append(";\n")
            .Append(context.Indent)
            .Append(context.Parameter)
            .Append(".DataTypeName = \"pg_catalog.bool\";\n")
            .Append(context.Indent)
            .Append(context.Parameter)
            .Append(".TypedValue = ")
            .Append(context.ValueOf(list))
            .Append(" is null ? null : true;\n")
            .Append(context.Indent)
            .Append(context.Command)
            .Append(".Parameters.Add(")
            .Append(context.Parameter)
            .Append(");\n");

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
            case null when bound.IsArraySequence:
                _ = builder.Append(IList).Append(ConversionPlanner.ElementOf(bound.TypeText)).Append('>');
                break;
            case null:
                _ = builder.Append(bound.TypeText).Append(bound.IsNullable && bound.IsValueType ? "?" : "");
                break;
            case { IsByElement: false } write:
                var canBeNull = bound.IsNullable || !bound.IsValueType;
                _ = builder.Append(write.DatabaseText).Append(write.DatabaseIsValueType && canBeNull ? "?" : "");
                break;
            case var write when bound.IsArraySequence:
                _ = builder.Append(IList).Append(ConvertedElementText(write)).Append('>');
                break;
            case var write:
                _ = builder.Append(ConvertedElementText(write)).Append("[]");
                break;
        }
    }

    // The property's value, or its converter's result, by a conditional typed by TypedValue, whose null is the null
    // of T.  A sequence that is no IList of its element is copied into an array.
    private static void AppendValue(in BindingContext context, MethodParameter bound)
    {
        var builder = context.Builder;
        if (bound.Write is { } write)
        {
            AppendConverted(context, bound, write, "", "null");
        }
        else if (bound.IsArraySequence)
        {
            var value = context.Names.Value;
            AppendMatch(context, bound);
            _ = builder
                .Append(value)
                .Append(" as ")
                .Append(IList)
                .Append(ConversionPlanner.ElementOf(bound.TypeText))
                .Append("> ?? ")
                .Append(ToArray)
                .Append(value)
                .Append(") : null");
        }
        else
        {
            _ = builder.Append(context.ValueOf(bound));
        }
    }
}
