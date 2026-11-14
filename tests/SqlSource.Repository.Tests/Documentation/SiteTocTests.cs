using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The top bar of the manual's site is site/toc.yml, and a folder of pages has a toc.yml of its own, its sidebar, that
// the entry for the folder leads to.  The landing page, site/index.md, is in no toc.yml.  docfx publishes a page that
// no toc.yml lists, with no warning and no way to reach the page but a link, and fails on an entry whose page is gone.
public sealed partial class SiteTocTests
{
    // The folder of the made-up pages of a test, written so that InboundSiteReferenceTests does not read them as
    // references to pages of the site.
    private const string MadeUpFolder = "site/a";

    // The site's root, which shows no sidebar.
    private const string LandingPage = "site/index.md";

    // Fails when a toc.yml of site/, at any depth, has an entry that names no page of the site or a folder with no
    // toc.yml, when a page is named by more than one entry, when a page but site/index.md is in no toc.yml, or when a
    // toc.yml but the root's is led to by no entry.
    [Fact]
    public void Toc_ListsEveryPageOnce_AndNothingElse()
    {
        var tocs = Directory
            .EnumerateFiles(RepositoryRoot.Path(UserDocs.Site), "toc.yml", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(RepositoryRoot.Path(), path).Replace('\\', '/'),
                File.ReadAllText,
                StringComparer.Ordinal
            );

        Problems(tocs, UserDocs.SitePages).ShouldBeEmpty();
    }

    // Fails when a problem of a nested set of toc.yml files, a folder with none, a gone page, a page named twice, a
    // toc.yml that two entries lead to, a page listed nowhere or a toc.yml that no entry leads to, goes unreported, or
    // when the landing page, which no toc.yml lists, is reported.
    [Fact]
    public void Problems_OfANestedSet_AreFound()
    {
        var tocs = new Dictionary<string, string>
        {
            ["site/toc.yml"] =
                "- name: A\n  href: a/\n- name: B\n  href: b/toc.yml\n- name: C\n  href: c/\n"
                + "- name: B2\n  href: b/\n",
            ["site/a/toc.yml"] =
                "- name: D\n  href: ../diagnostics.md\n- name: P\n  href: page.md\n- name: T\n  href: twice.md\n",
            ["site/b/toc.yml"] = "- name: T\n  href: ../a/twice.md\n- name: G\n  href: gone.md\n",
            ["site/e/toc.yml"] = "",
        };
        string[] pages =
        [
            "site/diagnostics.md",
            LandingPage,
            $"{MadeUpFolder}/page.md",
            $"{MadeUpFolder}/twice.md",
            $"{MadeUpFolder}/none.md",
        ];

        var problems = Problems(tocs, pages);

        problems.Count.ShouldBe(6);
        problems.ShouldContain(problem =>
            problem.Contains("site/b/toc.yml") && problem.Contains("more than one entry")
        );
        problems.ShouldContain(problem => problem.Contains("site/e/toc.yml") && problem.Contains("no entry leads"));
        problems.ShouldContain(problem => problem.Contains("site/toc.yml") && problem.Contains("c/"));
        problems.ShouldContain(problem => problem.Contains("site/b/toc.yml") && problem.Contains("gone.md"));
        problems.ShouldContain(problem =>
            problem.Contains($"{MadeUpFolder}/twice.md") && problem.Contains("more than one entry")
        );
        problems.ShouldContain(problem =>
            problem.Contains($"{MadeUpFolder}/none.md") && problem.Contains("no toc.yml")
        );
    }

    // Each problem is one line that names the file and the href, or the page.
    internal static IReadOnlyList<string> Problems(
        IReadOnlyDictionary<string, string> tocs,
        IReadOnlyList<string> pages
    )
    {
        var problems = new List<string>();
        var named = new List<string>();
        var ledBy = new List<string>();
        var led = new HashSet<string>(StringComparer.Ordinal) { "site/toc.yml" };
        foreach (var (toc, text) in tocs.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var folder = toc.Contains('/', StringComparison.Ordinal) ? toc[..toc.LastIndexOf('/')] : "";
            foreach (var match in text.Split('\n').Select(line => HrefPattern().Match(line)).Where(m => m.Success))
            {
                var href = match.Groups[1].Value;
                var target = Resolve(folder, href);
                if (href.EndsWith('/') || target.EndsWith("/toc.yml", StringComparison.Ordinal))
                {
                    var childToc = href.EndsWith('/') ? $"{target}toc.yml" : target;
                    _ = led.Add(childToc);
                    ledBy.Add(childToc);
                    if (!tocs.ContainsKey(childToc))
                    {
                        problems.Add($"{toc}: '{href}' names a folder with no toc.yml ({childToc})");
                    }
                }
                else if (!pages.Contains(target, StringComparer.Ordinal))
                {
                    problems.Add($"{toc}: '{href}' names no page ({target})");
                }
                else
                {
                    named.Add(target);
                }
            }
        }

        problems.AddRange(
            tocs.Keys.Where(toc => !led.Contains(toc))
                .Order(StringComparer.Ordinal)
                .Select(toc => $"{toc} is a toc.yml that no entry leads to")
        );
        problems.AddRange(
            named
                .GroupBy(page => page, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} is named by more than one entry")
        );
        problems.AddRange(
            ledBy
                .GroupBy(toc => toc, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} is led to by more than one entry")
        );
        problems.AddRange(
            pages
                .Except(named, StringComparer.Ordinal)
                .Where(page => page != LandingPage)
                .Select(page => $"{page} is in no toc.yml")
        );
        return problems;
    }

    // The path of an href that is read from the folder of its toc.yml, with its '..' and '.' segments resolved.
    private static string Resolve(string folder, string href)
    {
        var segments = new List<string>(folder.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var segment in href.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (segment is not ("." or ""))
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments) + (href.EndsWith('/') ? "/" : "");
    }

    [GeneratedRegex(@"^\s*-?\s*href:\s*(\S+)\s*$")]
    private static partial Regex HrefPattern();
}
