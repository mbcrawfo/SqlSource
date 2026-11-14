using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The manual's site has a fixed shape: the landing page and the errors at the root of site/, and every other page in
// one of three folders, start/, guides/ and reference/.  Each database that can be described has a folder of
// site/reference/databases/ with the same five pages, a tab group that gives a tab to one database gives one to each,
// and a tab for a database has the folder's name as its id.  Each limit of a page, a `## Limits` section and each
// `limit-` anchor in it, is listed on site/guides/troubleshooting.md, for a reader who has a symptom and no error id.
public sealed partial class SiteStructureTests
{
    // The pages that wait at the root of site/ for their phase of the documentation overhaul to move them.  The list
    // only shrinks: the phase that moves a page removes its name.
    internal static readonly IReadOnlyList<string> PagesNotYetMoved =
    [
        "concepts.md",
        "generator.md",
        "recipes.md",
        "settings.md",
        "tool.md",
    ];

    private const string Troubleshooting = "site/guides/troubleshooting.md";

    // The folder of the made-up page of a test, written so that InboundSiteReferenceTests does not read it as a
    // reference to a page of the site.
    private const string MadeUpFolder = "site/a";

    private static readonly IReadOnlyList<string> RootPages = ["index.md", "diagnostics.md"];

    private static readonly IReadOnlyList<string> Folders = ["start", "guides", "reference"];

    private const string Databases = "site/reference/databases";

    private const string SupportedVersionsPage = $"{Databases}/index.md";

    // The dialect of each database folder, for the tests of made-up lines.
    private static readonly IReadOnlyDictionary<string, string> MadeUpDialects = new Dictionary<string, string>(
        StringComparer.Ordinal
    )
    {
        ["postgresql"] = "postgres",
        ["sqlserver"] = "mssql",
    };

    private static readonly IReadOnlyList<string> DatabasePages =
    [
        "index.md",
        "nullability.md",
        "types.md",
        "libraries.md",
        "query-methods.md",
    ];

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

    // Fails when a folder directly in site/reference/databases/ lacks one of its five pages or holds a file or folder
    // beside them, a file of an agent aside, and when fewer than two such folders are found, so that a reading that
    // finds none cannot pass.
    [Fact]
    public void DatabaseFolders_EachHoldsTheFivePages()
    {
        var databases = DatabaseNames();
        databases.Count.ShouldBeGreaterThanOrEqualTo(2, $"{Databases}/ should hold a folder for each database");

        databases
            .SelectMany(database =>
            {
                var folder = $"{Databases}/{database}";
                var entries = Directory
                    .EnumerateFileSystemEntries(RepositoryRoot.Path(folder))
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Where(name =>
                        name != "AGENTS.md"
                        && !name.StartsWith("CLAUDE", StringComparison.Ordinal)
                        // A file the system makes, such as .DS_Store, is not in the repository.
                        && !name.StartsWith('.')
                    )
                    .ToList();

                return DatabasePages
                    .Except(entries, StringComparer.Ordinal)
                    .Select(name => $"{folder}/ has no {name}: each database folder holds the same five pages")
                    .Concat(
                        entries
                            .Except(DatabasePages, StringComparer.Ordinal)
                            .Select(name =>
                                $"{folder}/{name} is not one of the five pages of a database folder: "
                                + string.Join(", ", DatabasePages)
                            )
                    );
            })
            .ShouldBeEmpty();
    }

    // Fails when a tab group of a page of the site gives a tab to one database folder of site/reference/databases/ and
    // not to another, a sample for one described database alone, or when a tab names a database by an id that is not
    // its folder's name: the folder's name in another case, or the database's dialect.
    [Fact]
    public void TabGroups_ThatNameADatabase_NameEachByItsFolder()
    {
        var dialects = DatabaseDialects();
        UserDocs
            .SitePages.SelectMany(page =>
                TabGroupProblems(page, File.ReadAllLines(RepositoryRoot.Path(page)), dialects)
            )
            .ShouldBeEmpty();
    }

    // Fails when TabGroupProblems misses a group with one database alone, reports a group with each database or with
    // none, reads a tab heading in a fenced block, or takes two groups for one: groups parted by another heading, by a
    // `---` line, and groups of tab headings of two levels.
    [Fact]
    public void TabGroupProblems_OfMadeUpLines_FindPartialGroups() =>
        TabGroupProblems(
                $"{MadeUpFolder}/page.md",
                [
                    "# Page",
                    "",
                    "## [PostgreSQL](#tab/postgresql)",
                    "",
                    "Alone.",
                    "",
                    "## Between",
                    "",
                    "## [PostgreSQL](#tab/postgresql/npgsql8)",
                    "",
                    "```markdown",
                    "## Inside a block",
                    "```",
                    "",
                    "## [SQL Server](#tab/sqlserver)",
                    "",
                    "## Next",
                    "",
                    "## [Command line](#tab/cli)",
                    "",
                    "## [MSBuild](#tab/msbuild)",
                    "",
                    "## Last",
                    "",
                    "### [SQL Server](#tab/sqlserver)",
                    "",
                    "#### [PostgreSQL](#tab/postgresql)",
                    "",
                    "---",
                    "",
                    "#### [SQL Server](#tab/sqlserver)",
                ],
                MadeUpDialects
            )
            .ShouldBe([
                $"{MadeUpFolder}/page.md:3: the tab group has tabs for postgresql and none for sqlserver: give each "
                    + "database folder of site/reference/databases/ its tab",
                $"{MadeUpFolder}/page.md:25: the tab group has tabs for sqlserver and none for postgresql: give each "
                    + "database folder of site/reference/databases/ its tab",
                $"{MadeUpFolder}/page.md:27: the tab group has tabs for postgresql and none for sqlserver: give each "
                    + "database folder of site/reference/databases/ its tab",
                $"{MadeUpFolder}/page.md:31: the tab group has tabs for sqlserver and none for postgresql: give each "
                    + "database folder of site/reference/databases/ its tab",
            ]);

    // Fails when TabGroupProblems misses a tab id that is a database folder's name in another case, or a database's
    // dialect in its own case or another, the first segment of a dependent tab's id included, or reports a tab id
    // that is the folder's name or names no database.
    [Fact]
    public void TabGroupProblems_OfMadeUpLines_FindTabIdsThatAreNotTheFolderName() =>
        TabGroupProblems(
                $"{MadeUpFolder}/page.md",
                [
                    "## [PostgreSQL](#tab/postgres)",
                    "",
                    "## [SQL Server](#tab/SqlServer)",
                    "",
                    "## Between",
                    "",
                    "## [PostgreSQL](#tab/PostgreSQL)",
                    "",
                    "## [SQL Server](#tab/mssql)",
                    "",
                    "## Next",
                    "",
                    "## [PostgreSQL](#tab/postgres/npgsql8)",
                    "",
                    "## [SQL Server](#tab/MSSQL)",
                    "",
                    "## Then",
                    "",
                    "## [PostgreSQL](#tab/postgresql)",
                    "",
                    "## [SQL Server](#tab/sqlserver)",
                    "",
                    "## Last",
                    "",
                    "## [MySQL](#tab/mysql)",
                ],
                MadeUpDialects
            )
            .ShouldBe([
                MisnamedTab(1, "postgres", "postgresql"),
                MisnamedTab(3, "SqlServer", "sqlserver"),
                MisnamedTab(7, "PostgreSQL", "postgresql"),
                MisnamedTab(9, "mssql", "sqlserver"),
                MisnamedTab(13, "postgres", "postgresql"),
                MisnamedTab(15, "MSSQL", "sqlserver"),
            ]);

    private static string MisnamedTab(int line, string id, string folder) =>
        $"{MadeUpFolder}/page.md:{line}: the tab id '{id}' names the database {folder}: use the folder's name, "
        + $"`{folder}`, as the tab id";

    // The names of the folders directly in site/reference/databases/, which are the databases that can be described.
    internal static IReadOnlyList<string> DatabaseNames() =>
        [
            .. Directory
                .EnumerateDirectories(RepositoryRoot.Path(Databases))
                .Select(Path.GetFileName)
                .OfType<string>()
                .Order(StringComparer.Ordinal),
        ];

    // The dialect of each database folder of site/reference/databases/, as the table of
    // site/reference/databases/index.md#supported-versions gives it: a row's `dialect` cell, without its backticks,
    // under the folder of the `<folder>/index.md` link of its Pages cell.
    internal static IReadOnlyDictionary<string, string> DatabaseDialects()
    {
        var table = SupportedVersionsTests.ReadTable(File.ReadAllLines(RepositoryRoot.Path(SupportedVersionsPage)));
        var headers = table.Headers.ToList();
        var dialect = headers.IndexOf("`dialect`");
        var pages = headers.IndexOf("Pages");
        dialect.ShouldBeGreaterThanOrEqualTo(0, $"{SupportedVersionsPage}#supported-versions has no column `dialect`");
        pages.ShouldBeGreaterThanOrEqualTo(0, $"{SupportedVersionsPage}#supported-versions has no column Pages");

        var dialects = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            row.Count.ShouldBeGreaterThan(
                Math.Max(dialect, pages),
                $"{SupportedVersionsPage}#supported-versions: the row '{row[0]}' has too few cells"
            );
            var folder = PagesCellFolder().Match(row[pages]);
            folder.Success.ShouldBeTrue(
                $"{SupportedVersionsPage}#supported-versions: the Pages cell of '{row[0]}', '{row[pages]}', links "
                    + "no <folder>/index.md"
            );
            dialects.Add(folder.Groups[1].Value, row[dialect].Trim('`'));
        }

        return dialects;
    }

    // The problems of the docfx tab groups of the lines, given the dialect of each database folder.  Each group that
    // has a tab for one of the databases and none for another is one problem, with the line of its first tab.  Each
    // tab whose id is not a folder's name but is, in any case, a folder's name or a database's dialect is one problem,
    // with its line.  A group is a run of tab headings, `#... [Text](#tab/<id>)`, of one level, with no other heading
    // and no `---` line between them, outside fenced blocks: docfx ends a group at either.  A dependent tab's id holds
    // slashes, and its first segment names the tab.
    internal static IReadOnlyList<string> TabGroupProblems(
        string page,
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, string> dialects
    )
    {
        var databases = dialects.Keys.Order(StringComparer.Ordinal).ToList();
        var problems = new List<string>();
        var groupLine = 0;
        var groupLevel = 0;
        var groupIds = new List<string>();

        void Close()
        {
            var present = databases.Where(database => groupIds.Contains(database, StringComparer.Ordinal)).ToList();
            var missing = databases.Except(present, StringComparer.Ordinal).ToList();
            if (present.Count > 0 && missing.Count > 0)
            {
                problems.Add(
                    $"{page}:{groupLine}: the tab group has tabs for {string.Join(", ", present)} and none for "
                        + $"{string.Join(", ", missing)}: give each database folder of {Databases}/ its tab"
                );
            }

            groupIds.Clear();
            groupLevel = 0;
        }

        var fenced = false;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced)
            {
                continue;
            }

            if (line.Trim() == "---")
            {
                Close();
                continue;
            }

            if (TabGroupHeading().Match(line) is not { Success: true } heading)
            {
                continue;
            }

            var level = heading.Groups[1].Length;
            if (TabHeadingText().Match(heading.Groups[2].Value) is not { Success: true } tab)
            {
                Close();
                continue;
            }

            if (level != groupLevel)
            {
                Close();
                groupLine = index + 1;
                groupLevel = level;
            }

            var id = tab.Groups[1].Value;
            groupIds.Add(id);
            if (!databases.Contains(id, StringComparer.Ordinal))
            {
                var named = dialects
                    .Where(pair =>
                        string.Equals(id, pair.Key, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(id, pair.Value, StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(pair => pair.Key)
                    .FirstOrDefault();
                if (named is not null)
                {
                    problems.Add(
                        $"{page}:{index + 1}: the tab id '{id}' names the database {named}: use the folder's name, "
                            + $"`{named}`, as the tab id"
                    );
                }
            }
        }

        Close();
        return problems;
    }

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

    // Fails when an h3 under a `## Limits` heading, on any page, does not stand under its own `limit-` anchor: a limit
    // with no such anchor could have no row in site/guides/troubleshooting.md and pass the test above.
    [Fact]
    public void Limits_EachStandsUnderALimitAnchor() =>
        UserDocs
            .SitePages.SelectMany(page => UnanchoredLimits(page, File.ReadAllLines(RepositoryRoot.Path(page))))
            .ShouldBeEmpty();

    // Fails when UnanchoredLimits misses an h3 of `## Limits` with no anchor, or with an anchor that does not start
    // with `limit-`, or reports one that has its anchor, an h3 under another h2, or a line inside a fenced block.
    [Fact]
    public void UnanchoredLimits_OfMadeUpLines_AreFound() =>
        UnanchoredLimits(
                $"{MadeUpFolder}/page.md",
                [
                    "## Other",
                    "",
                    "### Not a limit",
                    "",
                    "<a id=\"limits\"></a>",
                    "",
                    "## Limits",
                    "",
                    "<a id=\"limit-ok\"></a>",
                    "",
                    "### Has its anchor",
                    "",
                    "### No anchor",
                    "",
                    "<a id=\"list-in-aggregate\"></a>",
                    "",
                    "### Another anchor",
                    "",
                    "```sql fragment",
                    "### inside a block",
                    "```",
                    "",
                    "## After",
                    "",
                    "### Not a limit either",
                ]
            )
            .ShouldBe([
                $"{MadeUpFolder}/page.md:13: the limit '### No anchor' stands under no <a id=\"limit-...\"> line",
                $"{MadeUpFolder}/page.md:17: the limit '### Another anchor' stands under no <a id=\"limit-...\"> line",
            ]);

    // Fails when the reading finds no site/index.md or troubleshooting page, no page with `## Limits`, fewer than two
    // `limit-` anchors, or no link of the troubleshooting page, and when the dialects read from the table of supported
    // versions are not one for each database folder, or one is empty: a reading that finds nothing would make the
    // tests above pass.
    [Fact]
    public void Reading_OfTheSite_FindsPagesLimitsLinksAndDialects()
    {
        UserDocs.SitePages.ShouldContain("site/index.md");
        UserDocs.SitePages.ShouldContain(Troubleshooting);
        PagesWithLimits().ShouldNotBeEmpty();
        LimitAnchors().Count.ShouldBeGreaterThanOrEqualTo(2);
        TroubleshootingTargets().ShouldNotBeEmpty();

        var dialects = DatabaseDialects();
        dialects.Count.ShouldBeGreaterThanOrEqualTo(
            2,
            $"{SupportedVersionsPage}#supported-versions should have a row for each database folder"
        );
        dialects
            .Keys.Order(StringComparer.Ordinal)
            .ShouldBe(
                DatabaseNames(),
                ignoreOrder: false,
                customMessage: $"{SupportedVersionsPage}#supported-versions should link the index.md of each folder "
                    + $"of {Databases}/"
            );
        dialects.Values.ShouldAllBe(value => value.Length > 0, "each `dialect` cell should name a dialect");
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

    // Each h3 of a `## Limits` section, outside fenced blocks, whose last line above that is not blank is not a
    // `limit-` anchor: one problem for each, with its line.
    internal static IReadOnlyList<string> UnanchoredLimits(string page, IReadOnlyList<string> lines)
    {
        var problems = new List<string>();
        var fenced = false;
        var inLimits = false;
        var above = "";
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }
            else if (!fenced && line.StartsWith('#'))
            {
                if (LimitsHeading().IsMatch(line))
                {
                    inLimits = true;
                }
                else if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    if (inLimits && !LimitAnchorLine().IsMatch(above))
                    {
                        problems.Add(
                            $"{page}:{index + 1}: the limit '{line}' stands under no <a id=\"limit-...\"> line"
                        );
                    }
                }
                else if (!line.StartsWith("####", StringComparison.Ordinal))
                {
                    inLimits = false;
                }
            }

            if (line.Trim().Length > 0)
            {
                above = line.Trim();
            }
        }

        return problems;
    }

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

    // An ATX heading: its level and its text.
    [GeneratedRegex(@"^ {0,3}(#{1,6})[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex TabGroupHeading();

    // The text of a docfx tab heading, whose whole text links to `#tab/<id>`: the first segment of the id.
    [GeneratedRegex(@"^\[[^\]]*\]\(#tab/([^/)\s]+)[^)\s]*\)$")]
    private static partial Regex TabHeadingText();

    // The folder of the link of a Pages cell, `[PostgreSQL](postgresql/index.md)`.
    [GeneratedRegex(@"\]\(([^/()\s]+)/index\.md\)")]
    private static partial Regex PagesCellFolder();

    [GeneratedRegex(@"^##\s+Limits\s*$")]
    private static partial Regex LimitsHeading();

    [GeneratedRegex(@"^<a id=""limit-[^""]+""></a>$")]
    private static partial Regex LimitAnchorLine();
}
