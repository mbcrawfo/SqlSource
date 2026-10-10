using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One <c>[SqlSourceGenerate]</c> that claims a file, by where it stands: the tool reads syntax and keeps no name
/// of a type.  The log and <c>--verbose</c> say it.
/// </summary>
/// <param name="ProjectPath">The full path of the project the attribute is in.</param>
/// <param name="SourcePath">The full path of the C# file that carries it.</param>
/// <param name="Position">Where the attribute starts in that file, counted from zero.</param>
internal sealed record FileClaim(string ProjectPath, string SourcePath, LinePosition Position);
