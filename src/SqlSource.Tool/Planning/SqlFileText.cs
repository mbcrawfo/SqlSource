using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// A <c>.sql</c> file on the disk in the form the generator's reader takes one.
/// </summary>
/// <remarks>
/// The file is read once, the first time it is asked for, and every later question gets that read.  A file that
/// cannot be read has no text, and the generator parses that as an empty file.  A file that the compiler reads in its
/// fallback encoding is the planner's <c>SQLSRC271</c>: on Windows the compiler reads it in the system's code page, so
/// its hashes are not the tool's, and elsewhere both read its characters outside ASCII as replacement characters.
/// </remarks>
internal sealed class SqlFileText(string path) : AdditionalText
{
    private readonly Lazy<FileContents?> _contents = new(() => FileText.Read(path));

    public override string Path => path;

    /// <summary>
    /// Whether the C# compiler reads the file in a fallback encoding.  False when the file cannot be read.
    /// </summary>
    public bool CompilerFallsBack => _contents.Value is { CompilerFallsBack: true };

    public override SourceText? GetText(CancellationToken cancellationToken = default) => _contents.Value?.Text;
}
