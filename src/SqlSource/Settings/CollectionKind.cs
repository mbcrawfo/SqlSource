namespace SqlSource.Settings;

/// <summary>
/// The type a generated method returns many rows in.  The generator's own form of <c>GeneratorCollectionType</c>,
/// member for member.
/// </summary>
internal enum CollectionKind
{
    /// <summary><c>IEnumerable&lt;T&gt;</c>, over an array.</summary>
    IEnumerable = 0,

    /// <summary><c>ICollection&lt;T&gt;</c>, over a list.</summary>
    ICollection = 1,

    /// <summary><c>IReadOnlyCollection&lt;T&gt;</c>, over an array.</summary>
    IReadOnlyCollection = 2,

    /// <summary><c>IList&lt;T&gt;</c>, over a list.</summary>
    IList = 3,

    /// <summary><c>IReadOnlyList&lt;T&gt;</c>, over an array.</summary>
    IReadOnlyList = 4,

    /// <summary>An array.</summary>
    Array = 5,

    /// <summary><c>List&lt;T&gt;</c>.</summary>
    List = 6,

    /// <summary><c>ImmutableArray&lt;T&gt;</c>.</summary>
    ImmutableArray = 7,

    /// <summary><c>ImmutableList&lt;T&gt;</c>.</summary>
    ImmutableList = 8,

    /// <summary><c>IImmutableList&lt;T&gt;</c>, over an immutable list.</summary>
    IImmutableList = 9,
}
