using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the file of one type's methods: a partial declaration of the type with the methods on it and those of the
/// classes nested in it, then the extension class beside the type.
/// </summary>
internal static class MethodEmitter
{
    // A method calls a converter, and names a property type, that can be marked [Obsolete] or [Experimental], which is
    // what was asked for; the compiler's warning, or the error of [Experimental], would be in code the consumer cannot
    // change.
    private const string PragmaStart = "#pragma warning disable ";

    private const string PragmaEnd = " // A converter or a mapped type is obsolete or experimental.\n";

    /// <summary>
    /// Writes the file of <paramref name="methods" />, or no source when no method is left, and whether a method it
    /// keeps reads its rows into the project's <see cref="RowBuffer" />.
    /// </summary>
    /// <param name="methods">The type's methods.</param>
    /// <param name="dropped">The <see cref="QueryMethod.Name" />s of the extension methods to leave out.</param>
    /// <param name="extensionClassBlocked">
    /// Whether the extension class cannot be written, which leaves out every method under
    /// <see cref="MemberLocation.ExtensionClass" />.
    /// </param>
    public static (string HintName, string? Source, bool UsesRowBuffer) Emit(
        TypeMethods methods,
        EquatableArray<string> dropped,
        bool extensionClassBlocked
    )
    {
        var (onType, extensions) = Keep(methods.Methods, dropped, extensionClassBlocked);
        var writesType = onType.Count > 0;
        if (!writesType && extensions.Count == 0)
        {
            return (methods.HintName, null, false);
        }

        var builder = new StringBuilder(writesType ? TypeEmitter.HeaderLines : GeneratedText.Header);
        if (writesType && methods.DisablesProtectedWarning)
        {
            _ = builder.Append(TypeEmitter.ProtectedPragma);
        }

        if (methods.WarningIds.Count > 0)
        {
            _ = builder.Append(PragmaStart).Append(string.Join(", ", methods.WarningIds)).Append(PragmaEnd);
        }

        _ = builder.Append('\n');
        var depth = 0;
        if (methods.NamespaceText.Length > 0)
        {
            TypeEmitter.Open(builder, depth++, "namespace " + methods.NamespaceText);
        }

        var usesRowBuffer = false;
        if (writesType)
        {
            usesRowBuffer |= AppendOnType(builder, depth, methods, onType);
        }

        if (extensions.Count > 0)
        {
            if (writesType)
            {
                _ = builder.Append('\n');
            }

            usesRowBuffer |= AppendExtensionClass(builder, depth, methods, extensions);
        }

        while (depth > 0)
        {
            _ = builder.Append(TypeEmitter.GetIndent(--depth)).Append("}\n");
        }

        return (methods.HintName, builder.ToString(), usesRowBuffer);
    }

    // The partial declaration of the type, with its own methods and then each nested class's, as a static partial
    // class with the accessibility that the type's file gives it too.
    private static bool AppendOnType(StringBuilder builder, int depth, TypeMethods methods, List<QueryMethod> onType)
    {
        var start = depth;
        foreach (var declaration in methods.Types)
        {
            TypeEmitter.Open(
                builder,
                depth++,
                "partial " + declaration.Keyword + " " + declaration.Name + declaration.TypeParameters
            );
        }

        var usesRowBuffer = false;
        var isFirst = true;
        foreach (var method in onType)
        {
            if (method.Host.Location == MemberLocation.Class)
            {
                usesRowBuffer |= AppendMethod(builder, depth, method, ref isFirst);
            }
        }

        foreach (var nestedClass in methods.NestedClasses)
        {
            var isFirstInClass = true;
            foreach (var method in onType)
            {
                if (
                    method.Host.Location != MemberLocation.NestedClass
                    || !string.Equals(method.Host.NestedClass, nestedClass.Name, System.StringComparison.Ordinal)
                )
                {
                    continue;
                }

                if (isFirstInClass)
                {
                    TypeEmitter.Separate(builder, ref isFirst);
                    TypeEmitter.OpenNestedClass(builder, depth, nestedClass, methods.DisplayName);
                }

                usesRowBuffer |= AppendMethod(builder, depth + 1, method, ref isFirstInClass);
            }

            if (!isFirstInClass)
            {
                _ = builder.Append(TypeEmitter.GetIndent(depth)).Append("}\n");
            }
        }

        while (depth > start)
        {
            _ = builder.Append(TypeEmitter.GetIndent(--depth)).Append("}\n");
        }

        return usesRowBuffer;
    }

    // The extension class, which takes the widest accessibility of the type's extension methods.
    private static bool AppendExtensionClass(
        StringBuilder builder,
        int depth,
        TypeMethods methods,
        List<QueryMethod> extensions
    )
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
            depth,
            (methods.ExtensionClassIsPublic ? "public" : "internal") + " static partial class " + methods.ExtensionClass
        );
        var usesRowBuffer = false;
        var isFirst = true;
        foreach (var method in extensions)
        {
            usesRowBuffer |= AppendMethod(builder, depth + 1, method, ref isFirst);
        }

        _ = builder.Append(indent).Append("}\n");
        return usesRowBuffer;
    }

    private static bool AppendMethod(StringBuilder builder, int depth, QueryMethod method, ref bool isFirst)
    {
        TypeEmitter.Separate(builder, ref isFirst);
        QueryMethodWriter.Append(builder, TypeEmitter.GetIndent(depth), method);
        return RowBuffer.IsUsedBy(method);
    }

    // The methods on the type and in its nested classes, all kept, and the extension methods that are not dropped,
    // none when the extension class is blocked.  The list of the dropped is almost always empty, and holds a few
    // clashes when it is not.
    private static (List<QueryMethod> OnType, List<QueryMethod> Extensions) Keep(
        EquatableArray<QueryMethod> methods,
        EquatableArray<string> dropped,
        bool extensionClassBlocked
    )
    {
        var onType = new List<QueryMethod>();
        var extensions = new List<QueryMethod>();
        foreach (var method in methods)
        {
            if (!method.Host.IsExtension)
            {
                onType.Add(method);
            }
            else if (!extensionClassBlocked && !dropped.Contains(method.Name))
            {
                extensions.Add(method);
            }
        }

        return (onType, extensions);
    }
}
