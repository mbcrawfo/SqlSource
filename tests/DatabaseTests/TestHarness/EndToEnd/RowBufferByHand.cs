namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The pooled buffer that a generated many method of any pooled collection type reads its rows into, for the
// hand-written methods of the allocation tests of both end-to-end projects, which make the generated method's calls.
// From its declaration to its last brace it is the generated SqlSourceRowBuffer<T> of
// src/SqlSource/Generation/Emitting/RowBuffer.cs but its name and its modifier, at the depth of a file-scoped
// namespace, and RowBufferTests of tests/SqlSource.Tests fails when the two differ: a change to the generated buffer
// changes this one with it.  So CSharpier and the rules of the repository's own code leave it as it is generated, and
// dotnet format with them: IDE0001 would drop each global::, IDE0028 would write new List<T>() as [], IDE0301 would
// write Array.Empty<T>() as [], IDE1006 would name its fields with a '_' and IDE0251 would make Return readonly.
#pragma warning disable IDE0001, IDE0028, IDE0301, IDE1006, IDE0251
// csharpier-ignore-start
internal struct RowBufferByHand<T>
    where T : class
{
    private object[]? items;

    private int count;

    public void Add(T row)
    {
        if (items is null || count == items.Length)
        {
            Grow();
        }

        items![count++] = row;
    }

    public T[] ToArray()
    {
        if (items is null)
        {
            return global::System.Array.Empty<T>();
        }

        var result = new T[count];
        Rows().CopyTo(result);
        Release();
        return result;
    }

    public global::System.Collections.Generic.List<T> ToList()
    {
        if (items is null)
        {
            return new global::System.Collections.Generic.List<T>();
        }

        var result = new global::System.Collections.Generic.List<T>(count);
        global::System.Runtime.InteropServices.CollectionsMarshal.SetCount(result, count);
        Rows().CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(result));
        Release();
        return result;
    }

    public global::System.Collections.Immutable.ImmutableArray<T> ToImmutableArray()
    {
        if (items is null)
        {
            return global::System.Collections.Immutable.ImmutableArray<T>.Empty;
        }

        return global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(ToArray());
    }

    private global::System.ReadOnlySpan<T> Rows() =>
        global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
            ref global::System.Runtime.CompilerServices.Unsafe.As<object, T>(
                ref global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items!)),
            count);

    private void Release()
    {
        Return(items!);
        items = null;
        count = 0;
    }

    private void Grow()
    {
        var length = items is null ? 16 : items.Length * 2;
        var next = length <= 1024 ? global::System.Buffers.ArrayPool<object>.Shared.Rent(length) : new object[length];
        if (items is not null)
        {
            global::System.Array.Copy(items, next, count);
            Return(items);
        }

        items = next;
    }

    private void Return(object[] buffer)
    {
        if (buffer.Length <= 1024)
        {
            global::System.Array.Clear(buffer, 0, count);
            global::System.Buffers.ArrayPool<object>.Shared.Return(buffer);
        }
    }
}
// csharpier-ignore-end
#pragma warning restore IDE0001, IDE0028, IDE0301, IDE1006, IDE0251
