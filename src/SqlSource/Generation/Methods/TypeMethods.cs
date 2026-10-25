using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Settings;

namespace SqlSource.Generation.Methods;

/// <summary>
/// The methods of one type, and what their file is written from.
/// </summary>
/// <param name="TypeKey">The type, as <c>HintName.NameOf</c> gives it.</param>
/// <param name="HintName">The name of the methods' file, from <c>HintName.ForMethods</c>.</param>
/// <param name="Placement">Where the methods go.</param>
/// <param name="Namespace">The type's namespace as a value.  Empty for the global namespace.</param>
/// <param name="NamespaceText">The type's namespace as code writes it.  Empty for the global namespace.</param>
/// <param name="Types">The type and the types it is nested in, outermost first.</param>
/// <param name="ExtensionClass">
/// The name of the class that holds the methods under <see cref="MethodPlacement.ExtensionClass" />: the type's own
/// name and <c>Extensions</c>.
/// </param>
/// <param name="IsPublic">
/// Whether the type and every type around it are public, which makes the extension class public.
/// </param>
/// <param name="AttributeLocation">Where the type's attribute is.</param>
/// <param name="Methods">The methods, in the order of the type's files and of their queries.</param>
/// <param name="Diagnostics">The problems of the methods, each at its query's name.</param>
/// <param name="CodeGenEngines">The engines of the type's queries whose output is <c>CodeGen</c>.</param>
internal sealed record TypeMethods(
    string TypeKey,
    string HintName,
    MethodPlacement Placement,
    string Namespace,
    string NamespaceText,
    EquatableArray<TypeDeclaration> Types,
    string ExtensionClass,
    bool IsPublic,
    LocationInfo AttributeLocation,
    EquatableArray<QueryMethod> Methods,
    EquatableArray<DiagnosticInfo> Diagnostics,
    CodeGenEngines CodeGenEngines
);
