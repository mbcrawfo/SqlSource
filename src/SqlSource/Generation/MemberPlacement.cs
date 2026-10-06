namespace SqlSource.Generation;

/// <summary>
/// Where a type's generated members go.  The generator's own form of the <c>SqlQueriesMode</c> it emits.
/// </summary>
internal enum MemberPlacement
{
    /// <summary>In a private static class named <c>Sql</c>, nested in the type.</summary>
    Nested,

    /// <summary>On the type itself.</summary>
    Direct,
}
