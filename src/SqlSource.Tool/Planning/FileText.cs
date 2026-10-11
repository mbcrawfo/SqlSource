using System;
using System.IO;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Reads a file of a project as the text the generator's code takes: a <c>.sql</c> file for the parser, a source
/// file for <see cref="AttributeReader" />.
/// </summary>
internal static class FileText
{
    /// <summary>
    /// The text of the file, or null when the system does not let it be read.  The caller says what a file with no
    /// text means.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    public static SourceText? Read(string path)
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
