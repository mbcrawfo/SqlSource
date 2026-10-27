using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// A type marked with the generated attribute: everything the later steps need from the declaration, as data.
/// </summary>
/// <param name="Namespace">The namespace as it is written in code.  Empty for the global namespace.</param>
/// <param name="Types">The containing types from the outermost to the type itself.  Never empty.</param>
/// <param name="Placement">Where the members go.</param>
/// <param name="MethodPlacement">Where the methods of the type's queries go.</param>
/// <param name="Settings">What the attribute says about the settings.</param>
/// <param name="IsPublic">
/// Whether the type and every type it is nested in are declared <c>public</c>, which makes its models public.
/// </param>
/// <param name="Path">The attribute's <c>Path</c>, or null when it is not set or is empty.</param>
/// <param name="FilePath">The path of the source file that carries the attribute.</param>
/// <param name="AttributeLocation">Where the attribute is.</param>
/// <param name="Diagnostics">The problems found in the declaration.</param>
internal sealed record TargetType(
    string Namespace,
    EquatableArray<TypeDeclaration> Types,
    MemberPlacement Placement,
    MethodPlacement MethodPlacement,
    SettingsLevel Settings,
    bool IsPublic,
    string? Path,
    string FilePath,
    LocationInfo AttributeLocation,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>
    /// The name of the class that holds the members in <see cref="MemberPlacement.Nested" /> placement.  The reader
    /// leaves the default, and <see cref="NestedClassCheck" /> sets it once the chain of the folder of the type's C#
    /// file is known.
    /// </summary>
    public string NestedClass { get; init; } = QuerySettings.DefaultNestedClass;

    /// <summary>
    /// The name of the type itself, as a value: without the <c>@</c> of an escaped keyword.
    /// </summary>
    public string Name => Types[Types.Count - 1].ValueName;

    /// <summary>
    /// The namespace as a value: without the <c>@</c> of an escaped keyword.  Empty for the global namespace.
    /// </summary>
    public string NamespaceValue => Namespace.Replace("@", string.Empty);
}
