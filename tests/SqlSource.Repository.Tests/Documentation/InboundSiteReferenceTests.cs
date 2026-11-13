using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// Every reference to a page of the site from outside the user documents: a path `site/<path>.md`, with or without an
// anchor, and after `../` read from the folder of its file, and a URL of the published site.  It fails on a reference
// that names no page, or an anchor that is not on the page, in any text file that git tracks but `docs/superpowers/`
// (whose files record intent and are not kept current) and the user documents (`UserDocsLinksTests` follows those).
// A reference that names nothing is a broken reference: mend it.  Never add an exception to pass.
// The exceptions are these four and no other:
//   - UserDocsReadingTests.cs, whole: its test data is wrong on purpose.
//   - This file, whole: its test data is wrong on purpose.
//   - TestScopesTests.cs, `site/x.md`: a made-up path for a pattern test.
//   - ToolDiagnosticsTests.cs and Expected.cs, `diagnostics.html#sqlsrc`: the start of a help link that the code ends
//     with the digits of an id.
// The walk reads only what git tracks, as far as a test may know it: a folder that `.gitignore` leaves out, such as
// `.superpowers/`, holds briefs of local tools, and a reference in one would fail on one machine only.
public sealed partial class InboundSiteReferenceTests
{
    private static readonly string[] Extensions =
    [
        ".md",
        ".cs",
        ".yml",
        ".yaml",
        ".json",
        ".props",
        ".targets",
        ".csproj",
        ".sh",
        ".txt",
        ".sql",
    ];

    private static readonly string[] WholeFileExceptions =
    [
        "tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs",
        "tests/SqlSource.Repository.Tests/Documentation/InboundSiteReferenceTests.cs",
    ];

    private static readonly (string File, string Reference)[] ReferenceExceptions =
    [
        ("tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs", "site/x.md"),
        (
            "tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs",
            "https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc"
        ),
        ("tests/SqlSource.Tool.Tests/Expected.cs", "https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc"),
    ];

    // The references of the repository outside the user documents, `docs/superpowers/` and the whole-file exceptions,
    // before the exceptions of one reference.
    private static readonly IReadOnlyList<(string File, int Line, string Reference)> Found =
    [
        .. Walk(RepositoryRoot.Path())
            .Where(file =>
                !file.StartsWith("docs/superpowers/", StringComparison.Ordinal)
                && !UserDocs.Files.Contains(file)
                && !WholeFileExceptions.Contains(file)
            )
            .SelectMany(file =>
                References(File.ReadAllText(RepositoryRoot.Path(file.Split('/'))))
                    .Select(found => (file, found.Line, found.Reference))
            ),
    ];

    // Fails on a reference that names no page, or no anchor of its page, and is not one of the exceptions.
    [Fact]
    public void References_OfTheRepository_NameAPageAndAnchor() =>
        Found
            .Where(found => !ReferenceExceptions.Contains((found.File, found.Reference)))
            .Select(found => (found.File, found.Line, found.Reference, Problem: Problem(found.File, found.Reference)))
            .Where(found => found.Problem is not null)
            .Select(found => $"{found.File}:{found.Line}: {found.Reference}: {found.Problem}")
            .ShouldBeEmpty();

    // Fails when the scan finds no reference to `site/diagnostics.md`, in each form, or when an exception no longer
    // matches a string of its file: a scan that finds nothing would pass, and an exception must not outlive its string.
    [Fact]
    public void References_OfTheRepository_AreFound()
    {
        Found.ShouldContain(found => found.Reference.StartsWith("site/diagnostics.md", StringComparison.Ordinal));
        Found.ShouldContain(found =>
            found.Reference.StartsWith(
                "https://mbcrawfo.github.io/SqlSource/diagnostics.html",
                StringComparison.Ordinal
            )
        );
        foreach (var exception in ReferenceExceptions)
        {
            Found.ShouldContain(
                found => found.File == exception.File && found.Reference == exception.Reference,
                $"{exception.File} no longer holds {exception.Reference}"
            );
        }
    }

    // Fails when a form of reference is read wrongly, or one of its `../` is dropped.
    [Theory]
    [InlineData("`site/tool.md`", "site/tool.md")]
    [InlineData("[x](../../site/tool.md#exit-codes)", "../../site/tool.md#exit-codes")]
    [InlineData("[x](site/tool.md#exit-codes)", "./site/tool.md#exit-codes")]
    [InlineData("// see site/sub/probe.md#nope", "site/sub/probe.md#nope")]
    [InlineData(
        "See https://mbcrawfo.github.io/SqlSource/tool.html#exit-codes.",
        "https://mbcrawfo.github.io/SqlSource/tool.html#exit-codes"
    )]
    [InlineData(
        "(https://mbcrawfo.github.io/SqlSource/reference/cli.html)",
        "https://mbcrawfo.github.io/SqlSource/reference/cli.html"
    )]
    [InlineData(
        "\"https://mbcrawfo.github.io/SqlSource/diagnostics.html#\"",
        "https://mbcrawfo.github.io/SqlSource/diagnostics.html#"
    )]
    public void References_EachForm_IsFound(string line, string expected) =>
        References(line).Select(found => found.Reference).ShouldBe([expected]);

    // Fails when a text that is not a reference to a page of the site is read as one.
    [Theory]
    [InlineData("the manual's recipe")]
    [InlineData("the folder `site/`")]
    [InlineData("site/<page>.md")]
    [InlineData("`site/*.md`")]
    [InlineData("tests/site/x.md")]
    [InlineData("`tool.md`")]
    [InlineData("mysite/tool.md")]
    public void References_OtherWords_AreNotRead(string line) => References(line).ShouldBeEmpty();

    // A placeholder after the site's address reads as the root alone, which is no problem.
    [Fact]
    public void References_OfAPlaceholderUrl_ReadAsTheRoot()
    {
        var found = References("https://mbcrawfo.github.io/SqlSource/<page>.html").ToList();
        found.Select(reference => reference.Reference).ShouldBe(["https://mbcrawfo.github.io/SqlSource/"]);
        Problem("README.md", found[0].Reference).ShouldBeNull();
    }

    // Fails when a reference carries the line of another, or a line counted from 0 or split at a CR LF.
    [Fact]
    public void References_OfALine_CarryTheirLineNumbers() =>
        References("one\ntwo `site/tool.md`\r\nthree").Select(found => found.Line).ShouldBe([2]);

    // Fails when a reference that names no page of the site, or no anchor of it, has no problem, or a good one has,
    // including a `../` path that leaves the repository, misses `site/`, or lands on another page than it names.
    [Theory]
    [InlineData("README.md", "site/diagnostics.md#sqlsrc001", true)]
    [InlineData("README.md", "site/diagnostics.md#sqlsrc999", false)]
    [InlineData("README.md", "site/nope.md", false)]
    [InlineData("README.md", "site/diagnostics.md#", true)]
    [InlineData("docs/architecture/README.md", "../../site/diagnostics.md#sqlsrc001", true)]
    [InlineData("docs/architecture/README.md", "../site/diagnostics.md", false)]
    [InlineData("docs/architecture/README.md", "../../../site/diagnostics.md", false)]
    [InlineData("docs/README.md", "../site/diagnostics.md", true)]
    [InlineData("docs/README.md", "../../site/diagnostics.md", false)]
    [InlineData("README.md", "./site/diagnostics.md#sqlsrc001", true)]
    [InlineData("docs/architecture/README.md", "./site/diagnostics.md", false)]
    [InlineData("a/b/c.md", "../../site/nope.md", false)]
    [InlineData("README.md", "https://mbcrawfo.github.io/SqlSource/", true)]
    [InlineData("README.md", "https://mbcrawfo.github.io/SqlSource/diagnostics.html#", true)]
    [InlineData("README.md", "https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc999", false)]
    [InlineData("README.md", "https://mbcrawfo.github.io/SqlSource/reference/", false)]
    [InlineData("README.md", "https://mbcrawfo.github.io/SqlSource/AGENTS.html", false)]
    public void Problem_OfAReference_IsNullOnlyWhenItNamesAPageAndAnchor(string file, string reference, bool good) =>
        (Problem(file, reference) is null).ShouldBe(good);

    // Fails when the walk reads a file that git ignores or a binary file, or skips a tracked text file.
    [Fact]
    public void Walk_OfAFolder_SkipsWhatGitIgnores()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            foreach (
                var file in new[]
                {
                    "a.md",
                    ".github/b.yml",
                    "docs/c.cs",
                    ".superpowers/d.md",
                    ".claude/e.md",
                    "artifacts/site/f.md",
                    "src/bin/g.cs",
                    "TestResults/h.txt",
                    "CLAUDE.local.md",
                    "docs/i.png",
                }
            )
            {
                var path = Path.Combine(root, file);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "");
            }

            Walk(root).ShouldBe(["a.md", ".github/b.yml", "docs/c.cs"], ignoreOrder: true);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // The references of a text, each with its line (from 1).
    internal static IEnumerable<(int Line, string Reference)> References(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var found = PathReference()
                .Matches(lines[index])
                .Select(match =>
                    (
                        match.Index,
                        Reference: match.Value.StartsWith("site/", StringComparison.Ordinal)
                        && lines[index][..match.Index].EndsWith("](", StringComparison.Ordinal)
                            ? "./" + match.Value
                            : match.Value
                    )
                )
                .Concat(
                    UrlReference()
                        .Matches(lines[index])
                        .Select(match => (match.Index, Reference: match.Value.TrimEnd('.', ',', ';', ':')))
                )
                .OrderBy(match => match.Index);
            foreach (var (_, reference) in found)
            {
                yield return (index + 1, reference);
            }
        }
    }

    // What is wrong with a reference in a file, or null when it names a page and, if it has one, an anchor that exist.
    // A path is read from the root, unless it follows `../` or is the target of a Markdown link, which References gives
    // as `./site/...`: either makes it a relative link of the file's folder.
    internal static string? Problem(string file, string reference)
    {
        string path;
        string anchor;
        var relative =
            reference.StartsWith("../", StringComparison.Ordinal)
            || reference.StartsWith("./", StringComparison.Ordinal);
        if (relative || reference.StartsWith("site/", StringComparison.Ordinal))
        {
            var hash = reference.IndexOf('#', StringComparison.Ordinal);
            path = hash < 0 ? reference : reference[..hash];
            anchor = hash < 0 ? "" : reference[(hash + 1)..];
            if (relative)
            {
                var segments = file.Split('/').SkipLast(1).ToList();
                foreach (var segment in path.Split('/'))
                {
                    if (segment == ".")
                    {
                        continue;
                    }

                    if (segment != "..")
                    {
                        segments.Add(segment);
                    }
                    else if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }
                    else
                    {
                        return $"{reference} leaves the repository from {file}";
                    }
                }

                path = string.Join('/', segments);
                if (!path.StartsWith("site/", StringComparison.Ordinal))
                {
                    return $"{reference} from {file} reaches {path}, which is not under site/";
                }
            }
        }
        else
        {
            if (UserDocs.SiteLinkProblem(reference) is { } problem)
            {
                return problem;
            }

            if (UserDocs.Resolve("README.md", reference) is not { } resolved)
            {
                return null;
            }

            (path, anchor) = resolved;
            if (!UserDocs.SitePages.Contains(path))
            {
                return $"{path} is not a page that the site publishes";
            }
        }

        if (!File.Exists(RepositoryRoot.Path(path.Split('/'))))
        {
            return $"{path} is not a file of the repository";
        }

        return anchor.Length == 0 || UserDocs.Anchors(path).Contains(anchor) ? null : $"{path} has no anchor #{anchor}";
    }

    // The repository paths, with '/', of the text files under `root` that git would track, before the exceptions.
    internal static IEnumerable<string> Walk(string root) => Walk(root, "");

    private static IEnumerable<string> Walk(string folder, string relative)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var name = Path.GetFileName(file);
            if (
                name != "CLAUDE.local.md"
                && (name == ".editorconfig" || Extensions.Contains(Path.GetExtension(name), StringComparer.Ordinal))
            )
            {
                yield return relative.Length == 0 ? name : $"{relative}/{name}";
            }
        }

        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(child);
            if (
                RepositoryFolders.Skipped.Contains(name)
                || name == "artifacts"
                || (name.StartsWith('.') && name != ".github")
            )
            {
                continue;
            }

            foreach (var file in Walk(child, relative.Length == 0 ? name : $"{relative}/{name}"))
            {
                yield return file;
            }
        }
    }

    // `site/<path>.md` and an optional anchor, with no letter, digit, `_`, `-`, `.` or `/` before `site/`, unless it
    // follows `../`, which the reference then holds.
    [GeneratedRegex(
        @"(?:(?:\.\./)+|(?<![A-Za-z0-9_.\-/]))site/[A-Za-z0-9_.\-/]*\.md(?![A-Za-z0-9_])(?:#[A-Za-z0-9_\-]*)?"
    )]
    private static partial Regex PathReference();

    // The site's address and every character after it up to a space, a quote, a bracket or an angle bracket.
    [GeneratedRegex(@"https?://mbcrawfo\.github\.io/SqlSource[^\s`""'<>)\]\\]*", RegexOptions.IgnoreCase)]
    private static partial Regex UrlReference();
}
