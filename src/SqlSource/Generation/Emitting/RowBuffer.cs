using System.Text;
using SqlSource.Generation.Methods;
using SqlSource.Settings;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// The buffer a method of <see cref="ResultShape.Many" /> reads its rows into when it returns an array, a list, an
/// immutable array or one of their interfaces: one struct for the project, in a file of its own, which rents its arrays
/// from the shared pool.
/// </summary>
/// <remarks>
/// It is internal and marked with Roslyn's <c>EmbeddedAttribute</c>, so that no other assembly, even one that sees the
/// project's internals, sees it, and a method names it from <c>global::</c>, so that no argument or type of the user's
/// hides it.
/// </remarks>
internal static class RowBuffer
{
    /// <summary>The name of the buffer's file.</summary>
    public const string HintName = "SqlSource-RowBuffer.g.cs";

    /// <summary>The buffer's type as a method names it, from <c>global::</c>, without its type argument.</summary>
    public const string TypeText = "global::SqlSource.SqlSourceRowBuffer";

    // The lines before the declaration, and after it, at depth 0.  An empty line is written without indentation.
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
        "        Rows().CopyTo(result);",
        "        Release();",
        "        return result;",
        "    }",
        "",
        "    public global::System.Collections.Generic.List<T> ToList()",
        "    {",
        "        if (items is null)",
        "        {",
        "            return new global::System.Collections.Generic.List<T>();",
        "        }",
        "",
        "        var result = new global::System.Collections.Generic.List<T>(count);",
        "        global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(result, count);",
        "        Rows().CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(result));",
        "        Release();",
        "        return result;",
        "    }",
        "",
        "    public global::System.Collections.Immutable.ImmutableArray<T> ToImmutableArray()",
        "    {",
        "        if (items is null)",
        "        {",
        "            return global::System.Collections.Immutable.ImmutableArray<T>.Empty;",
        "        }",
        "",
        "        return global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray("
            + "ToArray());",
        "    }",
        "",
        "    private global::System.ReadOnlySpan<T> Rows() =>",
        "        global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(",
        "            ref global::System.Runtime.CompilerServices.Unsafe.As<object, T>(",
        "                ref global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items!)),",
        "            count);",
        "",
        "    private void Release()",
        "    {",
        "        Return(items!);",
        "        items = null;",
        "        count = 0;",
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
    /// pool and returns an array, <c>IEnumerable&lt;T&gt;</c>, <c>IReadOnlyCollection&lt;T&gt;</c>,
    /// <c>IReadOnlyList&lt;T&gt;</c>, <c>List&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, <c>ICollection&lt;T&gt;</c> or
    /// <c>ImmutableArray&lt;T&gt;</c>: every type but <c>ImmutableList&lt;T&gt;</c>, <c>IImmutableList&lt;T&gt;</c> and
    /// <c>IAsyncEnumerable&lt;T&gt;</c>, which buffers nothing.
    /// </summary>
    public static bool IsUsedBy(QueryMethod method) =>
        method.Shape == ResultShape.Many
        && method.UseArrayPool
        && method.Collection
            is CollectionKind.Array
                or CollectionKind.IEnumerable
                or CollectionKind.IReadOnlyCollection
                or CollectionKind.IReadOnlyList
                or CollectionKind.List
                or CollectionKind.IList
                or CollectionKind.ICollection
                or CollectionKind.ImmutableArray;

    /// <summary>
    /// The buffer's file: the struct, its documentation comment first, in the namespace <c>SqlSource</c>.
    /// </summary>
    public static string Source { get; } = BuildSource();

    private static string BuildSource()
    {
        var builder = new StringBuilder(GeneratedText.Header).Append("\nnamespace SqlSource\n{\n");
        AppendLines(builder, GeneratedText.Indent, Summary);
        _ = builder
            .Append(GeneratedText.Indent)
            .Append("[global::Microsoft.CodeAnalysis.Embedded]\n")
            .Append(GeneratedText.Indent)
            .Append("internal struct SqlSourceRowBuffer<T>\n");
        AppendLines(builder, GeneratedText.Indent, Body);
        return builder.Append("}\n").ToString();
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
