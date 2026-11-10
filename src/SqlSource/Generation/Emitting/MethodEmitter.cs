using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlSource.Generation.Emitting.QueryMethods;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Writes the file of one type's methods: a partial declaration of the type with the methods on it and those of the
/// classes nested in it, then the repository's interface beside the type, which that declaration implements, then the
/// extension class beside the type.
/// </summary>
internal static class MethodEmitter
{
    // A method calls a converter, and names a property type, that can be marked [Obsolete] or [Experimental], which is
    // what was asked for; the compiler's warning, or the error of [Experimental], would be in code the consumer cannot
    // change.
    private const string PragmaStart = "#pragma warning disable ";

    private const string PragmaEnd = " // A converter or a mapped type is obsolete or experimental.\n";

    /// <summary>
    /// Writes the file of <paramref name="methods" />, or no source when no method is left and the type gets no
    /// interface, and whether a method it keeps reads its rows into the project's <see cref="RowBuffer" />.
    /// </summary>
    /// <param name="methods">The type's methods.</param>
    /// <param name="dropped">The <see cref="QueryMethod.Name" />s of the extension methods to leave out.</param>
    /// <param name="extensionClassBlocked">
    /// Whether the extension class cannot be written, which leaves out every method under
    /// <see cref="MemberLocation.ExtensionClass" />.
    /// </param>
    /// <param name="interfaceBlocked">
    /// Whether the repository's interface cannot be written, since something else that SqlSource writes has its name.
    /// </param>
    public static (string HintName, string? Source, bool UsesRowBuffer) Emit(
        TypeMethods methods,
        EquatableArray<string> dropped,
        bool extensionClassBlocked,
        bool interfaceBlocked
    )
    {
        var (onType, extensions) = Keep(methods.Methods, dropped, extensionClassBlocked);
        var interfaceName = interfaceBlocked ? null : methods.InterfaceName;
        // The interface is written when every method of the repository failed, so that code that names it does not
        // fail too.
        var writesType = onType.Count > 0 || interfaceName is not null;
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
            usesRowBuffer |= AppendOnType(builder, depth, methods, interfaceName, onType);
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
    // class with the accessibility that the type's file gives it too, then the repository's interface, in the types
    // the type is nested in, which the type's declaration implements.
    private static bool AppendOnType(
        StringBuilder builder,
        int depth,
        TypeMethods methods,
        string? interfaceName,
        List<QueryMethod> onType
    )
    {
        var start = depth;
        var last = methods.Types.Count - 1;
        for (var index = 0; index <= last; index++)
        {
            var declaration = methods.Types[index];
            var header = "partial " + declaration.Keyword + " " + declaration.Name + declaration.TypeParameters;
            TypeEmitter.Open(
                builder,
                depth++,
                index == last && interfaceName is not null
                    ? header + " : " + InterfaceOf(methods, interfaceName)
                    : header
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

        _ = builder.Append(TypeEmitter.GetIndent(--depth)).Append("}\n");
        if (interfaceName is not null)
        {
            _ = builder.Append('\n');
            AppendInterface(builder, depth, methods, interfaceName, onType);
        }

        while (depth > start)
        {
            _ = builder.Append(TypeEmitter.GetIndent(--depth)).Append("}\n");
        }

        return usesRowBuffer;
    }

    // The reference to the interface from the type's base list: from global::, through the types the type is nested
    // in, with their type parameters, and with the type's own, so that it means the interface beside the type.
    private static string InterfaceOf(TypeMethods methods, string name)
    {
        var reference = new StringBuilder("global::");
        if (methods.NamespaceText.Length > 0)
        {
            _ = reference.Append(methods.NamespaceText).Append('.');
        }

        for (var index = 0; index < methods.Types.Count - 1; index++)
        {
            _ = reference.Append(methods.Types[index].Name).Append(methods.Types[index].TypeParameters).Append('.');
        }

        return reference
            .Append(CodeNameOf(name))
            .Append(methods.Types[methods.Types.Count - 1].TypeParameters)
            .ToString();
    }

    // The repository's interface: a summary that names the type, then a partial interface of the type's accessibility,
    // public or internal, and its type parameters, without their constraints, whose members are the type's public
    // repository methods.
    private static void AppendInterface(
        StringBuilder builder,
        int depth,
        TypeMethods methods,
        string name,
        List<QueryMethod> onType
    )
    {
        var indent = TypeEmitter.GetIndent(depth);
        _ = builder
            .Append(indent)
            .Append("/// <summary>\n")
            .Append(indent)
            .Append("/// The public repository methods of <c>")
            .Append(XmlDocWriter.Escape(methods.DisplayName))
            .Append("</c>, which it implements.\n")
            .Append(indent)
            .Append("/// </summary>\n");
        TypeEmitter.Open(
            builder,
            depth,
            (methods.InterfaceIsPublic ? "public" : "internal")
                + " partial interface "
                + CodeNameOf(name)
                + methods.Types[methods.Types.Count - 1].TypeParameters
        );
        var memberIndent = TypeEmitter.GetIndent(depth + 1);
        var isFirst = true;
        foreach (var method in onType.Where(static method => IsInterfaceMember(method)))
        {
            TypeEmitter.Separate(builder, ref isFirst);
            QueryMethodWriter.AppendInterfaceMember(builder, memberIndent, method);
        }

        _ = builder.Append(indent).Append("}\n");
    }

    // A member of the interface: a repository's public method on the type.
    private static bool IsInterfaceMember(QueryMethod method) =>
        method is { Source: not null, Host: { Location: MemberLocation.Class, Accessibility: Visibility.Public } };

    // The interface's name as code writes it: a keyword after an @, a contextual one too, since C# refuses some of them
    // as the name of a type, such as file, scoped and required, and extension, which this compiler may not know.
    private static string CodeNameOf(string name) =>
        SqlIdentifier.IsReservedKeyword(name) || SqlIdentifier.IsContextualKeyword(name) ? "@" + name : name;

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
