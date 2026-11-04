namespace SqlSource.Settings;

/// <summary>
/// Where a query's generated members go, as the <c>member-location</c> of a marker or a settings file names it.
/// </summary>
internal enum MemberLocation
{
    /// <summary>In a generated static class beside the type, as extension methods.</summary>
    ExtensionClass = 0,

    /// <summary>On the type itself, as static members.</summary>
    Class = 1,

    /// <summary>In a static class nested in the type, named by the <c>nested-class</c> setting.</summary>
    NestedClass = 2,
}
