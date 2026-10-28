using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.Documentation;

// The limits of the root AGENTS.md's guidelines on every AGENTS.md: a rule of at most 80 words, and a file of at most
// 8 KB, or 16 KB for the root's.  A rule is a list item or a paragraph with the lines it wraps onto, so a long rule
// cannot pass by being wrapped or by being joined with others on one line.
public sealed partial class AgentsFileTests
{
    private const int MaxWords = 80;

    private const int MaxBytes = 8 * 1024;

    private const int MaxRootBytes = 16 * 1024;

    // The folders that hold no AGENTS.md of this repository: git's, the worktrees and settings of local tools, and
    // what a build or a test run writes.
    private static readonly HashSet<string> SkippedFolders =
    [
        with(StringComparer.Ordinal),
        ".git",
        ".claude",
        ".vs",
        ".idea",
        "bin",
        "obj",
        "node_modules",
        "TestResults",
    ];

    // The repository-relative paths, with '/', of every AGENTS.md.
    private static readonly IReadOnlyList<string> Files =
    [
        .. Find(RepositoryRoot.Path(), "").Order(StringComparer.Ordinal),
    ];

    // A finder that finds nothing would make the other tests pass.
    [Fact]
    public void Files_OfTheRepository_AreFound()
    {
        Files.ShouldContain("AGENTS.md");
        Files.ShouldContain(".github/AGENTS.md");
        Files.ShouldContain("src/SqlSource/AGENTS.md");
    }

    [Fact]
    public void Rules_OfEveryAgentsFile_AreAtMost80Words() =>
        Files
            .SelectMany(file =>
                Rules(File.ReadAllText(RepositoryRoot.Path(file.Split('/')))).Select(rule => (file, rule))
            )
            .Where(found => IsTooLong(found.rule))
            .Select(found => $"{found.file}:{found.rule.Line}: {found.rule.Words} words: {found.rule.Start}")
            .ShouldBeEmpty();

    [Fact]
    public void Size_OfEveryAgentsFile_IsWithinItsLimit() =>
        Files
            .Select(file => (file, bytes: new FileInfo(RepositoryRoot.Path(file.Split('/'))).Length))
            .Where(found => IsTooLarge(found.file, found.bytes))
            .Select(found => $"{found.file}: {found.bytes} bytes")
            .ShouldBeEmpty();

    [Fact]
    public void Rules_OfAText_AreItsItemsAndParagraphsWithTheLinesTheyWrapOnto()
    {
        const string Text = """
            # Heading

            An opening paragraph
            wrapped onto two lines.

            - **One rule**, wrapped
                onto a second line.
            - Two rule.
                ```csharp
                var ignored = "a fenced block counts no words";
                ```
            1. Three rule.
            | a | table | row of words |
            ~~~
            words in a tilde fence
            ~~~
            Four rule.

            > Maintenance: a footer.
            """;

        Rules(Text).Select(rule => (rule.Line, rule.Words)).ShouldBe([(3, 7), (6, 7), (8, 2), (12, 2), (17, 2)]);
    }

    // The checks above run over the real files, none of which is at a limit, so the limits are pinned here.
    [Theory]
    [InlineData(80, false)]
    [InlineData(81, true)]
    public void IsTooLong_RuleOfWords_IsTrueOnlyOver80(int words, bool expected) =>
        IsTooLong(Rules("- " + string.Join(' ', Enumerable.Repeat("word", words))).Single()).ShouldBe(expected);

    [Theory]
    [InlineData("src/SqlSource/AGENTS.md", 8192, false)]
    [InlineData("src/SqlSource/AGENTS.md", 8193, true)]
    [InlineData("AGENTS.md", 16384, false)]
    [InlineData("AGENTS.md", 16385, true)]
    public void IsTooLarge_FileOfBytes_IsTrueOnlyOverItsLimit(string file, long bytes, bool expected) =>
        IsTooLarge(file, bytes).ShouldBe(expected);

    private static bool IsTooLong(Rule rule) => rule.Words > MaxWords;

    private static bool IsTooLarge(string file, long bytes) => bytes > (file == "AGENTS.md" ? MaxRootBytes : MaxBytes);

    private static IEnumerable<string> Find(string folder, string relative)
    {
        if (File.Exists(Path.Combine(folder, "AGENTS.md")))
        {
            yield return relative.Length == 0 ? "AGENTS.md" : $"{relative}/AGENTS.md";
        }

        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            if (SkippedFolders.Contains(name))
            {
                continue;
            }

            foreach (var file in Find(child, relative.Length == 0 ? name : $"{relative}/{name}"))
            {
                yield return file;
            }
        }
    }

    // The rules of a Markdown text, each with the number of its first line, its words outside fenced blocks, and the
    // start of its text.  A heading, a table row and the maintenance footer are not rules.
    private static List<Rule> Rules(string text)
    {
        var rules = new List<Rule>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Rule? current = null;
        var fenced = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();
            if (
                trimmed.StartsWith("```", StringComparison.Ordinal)
                || trimmed.StartsWith("~~~", StringComparison.Ordinal)
            )
            {
                fenced = !fenced;
                continue;
            }

            if (fenced)
            {
                continue;
            }

            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('|') || trimmed.StartsWith('>'))
            {
                current = Close(rules, current);
                continue;
            }

            if (current is null || ListItem().IsMatch(line))
            {
                _ = Close(rules, current);
                current = new Rule(index + 1, 0, trimmed.Length > 60 ? trimmed[..60] : trimmed);
            }

            // The marker of a list item is not a word of its rule.
            var content = ListItem().Replace(line, "", 1).Trim();
            current = current with { Words = current.Words + CountWords(content) };
        }

        _ = Close(rules, current);
        return rules;
    }

    private static Rule? Close(List<Rule> rules, Rule? current)
    {
        if (current is not null)
        {
            rules.Add(current);
        }

        return null;
    }

    private static int CountWords(string line) => line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    [GeneratedRegex(@"^\s*([-*+]|\d+\.)\s")]
    private static partial Regex ListItem();

    private sealed record Rule(int Line, int Words, string Start);
}
