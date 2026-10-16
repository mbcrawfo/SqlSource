using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SqlSource.Testing;

namespace SqlSource.Tests.Documentation;

// The documents a user reads: the readme of the repository, the readme of the tool, and the pages of docs/manual/.
// The tests of this folder read them as text: their links, the anchors a link may name, and the fenced blocks.
internal static partial class UserDocs
{
    // Where a link of a packed readme points, since nuget.org shows the readme without the repository around it.
    public const string RepositoryUrl = "https://github.com/mbcrawfo/SqlSource/blob/main/";

    private const string Manual = "docs/manual";

    private static readonly ConcurrentDictionary<string, Document> Documents = new(StringComparer.Ordinal);

    // The repository-relative paths, with '/', of the user documents.  A page of docs/manual/ is one for the user,
    // except AGENTS.md, which is guidance for an agent.
    public static IReadOnlyList<string> Files { get; } =
    [
        "README.md",
        "src/SqlSource.Tool/README.md",
        .. Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot.Folder, Manual), "*.md")
            .Select(path => $"{Manual}/{Path.GetFileName(path)}")
            .Where(path => path != $"{Manual}/AGENTS.md")
            .Order(StringComparer.Ordinal),
    ];

    // What GitHub makes of a heading's text: the anchor a link to it names.
    public static string Slug(string heading)
    {
        // The text of a link is the heading's text, and its target is not.
        var text = LinkPattern().Replace(heading, "$1");
        var slug = new StringBuilder();
        foreach (var upper in text.Trim())
        {
            var character = char.ToLowerInvariant(upper);
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                _ = slug.Append(character);
            }
            else if (character == ' ')
            {
                _ = slug.Append('-');
            }
        }

        return slug.ToString();
    }

    // The anchors of a document of the repository: each heading's slug, and every `<a id="...">` and `<a name="...">`.
    public static ISet<string> Anchors(string file) => Load(file).AnchorSet;

    // Every inline link outside fenced blocks and inline code, with its line (from 1) and its target as written.
    public static IEnumerable<(string File, int Line, string Target)> Links() =>
        Files.SelectMany(file => Load(file).LinkList.Select(link => (file, link.Line, link.Target)));

    public static IEnumerable<(string File, int Line, string Info, string Text)> FencedBlocks() =>
        Files.SelectMany(file => Load(file).BlockList.Select(block => (file, block.Line, block.Info, block.Text)));

    // Where a link of `file` goes: a path of the repository with '/' and no trailing '/', and the anchor with no '#'.
    // Null when the link goes elsewhere: another site, or a scheme such as mailto.
    public static (string Path, string Anchor)? Resolve(string file, string target)
    {
        var hash = target.IndexOf('#', StringComparison.Ordinal);
        var path = Uri.UnescapeDataString(hash < 0 ? target : target[..hash]);
        var anchor = hash < 0 ? "" : Uri.UnescapeDataString(target[(hash + 1)..]);

        if (target.StartsWith(RepositoryUrl, StringComparison.Ordinal))
        {
            return (path[RepositoryUrl.Length..].TrimEnd('/'), anchor);
        }

        if (SchemePattern().IsMatch(target))
        {
            return null;
        }

        if (path.Length == 0)
        {
            return (file, anchor);
        }

        var root = RepositoryRoot.Folder;
        var folder = Path.GetDirectoryName(file) ?? "";
        var full = Path.GetFullPath(Path.Combine(root, folder, path));
        return (Path.GetRelativePath(root, full).Replace('\\', '/').TrimEnd('/'), anchor);
    }

    // Whether a path of the repository, as Resolve gives it, names a file or a folder that is there.
    public static bool Exists(string path)
    {
        var full = Path.Combine(RepositoryRoot.Folder, path);
        return File.Exists(full) || Directory.Exists(full);
    }

    private static Document Load(string file) =>
        Documents.GetOrAdd(file, name => Read(File.ReadAllLines(Path.Combine(RepositoryRoot.Folder, name))));

    private static Document Read(string[] lines)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var links = new List<(int Line, string Target)>();
        var blocks = new List<(int Line, string Info, string Text)>();

        Fence? fence = null;
        var content = new List<string>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var number = index + 1;

            if (fence is { } open)
            {
                if (IsClosing(line, open))
                {
                    blocks.Add((open.Line, open.Info, string.Join('\n', content)));
                    fence = null;
                }
                else
                {
                    content.Add(Unindent(line, open.Indent));
                }

                continue;
            }

            if (OpeningPattern().Match(line) is { Success: true } opening)
            {
                fence = new Fence(
                    number,
                    opening.Groups[1].Length,
                    opening.Groups[2].Value[0],
                    opening.Groups[2].Length,
                    opening.Groups[3].Value.Trim()
                );
                content.Clear();
                continue;
            }

            if (HeadingPattern().Match(line) is { Success: true } heading)
            {
                var slug = Slug(heading.Groups[1].Value);
                var seen = counts.GetValueOrDefault(slug);
                counts[slug] = seen + 1;
                _ = anchors.Add(seen == 0 ? slug : $"{slug}-{seen}");
            }

            foreach (Match anchor in AnchorPattern().Matches(line))
            {
                _ = anchors.Add(anchor.Groups[1].Value);
            }

            foreach (Match link in LinkTargetPattern().Matches(CodeSpanPattern().Replace(line, " ")))
            {
                links.Add((number, link.Groups[1].Value));
            }
        }

        // A fence that is never closed runs to the end of the file.
        if (fence is { } unclosed)
        {
            blocks.Add((unclosed.Line, unclosed.Info, string.Join('\n', content)));
        }

        return new Document(anchors, links, blocks);
    }

    private static bool IsClosing(string line, Fence fence)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= fence.Length && trimmed.All(character => character == fence.Character);
    }

    private static string Unindent(string line, int indent)
    {
        var skip = 0;
        while (skip < indent && skip < line.Length && line[skip] == ' ')
        {
            skip++;
        }

        return line[skip..];
    }

    private sealed record Fence(int Line, int Indent, char Character, int Length, string Info);

    private sealed record Document(
        ISet<string> AnchorSet,
        IReadOnlyList<(int Line, string Target)> LinkList,
        IReadOnlyList<(int Line, string Info, string Text)> BlockList
    );

    // The start of a fenced block: three or more backticks or tildes, then the info string.
    [GeneratedRegex(@"^( *)(`{3,}|~{3,})(.*)$")]
    private static partial Regex OpeningPattern();

    [GeneratedRegex(@"^ {0,3}#{1,6}[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"<a\s+(?:id|name)=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorPattern();

    // A code span: a run of backticks, then the same run again.
    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex CodeSpanPattern();

    // The target of an inline link or image, after `](`, with an optional title.
    [GeneratedRegex(@"\]\(\s*<?([^)\s>]*)>?(?:\s+""[^""]*"")?\s*\)")]
    private static partial Regex LinkTargetPattern();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:")]
    private static partial Regex SchemePattern();
}
