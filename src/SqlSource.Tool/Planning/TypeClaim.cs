using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// What one <c>[SqlSourceGenerate]</c> says that changes what is described: where its type's <c>.sql</c> files are.
/// </summary>
/// <param name="SourcePath">The full path of the C# file that carries the attribute.</param>
/// <param name="TypeName">
/// The name of the type itself, as the generator takes it: without the <c>@</c> of an escaped keyword and without type
/// parameters.  With no <c>Source</c>, the type's file is named for it.
/// </param>
/// <param name="Source">The attribute's <c>Source</c> as written, or null when it is not set or is empty.</param>
/// <param name="Position">Where the attribute starts in the file, counted from zero.</param>
internal sealed record TypeClaim(string SourcePath, string TypeName, string? Source, LinePosition Position);
