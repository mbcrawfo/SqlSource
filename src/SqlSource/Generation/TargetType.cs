using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;

namespace SqlSource.Generation;

/// <summary>
/// A type marked with the generated attribute: everything the later steps need from the declaration, as data.
/// </summary>
/// <param name="Namespace">The namespace as it is written in code.  Empty for the global namespace.</param>
/// <param name="Types">The containing types from the outermost to the type itself.  Never empty.</param>
/// <param name="MetadataName">
/// The name that <c>Compilation.GetTypeByMetadataName</c> finds the type by: the namespace, then each type's metadata
/// name, with its arity, from the outermost, joined by <c>+</c>.
/// </param>
/// <param name="IsPublic">
/// Whether the type and every type it is nested in are declared <c>public</c>, which makes its models public.
/// </param>
/// <param name="IsReachableAsProtected">
/// Whether code of another assembly can reach the type though it is not public: the type and every type it is nested
/// in are declared <c>public</c>, <c>protected</c> or <c>protected internal</c>, and one of them is not public.  A
/// public or protected member of it is then reachable as protected from a derived type there, and its internal models
/// are less accessible than such a member (CS0051).
/// </param>
/// <param name="IsStatic">Whether the type is a static class, which cannot have protected members.</param>
/// <param name="IsSealed">
/// Whether the type is sealed, as the compiler says it: a sealed class or record, and every struct.  A static class is
/// not.  A protected member of a sealed class gives the compiler's warning CS0628.
/// </param>
/// <param name="Source">The attribute's <c>Source</c>, or null when it is not set or is empty.</param>
/// <param name="GenerateInterface">
/// The attribute's <c>GenerateInterface</c>, or null when it is not written, which leaves the project's switch.
/// </param>
/// <param name="InterfaceName">The attribute's <c>InterfaceName</c>, or null when it is not set or is empty.</param>
/// <param name="FilePath">The path of the source file that carries the attribute.</param>
/// <param name="AttributeLocation">Where the attribute is.</param>
/// <param name="TakenNames">
/// The names that a nested class of the type, and a member that goes on the type, cannot have, sorted ordinally:
/// those of the type, its members, its nested types and its type parameters.  Read for every type, since where a
/// query's members go is known only once its file is parsed.
/// </param>
/// <param name="Diagnostics">The problems found in the declaration.</param>
internal sealed record TargetType(
    string Namespace,
    EquatableArray<TypeDeclaration> Types,
    string MetadataName,
    bool IsPublic,
    bool IsReachableAsProtected,
    bool IsStatic,
    bool IsSealed,
    string? Source,
    bool? GenerateInterface,
    string? InterfaceName,
    string FilePath,
    LocationInfo AttributeLocation,
    EquatableArray<string> TakenNames,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>
    /// The name of the type itself, as a value: without the <c>@</c> of an escaped keyword.
    /// </summary>
    public string Name => Types[Types.Count - 1].ValueName;

    /// <summary>
    /// The namespace as a value: without the <c>@</c> of an escaped keyword.  Empty for the global namespace.
    /// </summary>
    public string NamespaceValue => Namespace.Replace("@", string.Empty);

    /// <summary>
    /// Whether the type is a struct or a record struct, which cannot have protected members.  It is computed, so it
    /// takes no part in equality.
    /// </summary>
    public bool IsStruct => Types[Types.Count - 1].Keyword is "struct" or "record struct";
}
