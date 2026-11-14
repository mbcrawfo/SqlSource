using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The manual's site has a fixed shape: the landing page and the errors at the root of site/, and every other page in
// one of three folders, start/, guides/ and reference/.  Each limit of a page, a `## Limits` section and each
// `limit-` anchor in it, is listed on site/guides/troubleshooting.md, for a reader who has a symptom and no error id.
public sealed partial class SiteStructureTests
{
    // The pages that wait at the root of site/ for their phase of the documentation overhaul to move them.  The list
    // only shrinks: the phase that moves a page removes its name.
    internal static readonly IReadOnlyList<string> PagesNotYetMoved =
    [
        "concepts.md",
        "generator.md",
        "postgresql.md",
        "recipes.md",
        "settings.md",
        "sqlserver.md",
        "tool.md",
    ];

    private const string Troubleshooting = "site/guides/troubleshooting.md";

    private static readonly IReadOnlyList<string> RootPages = ["index.md", "diagnostics.md"];

    private static readonly IReadOnlyList<string> Folders = ["start", "guides", "reference"];

    // Fails when a .md file stands directly in site/ and is not index.md, diagnostics.md, a page not yet moved or a
    // file of an agent, and when a name of PagesNotYetMoved has no file, so that a moved page cannot stay listed.
    [Fact]
    public void RootPages_AreTheLandingPageTheErrorsAndThePagesNotYetMoved()
    {
        var files = Directory
            .EnumerateFiles(RepositoryRoot.Path(UserDocs.Site), "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => name != "AGENTS.md" && !name.StartsWith("CLAUDE", StringComparison.Ordinal))
            .ToList();

        files
            .Except([.. RootPages, .. PagesNotYetMoved], StringComparer.Ordinal)
            .Select(name => $"site/{name} is a page at the root: put it in start/, guides/ or reference/")
            .Concat(
                PagesNotYetMoved
                    .Except(files, StringComparer.Ordinal)
                    .Select(name => $"site/{name} is in PagesNotYetMoved and is not at the root: remove its name")
            )
            .ShouldBeEmpty();
    }

    // Fails when a folder directly in site/ is not start, guides or reference.
    [Fact]
    public void Folders_AreTheThreeParts() =>
        Directory
            .EnumerateDirectories(RepositoryRoot.Path(UserDocs.Site))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Except(Folders, StringComparer.Ordinal)
            .Select(name => $"site/{name}/ is a folder of site/ other than start/, guides/ and reference/")
            .ShouldBeEmpty();

    // Fails when a page with a `## Limits` heading is not linked from site/guides/troubleshooting.md, or when a
    // `limit-` anchor, on any page, is named by no link of that page.
    [Fact]
    public void Limits_EachHasItsEntryInTroubleshooting()
    {
        var targets = TroubleshootingTargets();

        PagesWithLimits()
            .Where(page => !targets.Any(target => target.Path == page))
            .Select(page => $"{page} has a ## Limits section that {Troubleshooting} does not link")
            .Concat(
                LimitAnchors()
                    .Where(limit => !targets.Contains(limit))
                    .Select(limit => $"{limit.Path}#{limit.Anchor} is a limit that {Troubleshooting} does not link")
            )
            .ShouldBeEmpty();
    }

    // A reading that finds nothing would make the tests above pass.
    [Fact]
    public void Reading_OfTheSite_FindsPagesLimitsAndLinks()
    {
        UserDocs.SitePages.ShouldContain("site/index.md");
        UserDocs.SitePages.ShouldContain(Troubleshooting);
        PagesWithLimits().ShouldNotBeEmpty();
        LimitAnchors().Count.ShouldBeGreaterThanOrEqualTo(2);
        TroubleshootingTargets().ShouldNotBeEmpty();
    }

    // The pages whose text, outside fenced blocks, has a `## Limits` heading.
    private static List<string> PagesWithLimits() =>
        [
            .. UserDocs.SitePages.Where(page =>
            {
                var fenced = false;
                foreach (var line in File.ReadAllLines(RepositoryRoot.Path(page)))
                {
                    if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        fenced = !fenced;
                    }
                    else if (!fenced && LimitsHeading().IsMatch(line))
                    {
                        return true;
                    }
                }

                return false;
            }),
        ];

    private static List<(string Path, string Anchor)> LimitAnchors() =>
        [
            .. UserDocs.SitePages.SelectMany(page =>
                UserDocs
                    .Anchors(page)
                    .Where(anchor => anchor.StartsWith("limit-", StringComparison.Ordinal))
                    .Select(anchor => (page, anchor))
            ),
        ];

    // Where each link of site/guides/troubleshooting.md goes.
    private static HashSet<(string Path, string Anchor)> TroubleshootingTargets() =>
        [
            .. UserDocs
                .Links()
                .Where(link => link.File == Troubleshooting)
                .Select(link => UserDocs.Resolve(link.File, link.Target))
                .OfType<(string Path, string Anchor)>(),
        ];

    [GeneratedRegex(@"^##\s+Limits\s*$")]
    private static partial Regex LimitsHeading();
}
