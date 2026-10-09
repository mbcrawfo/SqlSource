namespace SqlSource.Settings;

/// <summary>
/// Where a type's generated methods go.  The generator's own form of <c>MethodLocation</c>, member for member.  It
/// is set by the attribute alone, as <c>SqlLocation</c> is: it has no MSBuild property, no metadata and no marker.
/// </summary>
internal enum MethodPlacement
{
    /// <summary>In a generated static class beside the type, as extension methods.</summary>
    ExtensionClass = 0,

    /// <summary>On the type, as public static methods.</summary>
    Public = 1,

    /// <summary>On the type, as internal static methods.</summary>
    Internal = 2,

    /// <summary>On the type, as private static methods.</summary>
    Private = 3,
}
