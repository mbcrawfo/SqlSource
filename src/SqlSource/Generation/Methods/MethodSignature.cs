using System.Collections.Immutable;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// What a call sees of one generated method, and where it is: what the methods of two types are compared by.
/// </summary>
/// <param name="TypeKey">The type, as <see cref="TypeMethods.TypeKey" /> gives it.</param>
/// <param name="TypeName">The type in full, as a diagnostic names it: <c>App.Sample&lt;T&gt;</c>.</param>
/// <param name="Namespace">The type's namespace as a value.  Empty for the global namespace.</param>
/// <param name="ExtensionClass">
/// The class that holds the method under <see cref="MethodPlacement.ExtensionClass" />, and null when the method is on
/// the type.
/// </param>
/// <param name="IsPublic">Whether the extension class is public.</param>
/// <param name="Name">The method's name.</param>
/// <param name="QueryName">The query's name.</param>
/// <param name="ParameterKey">
/// The arguments a call gives beside those every method has: the input model's type, or empty, then <c>,string</c>
/// for each token.
/// </param>
/// <param name="NameLocation">Where the query's name is.</param>
/// <param name="AttributeLocation">Where the type's attribute is.</param>
internal sealed record MethodSignature(
    string TypeKey,
    string TypeName,
    string Namespace,
    string? ExtensionClass,
    bool IsPublic,
    string Name,
    string QueryName,
    string ParameterKey,
    LocationInfo NameLocation,
    LocationInfo AttributeLocation
)
{
    /// <summary>
    /// The extension class's full name, from identifier values, as <c>App.SampleExtensions</c>, or null when the method
    /// is on the type.
    /// </summary>
    public string? ExtensionClassFullName
    {
        get
        {
            if (ExtensionClass is null || Namespace.Length == 0)
            {
                return ExtensionClass;
            }

            return Namespace + "." + ExtensionClass;
        }
    }

    /// <summary>The signature of each of the type's methods, in their order.</summary>
    public static EquatableArray<MethodSignature> Of(TypeMethods methods)
    {
        if (methods.Methods.Count == 0)
        {
            return EquatableArray<MethodSignature>.Empty;
        }

        var extensionClass = methods.Placement == MethodPlacement.ExtensionClass ? methods.ExtensionClass : null;
        var typeName = methods.DisplayName;
        var signatures = ImmutableArray.CreateBuilder<MethodSignature>(methods.Methods.Count);
        foreach (var method in methods.Methods)
        {
            signatures.Add(
                new MethodSignature(
                    methods.TypeKey,
                    typeName,
                    methods.Namespace,
                    extensionClass,
                    methods.IsPublic,
                    method.Name,
                    method.QueryName,
                    ParameterKeyOf(method),
                    method.NameLocation,
                    methods.AttributeLocation
                )
            );
        }

        return new EquatableArray<MethodSignature>(signatures.MoveToImmutable());
    }

    private static string ParameterKeyOf(QueryMethod method)
    {
        var input = method.Input?.TypeText ?? string.Empty;
        var tokens = MethodWriter.GetTokens(method.Segments).Count;
        if (tokens == 0)
        {
            return input;
        }

        var key = new StringBuilder(input);
        for (var token = 0; token < tokens; token++)
        {
            _ = key.Append(",string");
        }

        return key.ToString();
    }
}
