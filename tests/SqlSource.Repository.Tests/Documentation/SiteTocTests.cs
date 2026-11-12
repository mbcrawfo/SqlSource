using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The sidebar of the manual's site is site/toc.yml.  docfx publishes a page that it leaves out, with no warning and no
// way to reach the page but a link, and fails on an entry whose page is gone.
public sealed partial class SiteTocTests
{
    [Fact]
    public void Toc_ListsEveryPageOnce_AndNothingElse()
    {
        var hrefs = File.ReadAllLines(RepositoryRoot.Path(UserDocs.Site, "toc.yml"))
            .Select(line => HrefPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => $"{UserDocs.Site}/{match.Groups[1].Value}")
            .ToList();

        hrefs.ShouldBeUnique();
        hrefs.Order(StringComparer.Ordinal).ShouldBe(UserDocs.SitePages.Order(StringComparer.Ordinal));
    }

    [GeneratedRegex(@"^\s*-?\s*href:\s*(\S+)\s*$")]
    private static partial Regex HrefPattern();
}
