namespace SqlSource.Settings;

/// <summary>
/// Where a type's generated members go.  The generator's own form of the <c>SqlLocation</c> it emits.
/// </summary>
internal enum MemberPlacement
{
    /// <summary>In a private static class nested in the type, named <c>Sql</c> unless a setting names it.</summary>
    Nested,

    /// <summary>On the type itself.</summary>
    Direct,
}
