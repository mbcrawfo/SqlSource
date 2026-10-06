using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// A type and the <c>.sql</c> files its <c>Path</c> resolves to.
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="Files">The normalised paths of the type's files, in member order.</param>
/// <param name="Diagnostics">
/// The type's problems so far: those of the declaration, and those found while resolving.  A type with any gets no
/// generated file.
/// </param>
internal sealed record TypeFiles(
    TargetType Type,
    EquatableArray<string> Files,
    EquatableArray<DiagnosticInfo> Diagnostics
);
