using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// What the generator produces for one type.
/// </summary>
/// <param name="HintName">The name of the generated file.  Unique among the types of a compilation.</param>
/// <param name="Source">The generated file, or null when the type has a problem and gets none.</param>
/// <param name="Diagnostics">The type's problems.  Those of its <c>.sql</c> files are reported for each file.</param>
internal sealed record TypeOutput(string HintName, string? Source, EquatableArray<DiagnosticInfo> Diagnostics);
