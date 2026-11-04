using System.Collections.Immutable;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;

namespace SqlSource.Generation.Methods;

/// <summary>
/// What a call sees of one generated extension method, and where it is: what the methods of two types are compared by.
/// A method on the type or in a class nested in it clashes with no other type's.
/// </summary>
/// <param name="TypeKey">The type, as <see cref="TypeMethods.TypeKey" /> gives it.</param>
/// <param name="TypeName">The type in full, as a diagnostic names it: <c>App.Sample&lt;T&gt;</c>.</param>
/// <param name="Namespace">The type's namespace as a value.  Empty for the global namespace.</param>
/// <param name="ExtensionClass">The extension class that holds the method.</param>
/// <param name="IsPublic">
/// Whether the extension class is public for the type, as <see cref="TypeMethods.ExtensionClassIsPublic" /> says.
/// </param>
/// <param name="Name">The method's name.</param>
/// <param name="QueryName">The query's name.</param>
/// <param name="ParameterKey">
/// The types of the arguments a call must give, beyond the receiver, since an argument with a default cannot tell two
/// overloads apart: the input model's type, or empty, then <c>,string</c> for each token.
/// </param>
/// <param name="NameLocation">Where the query's name is.</param>
/// <param name="AttributeLocation">Where the type's attribute is.</param>
internal sealed record MethodSignature(
    string TypeKey,
    string TypeName,
    string Namespace,
    string ExtensionClass,
    bool IsPublic,
    string Name,
    string QueryName,
    string ParameterKey,
    LocationInfo NameLocation,
    LocationInfo AttributeLocation
)
{
    /// <summary>
    /// The extension class's full name, from identifier values, as <c>App.SampleExtensions</c>.
    /// </summary>
    public string ExtensionClassFullName => Namespace.Length == 0 ? ExtensionClass : Namespace + "." + ExtensionClass;

    /// <summary>The signature of each of the type's extension methods, in their order.</summary>
    public static EquatableArray<MethodSignature> Of(TypeMethods methods)
    {
        var count = 0;
        foreach (var method in methods.Methods)
        {
            count += method.Host.IsExtension ? 1 : 0;
        }

        if (count == 0)
        {
            return EquatableArray<MethodSignature>.Empty;
        }

        var typeName = methods.DisplayName;
        var signatures = ImmutableArray.CreateBuilder<MethodSignature>(count);
        foreach (var method in methods.Methods)
        {
            if (!method.Host.IsExtension)
            {
                continue;
            }

            signatures.Add(
                new MethodSignature(
                    methods.TypeKey,
                    typeName,
                    methods.Namespace,
                    methods.ExtensionClass,
                    methods.ExtensionClassIsPublic,
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

    // The input's type, then a comma and the type of each other argument that a call must give, beyond the receiver.
    // A string is built only when there is such an argument.
    private static string ParameterKeyOf(QueryMethod method)
    {
        var key = string.Empty;
        StringBuilder? builder = null;
        foreach (var argument in MethodArguments.Of(method, MethodWriter.GetTokens(method.Segments)))
        {
            if (argument.Role == ArgumentRole.Receiver || argument.DefaultText is not null)
            {
                continue;
            }

            if (argument.Role == ArgumentRole.Input)
            {
                key = argument.TypeText;
                continue;
            }

            builder ??= new StringBuilder(key);
            _ = builder.Append(',').Append(argument.TypeText);
        }

        return builder?.ToString() ?? key;
    }
}
