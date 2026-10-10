using Microsoft.CodeAnalysis.Text;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tool.Planning;

/// <summary>
/// What one <c>[SqlSourceGenerate]</c> says that changes what is described: where its type's <c>.sql</c> files are,
/// and what is generated for them.
/// </summary>
/// <param name="SourcePath">The full path of the C# file that carries the attribute.</param>
/// <param name="Path">The attribute's <c>Path</c> as written, or null when it is not set or is empty.</param>
/// <param name="Output">The attribute's <c>Output</c>, or null when it is not set.</param>
/// <param name="Position">Where the attribute starts in the file, counted from zero.</param>
internal sealed record TypeClaim(string SourcePath, string? Path, OutputKind? Output, LinePosition Position);
