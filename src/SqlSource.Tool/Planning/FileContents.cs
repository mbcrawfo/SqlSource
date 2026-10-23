using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// A file read from the disk.
/// </summary>
/// <param name="Text">The text of the file, with a replacement character for a sequence that is not UTF-8.</param>
/// <param name="CompilerFallsBack">
/// Whether the C# compiler reads the file in a fallback encoding.  It does when the file has no UTF-8 or UTF-16 byte
/// order mark and its bytes are not valid UTF-8.  A file with a byte order mark is decoded by that mark, whatever its
/// bytes, so it is never one.
/// </param>
internal sealed record FileContents(SourceText Text, bool CompilerFallsBack);
