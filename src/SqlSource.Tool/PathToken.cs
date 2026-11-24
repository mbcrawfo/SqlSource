using System;
using System.IO;
using System.Linq;

namespace SqlSource.Tool;

/// <summary>
/// Whether a token typed where a path goes reads as a path, so that a message or the log may repeat it.  A password
/// typed there by mistake, as in <c>sqlsource describe S3cret</c>, does not.
/// </summary>
internal static class PathToken
{
    // The extensions of what the tool's paths name, so that a mistyped project, query or log is still named.
    private static readonly string[] KnownExtensions =
    [
        ".sln",
        ".slnx",
        ".csproj",
        ".sql",
        ".env",
        ".log",
        ".json",
        ".jsonl",
        ".zip",
    ];

    /// <summary>
    /// Whether the token holds <c>=</c> or <c>;</c>, which a connection string is made of.
    /// </summary>
    public static bool HoldsConnectionText(string text) => text.AsSpan().IndexOfAny('=', ';') >= 0;

    /// <summary>
    /// Whether a message may print this token as a path: it holds no <c>=</c> or <c>;</c>, and it is empty, ends in an
    /// extension of the tool's paths, names what exists, or starts at a folder that exists.  A separator alone is not
    /// enough, since a generated password can hold <c>/</c>.
    /// </summary>
    /// <param name="text">The token.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    public static bool MayBePrinted(string text, string workingDirectory)
    {
        if (HoldsConnectionText(text))
        {
            return false;
        }

        if (text.Length == 0 || KnownExtensions.Contains(Path.GetExtension(text), StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = Path.Combine(workingDirectory, text);
        return File.Exists(path) || Directory.Exists(path) || StartsAtAFolder(text, workingDirectory);
    }

    // The first folder that the token names, after its root, exists.  A token of one name has none.
    private static bool StartsAtAFolder(string text, string workingDirectory)
    {
        var root = Path.GetPathRoot(text) ?? "";
        var rest = text[root.Length..];
        var end = rest.AsSpan().IndexOfAny('/', '\\');
        return end > 0 && Directory.Exists(Path.Combine(workingDirectory, root, rest[..end]));
    }
}
