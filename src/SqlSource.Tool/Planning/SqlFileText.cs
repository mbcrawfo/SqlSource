using System;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// A <c>.sql</c> file on the disk in the form the generator's reader takes one.
/// </summary>
/// <remarks>
/// A file that cannot be read has no text, and the generator parses that as an empty file.  A file that is not UTF-8
/// is not read as the compiler reads it: <c>docs/tech-debt/TD-0032</c>.
/// </remarks>
internal sealed class SqlFileText(string path) : AdditionalText
{
    public override string Path => path;

    public override SourceText? GetText(CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SourceText.From(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
