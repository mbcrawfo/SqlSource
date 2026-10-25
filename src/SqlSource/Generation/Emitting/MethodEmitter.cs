using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the file of one type's methods: an extension class beside the type, or a partial declaration of the type.
/// </summary>
internal static class MethodEmitter
{
    /// <summary>
    /// Writes the file of <paramref name="methods" />, or no source when no method is left.
    /// </summary>
    /// <param name="methods">The type's methods.</param>
    /// <param name="dropped">The <see cref="QueryMethod.NameLocation" />s of the methods to leave out.</param>
    /// <param name="extensionClassBlocked">
    /// Whether the extension class cannot be written, which leaves out every method under
    /// <see cref="MethodPlacement.ExtensionClass" />.
    /// </param>
    public static (string HintName, string? Source) Emit(
        TypeMethods methods,
        EquatableArray<LocationInfo> dropped,
        bool extensionClassBlocked
    )
    {
        var isExtension = methods.Placement == MethodPlacement.ExtensionClass;
        var kept = Keep(methods.Methods, dropped, isExtension && extensionClassBlocked);
        if (kept.Count == 0)
        {
            return (methods.HintName, null);
        }

        var builder = new StringBuilder(isExtension ? GeneratedText.Header + "\n" : TypeEmitter.Header);
        var depth = 0;
        if (methods.NamespaceText.Length > 0)
        {
            TypeEmitter.Open(builder, depth++, "namespace " + methods.NamespaceText);
        }

        if (isExtension)
        {
            var indent = TypeEmitter.GetIndent(depth);
            _ = builder
                .Append(indent)
                .Append("/// <summary>\n")
                .Append(indent)
                .Append("/// The query methods of <c>")
                .Append(XmlDocWriter.Escape(methods.DisplayName))
                .Append("</c>, which extend the connection they run on.\n")
                .Append(indent)
                .Append("/// </summary>\n");
            TypeEmitter.Open(
                builder,
                depth++,
                (methods.IsPublic ? "public" : "internal") + " static partial class " + methods.ExtensionClass
            );
        }
        else
        {
            foreach (var declaration in methods.Types)
            {
                TypeEmitter.Open(
                    builder,
                    depth++,
                    "partial " + declaration.Keyword + " " + declaration.Name + declaration.TypeParameters
                );
            }
        }

        var modifiers = ModifiersOf(methods.Placement);
        var memberIndent = TypeEmitter.GetIndent(depth);
        for (var index = 0; index < kept.Count; index++)
        {
            if (index > 0)
            {
                _ = builder.Append('\n');
            }

            QueryMethodWriter.Append(builder, memberIndent, kept[index], modifiers, isExtension);
        }

        while (depth > 0)
        {
            _ = builder.Append(TypeEmitter.GetIndent(--depth)).Append("}\n");
        }

        return (methods.HintName, builder.ToString());
    }

    // The list of the dropped is almost always empty, and holds a few clashes when it is not.
    private static List<QueryMethod> Keep(
        EquatableArray<QueryMethod> methods,
        EquatableArray<LocationInfo> dropped,
        bool dropAll
    ) => dropAll ? [] : [.. methods.Where(method => !dropped.Contains(method.NameLocation))];

    private static string ModifiersOf(MethodPlacement placement) =>
        placement switch
        {
            MethodPlacement.Internal => "internal static",
            MethodPlacement.Private => "private static",
            MethodPlacement.Public or MethodPlacement.ExtensionClass or _ => "public static",
        };
}
