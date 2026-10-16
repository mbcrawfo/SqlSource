using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
            if (char.IsLetterOrDigit(character) || character is '-' or '_' || IsNonSpacingMark(character))
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

    private static bool IsNonSpacingMark(char character) =>
        CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark;

    // The anchors of a document of the repository: each heading's slug, and every `<a id="...">` and `<a name="...">`.
    public static ISet<string> Anchors(string file) => Load(file).AnchorSet;

    // Every inline link outside fenced blocks and inline code, with its line (from 1) and its target as written.
    public static IEnumerable<(string File, int Line, string Target)> Links() =>
        Files.SelectMany(file => Load(file).LinkList.Select(link => (file, link.Line, link.Target)));

    // The lines that use a form of Markdown these tests do not read, each with what to do about it: a link that is not
    // inline, a heading that is not ATX.  Anything such a line holds would pass unchecked.
    public static IEnumerable<(string File, int Line, string Message)> UnsupportedForms() =>
        Files.SelectMany(file => Load(file).Unsupported.Select(form => (file, form.Line, form.Message)));

    public static IEnumerable<(string File, int Line, string Info, string Text)> FencedBlocks() =>
        Files.SelectMany(file => Load(file).BlockList.Select(block => (file, block.Line, block.Info, block.Text)));

    // Whether a link names a file of this repository on GitHub, in any of the forms of its URLs that show one.
    public static bool IsRepositoryLink(string target) => RepositoryLinkPattern().IsMatch(target);

    // What is wrong with the form of a link to the repository on GitHub: it must be `blob/main/`, the URL that does
    // not change with a branch or a commit.  Null when the form is right, and for a link to anything else.
    public static string? RepositoryLinkProblem(string target) =>
        RepositoryLinkPattern().Match(target) is { Success: true } match && !IsMain(match)
            ? $"use {RepositoryUrl}<path>: only blob/main and tree/main are followed"
            : null;

    private static bool IsMain(Match link) =>
        !link.Groups[1].Value.Equals("raw", StringComparison.OrdinalIgnoreCase) && link.Groups[2].Value == "main";

    // Where a link of `file` goes: a path of the repository with '/' and no trailing '/', and the anchor with no '#'.
    // Null when the link goes elsewhere: another site, or a scheme such as mailto.
    public static (string Path, string Anchor)? Resolve(string file, string target)
    {
        var hash = target.IndexOf('#', StringComparison.Ordinal);
        var path = Uri.UnescapeDataString(hash < 0 ? target : target[..hash]);
        var anchor = hash < 0 ? "" : Uri.UnescapeDataString(target[(hash + 1)..]);

        if (IsRepositoryLink(target))
        {
            var match = RepositoryLinkPattern().Match(path);
            return IsMain(match) ? (match.Groups[3].Value.TrimEnd('/'), anchor) : null;
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

    // Reads the lines of one document.  Internal for the tests of this reading, which give it lines of their own.
    internal static Document Read(string[] lines)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var links = new List<(int Line, string Target)>();
        var blocks = new List<(int Line, string Info, string Text)>();
        var unsupported = new List<(int Line, string Message)>();

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

            if (OpeningPattern().Match(line) is { Success: true } opening && IsOpening(opening))
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

            // What is inside inline code is not Markdown: it makes no link and no anchor.
            var text = CodeSpanPattern().Replace(line, " ");

            if (HeadingPattern().Match(line) is { Success: true } heading)
            {
                var slug = Slug(heading.Groups[1].Value);
                var seen = counts.GetValueOrDefault(slug);
                counts[slug] = seen + 1;
                _ = anchors.Add(seen == 0 ? slug : $"{slug}-{seen}");
            }
            else if (index > 0 && IsSetextUnderline(lines[index - 1], line))
            {
                unsupported.Add(
                    (number, "a heading underlined with '=' or '-': use '#' headings, whose anchors are read")
                );
            }

            foreach (Match anchor in AnchorPattern().Matches(text))
            {
                _ = anchors.Add(anchor.Groups[1].Success ? anchor.Groups[1].Value : anchor.Groups[2].Value);
            }

            var linkMatches = LinkTargetPattern().Matches(text);
            foreach (Match link in linkMatches)
            {
                links.Add((number, link.Groups[1].Success ? link.Groups[1].Value : link.Groups[2].Value));
            }

            if (UnsupportedLinkPattern().IsMatch(text))
            {
                unsupported.Add((number, "a reference link, an autolink or an href: user docs use inline links only"));
            }
            else if (CountOf(text, "](") != linkMatches.Count)
            {
                unsupported.Add((number, "a link that is not read: user docs use inline links, [text](target)"));
            }
        }

        // A fence that is never closed runs to the end of the file.
        if (fence is { } unclosed)
        {
            blocks.Add((unclosed.Line, unclosed.Info, string.Join('\n', content)));
        }

        return new Document(anchors, links, blocks, unsupported);
    }

    // A backtick fence's info string has no backtick: otherwise the line is a code span.
    private static bool IsOpening(Match opening) =>
        opening.Groups[2].Value[0] != '`' || !opening.Groups[3].Value.Contains('`', StringComparison.Ordinal);

    // A line of '=' or '-' under a line of text makes the text a heading.  A list item, a table, a quote or a heading
    // above it is not text of a paragraph, and '---' under a blank line is a rule.
    private static bool IsSetextUnderline(string above, string line) =>
        SetextPattern().IsMatch(line) && above.Trim().Length > 0 && !NotParagraphPattern().IsMatch(above);

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (
            var at = text.IndexOf(part, StringComparison.Ordinal);
            at >= 0;
            at = text.IndexOf(part, at + 1, StringComparison.Ordinal)
        )
        {
            count++;
        }

        return count;
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

    internal sealed record Document(
        ISet<string> AnchorSet,
        IReadOnlyList<(int Line, string Target)> LinkList,
        IReadOnlyList<(int Line, string Info, string Text)> BlockList,
        IReadOnlyList<(int Line, string Message)> Unsupported
    );

    // The start of a fenced block: three or more backticks or tildes, then the info string.
    [GeneratedRegex(@"^( *)(`{3,}|~{3,})(.*)$")]
    private static partial Regex OpeningPattern();

    [GeneratedRegex(@"^ {0,3}#{1,6}[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex HeadingPattern();

    // An `id` or `name` attribute of an `<a>`, in either quote and among others.
    [GeneratedRegex(@"<a\b[^>]*?\s(?:id|name)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorPattern();

    // A code span: a run of backticks, then the same run again.
    [GeneratedRegex(@"(?<!`)(`+)(?!`).+?(?<!`)\1(?!`)")]
    private static partial Regex CodeSpanPattern();

    // The target of an inline link or image, after `](`: written between < and >, where it may hold spaces, or not,
    // and an optional title in either quote.
    [GeneratedRegex(@"\]\(\s*(?:<([^>]*)>|([^)\s<]*))(?:\s+(?:""[^""]*""|'[^']*'))?\s*\)")]
    private static partial Regex LinkTargetPattern();

    // A reference definition at the start of a line, an `href` attribute or an autolink.
    [GeneratedRegex(@"^\s{0,3}\[[^\]]+\]:\s|\bhref\s*=|<[A-Za-z][A-Za-z0-9+.\-]*:[^\s<>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex UnsupportedLinkPattern();

    [GeneratedRegex(@"^ {0,3}(?:=+|-+)[ \t]*$")]
    private static partial Regex SetextPattern();

    [GeneratedRegex(@"^\s*(?:#|>|[-*+]\s|\d+[.)]\s|\||<|```|~~~|([-*_])(?:[ \t]*\1){2,}[ \t]*$)")]
    private static partial Regex NotParagraphPattern();

    // Where the repository is on GitHub: the kind of URL, the ref and the path.  GitHub ignores the case of the owner
    // and the repository.
    [GeneratedRegex(
        @"^https://github\.com/mbcrawfo/SqlSource/(blob|tree|raw)/([^/#?]+)(?:/([^#?]*))?",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex RepositoryLinkPattern();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:")]
    private static partial Regex SchemePattern();
}
