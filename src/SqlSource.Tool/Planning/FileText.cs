using System;
using System.IO;
using System.Text.Unicode;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Reads a file of a project as the text the generator's code takes: a <c>.sql</c> file for the parser, a source
/// file for <see cref="AttributeReader" />.
/// </summary>
internal static class FileText
{
    /// <summary>
    /// The contents of the file, or null when the system does not let it be read.  The caller says what a file with
    /// no text means.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    public static FileContents? Read(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            // A byte order mark decides the encoding, and without one the bytes are UTF-8.
            var text = SourceText.From(bytes, bytes.Length);
            return new FileContents(text, CompilerFallsBack: !HasByteOrderMark(bytes) && !Utf8.IsValid(bytes));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool HasByteOrderMark(byte[] bytes) =>
        bytes is [0xEF, 0xBB, 0xBF, ..] or [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..];
}
