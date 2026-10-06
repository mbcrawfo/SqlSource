using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// A type marked with the generated attribute: everything the later steps need from the declaration, as data.
/// </summary>
/// <param name="Namespace">The namespace as it is written in code.  Empty for the global namespace.</param>
/// <param name="Types">The containing types from the outermost to the type itself.  Never empty.</param>
/// <param name="Placement">Where the members go.</param>
/// <param name="Path">The attribute's <c>Path</c>, or null when it is not set or is empty.</param>
/// <param name="FilePath">The path of the source file that carries the attribute.</param>
/// <param name="AttributeLocation">Where the attribute is.</param>
/// <param name="Diagnostics">The problems found in the declaration.</param>
internal sealed record TargetType(
    string Namespace,
    EquatableArray<TypeDeclaration> Types,
    MemberPlacement Placement,
    string? Path,
    string FilePath,
    LocationInfo AttributeLocation,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>
    /// The name of the type itself, as a value: without the <c>@</c> of an escaped keyword.
    /// </summary>
    public string Name => Types[Types.Count - 1].ValueName;
}
