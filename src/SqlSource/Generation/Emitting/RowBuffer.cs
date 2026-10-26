using System.Text;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// The buffer a method of <see cref="ResultShape.Many" /> reads its rows into when it returns an array or a read-only
/// interface: a <c>file</c> struct at the end of the file of methods, which rents its arrays from the shared pool.
/// </summary>
/// <remarks>
/// A method names it from <c>global::</c>, so no argument or type of the user's hides it, and only a type of the same
/// name and namespace that the file declares again, the type the methods go on, can clash with it.
/// </remarks>
internal static class RowBuffer
{
    /// <summary>The buffer's name.</summary>
    public const string Name = "SqlSourceRowBuffer";

    /// <summary>The buffer's name when the type the methods go on has <see cref="Name" />.</summary>
    public const string OtherName = "SqlSourceRowBufferOfRows";

    // The lines before the name, and after it, at depth 0.  An empty line is written without indentation.
    private static readonly string[] Summary =
    [
        "/// <summary>",
        "/// The rows of a query method, read into buffers from the shared array pool: one of up to 1,024 elements is",
        "/// rented and returned cleared, and a larger one is left to the garbage collector.",
        "/// </summary>",
    ];

    private static readonly string[] Body =
    [
        "    where T : class",
        "{",
        "    private object[]? items;",
        "",
        "    private int count;",
        "",
        "    public void Add(T row)",
        "    {",
        "        if (items is null || count == items.Length)",
        "        {",
        "            Grow();",
        "        }",
        "",
        "        items![count++] = row;",
        "    }",
        "",
        "    public T[] ToArray()",
        "    {",
        "        if (items is null)",
        "        {",
        "            return global::System.Array.Empty<T>();",
        "        }",
        "",
        "        var result = new T[count];",
        "        global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(",
        "            ref global::System.Runtime.CompilerServices.Unsafe.As<object, T>(",
        "                ref global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items)),",
        "            count).CopyTo(result);",
        "        Return(items);",
        "        items = null;",
        "        count = 0;",
        "        return result;",
        "    }",
        "",
        "    private void Grow()",
        "    {",
        "        var length = items is null ? 16 : items.Length * 2;",
        "        var next = length <= 1024 ? global::System.Buffers.ArrayPool<object>.Shared.Rent(length) : "
            + "new object[length];",
        "        if (items is not null)",
        "        {",
        "            global::System.Array.Copy(items, next, count);",
        "            Return(items);",
        "        }",
        "",
        "        items = next;",
        "    }",
        "",
        "    private void Return(object[] buffer)",
        "    {",
        "        if (buffer.Length <= 1024)",
        "        {",
        "            global::System.Array.Clear(buffer, 0, count);",
        "            global::System.Buffers.ArrayPool<object>.Shared.Return(buffer);",
        "        }",
        "    }",
        "}",
    ];

    /// <summary>
    /// Whether the method reads its rows into the buffer: a method of <see cref="ResultShape.Many" /> that may use the
    /// pool and returns an array, <c>IEnumerable&lt;T&gt;</c>, <c>IReadOnlyCollection&lt;T&gt;</c> or
    /// <c>IReadOnlyList&lt;T&gt;</c>.
    /// </summary>
    public static bool IsUsedBy(QueryMethod method) =>
        method.Shape == ResultShape.Many
        && method.UseArrayPool
        && method.Collection
            is CollectionKind.Array
                or CollectionKind.IEnumerable
                or CollectionKind.IReadOnlyCollection
                or CollectionKind.IReadOnlyList;

    /// <summary>
    /// The buffer's name in the file of <paramref name="methods" />: <see cref="OtherName" /> when the methods go on a
    /// type whose outermost type, the one in the namespace beside the buffer, has <see cref="Name" />.
    /// </summary>
    public static string NameFor(TypeMethods methods) =>
        methods.Placement != MethodPlacement.ExtensionClass && methods.Types[0].ValueName == Name ? OtherName : Name;

    /// <summary>The buffer's type as a method names it, from <c>global::</c>, without its type argument.</summary>
    public static string TypeTextFor(TypeMethods methods) =>
        "global::" + (methods.NamespaceText.Length > 0 ? methods.NamespaceText + "." : string.Empty) + NameFor(methods);

    /// <summary>
    /// Appends the buffer's struct, its documentation comment first, each line after <paramref name="indent" />.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line, the namespace's depth.</param>
    /// <param name="name">The struct's name, from <see cref="NameFor" />.</param>
    public static void Append(StringBuilder builder, string indent, string name)
    {
        AppendLines(builder, indent, Summary);
        _ = builder.Append(indent).Append("file struct ").Append(name).Append("<T>\n");
        AppendLines(builder, indent, Body);
    }

    private static void AppendLines(StringBuilder builder, string indent, string[] lines)
    {
        foreach (var line in lines)
        {
            if (line.Length > 0)
            {
                _ = builder.Append(indent).Append(line);
            }

            _ = builder.Append('\n');
        }
    }
}
