using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Documentation;

// How UserDocs reads a document, on lines of its own: the other tests of this folder trust it, so a form it reads
// wrongly would pass them without checking what it holds.
public sealed class UserDocsReadingTests
{
    [Fact]
    public void Anchors_RepeatedHeading_GetsASuffix() =>
        UserDocs
            .Read(["# Title", "## Same", "## Same", "## Same"])
            .AnchorSet.ShouldBe(["title", "same", "same-1", "same-2"], ignoreOrder: true);

    [Theory]
    [InlineData("""<a id="one"></a>""", "one")]
    [InlineData("<a id='one'></a>", "one")]
    [InlineData("""<a name="one"></a>""", "one")]
    [InlineData("""<a class="x" id='one'>text</a>""", "one")]
    [InlineData("""<A ID="one"></A>""", "one")]
    public void Anchors_ExplicitAnchor_IsFound(string line, string anchor) =>
        UserDocs.Read([line]).AnchorSet.ShouldBe([anchor]);

    [Fact]
    public void Anchors_InsideInlineCode_AreNotFound() =>
        UserDocs.Read(["Write `<a id=\"one\"></a>` above a heading."]).AnchorSet.ShouldBeEmpty();

    [Fact]
    public void Anchors_InsideAFencedBlock_AreNotFound() =>
        UserDocs.Read(["```html", "<a id=\"one\"></a>", "# Not a heading", "```"]).AnchorSet.ShouldBeEmpty();

    [Fact]
    public void Links_InlineCodeAndFencedBlocks_AreNotLinks() =>
        UserDocs
            .Read(["`[a](b.md)` and [c](d.md)", "~~~", "[e](f.md)", "~~~"])
            .LinkList.Select(link => link.Target)
            .ShouldBe(["d.md"]);

    [Theory]
    [InlineData("[a](b.md \"Title\")", "b.md")]
    [InlineData("[a](b.md 'Title')", "b.md")]
    [InlineData("[a](<b c.md>)", "b c.md")]
    [InlineData("[a](<b c.md> 'Title')", "b c.md")]
    [InlineData("![a](b.png)", "b.png")]
    [InlineData("[a](#x)", "#x")]
    public void Links_InlineForms_AreRead(string line, string target)
    {
        var document = UserDocs.Read([line]);

        document.LinkList.Select(link => link.Target).ShouldBe([target]);
        document.Unsupported.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("[a]: https://example.com")]
    [InlineData("""<a href="https://example.com">a</a>""")]
    [InlineData("See <https://example.com>.")]
    [InlineData("""[a](b.md "unfinished title)""")]
    public void Unsupported_OtherLinkForms_AreReported(string line) =>
        UserDocs.Read(["Text", line]).Unsupported.Select(form => form.Line).ShouldBe([2]);

    [Fact]
    public void Unsupported_LinkFormsInsideCode_AreNotReported() =>
        UserDocs.Read(["`[a]: b` and `<https://example.com>`", "```", "[a]: b", "```"]).Unsupported.ShouldBeEmpty();

    [Theory]
    [InlineData("Title", "=====")]
    [InlineData("Title", "-----")]
    [InlineData("Title", "-")]
    [InlineData("```x``` text", "---")]
    [InlineData("<b>bold</b> text", "---")]
    [InlineData("""<a id="x"></a>""", "---")]
    public void Unsupported_SetextHeading_IsReported(string text, string underline) =>
        UserDocs.Read([text, underline]).Unsupported.Select(form => form.Line).ShouldBe([2]);

    [Theory]
    [InlineData("", "---")]
    [InlineData("- item", "---")]
    [InlineData("| a |", "---")]
    [InlineData("# Title", "---")]
    [InlineData("***", "---")]
    [InlineData("___", "---")]
    [InlineData("_ _ _", "---")]
    [InlineData("1. item", "---")]
    [InlineData("1) item", "---")]
    [InlineData("> quote", "---")]
    [InlineData("---", "---")]
    public void Unsupported_RuleOrTable_IsNotReported(string above, string line) =>
        UserDocs.Read([above, line]).Unsupported.ShouldBeEmpty();

    // The line above is the closing fence of a block, which is not a paragraph either.
    [Theory]
    [InlineData("```", "```")]
    [InlineData("~~~", "~~~")]
    [InlineData("````", "````  ")]
    public void Unsupported_RuleAfterAFencedBlock_IsNotReported(string opening, string closing) =>
        UserDocs.Read([opening, "text", closing, "---"]).Unsupported.ShouldBeEmpty();

    // The line above starts an HTML block, which is not a paragraph either.
    [Theory]
    [InlineData("<!-- note -->")]
    [InlineData("<details><summary>More</summary>")]
    [InlineData("""<div class="x">text""")]
    [InlineData("""<a id="x">""")]
    [InlineData("</a>")]
    public void Unsupported_RuleUnderAnHtmlBlock_IsNotReported(string above) =>
        UserDocs.Read([above, "---"]).Unsupported.ShouldBeEmpty();

    [Fact]
    public void Blocks_BacktickInTheInfoOfABacktickFence_IsNotAFence()
    {
        var document = UserDocs.Read(["```sql `x`", "[a](b.md)", "```sql", "SELECT 1;", "```"]);

        document.LinkList.Select(link => link.Target).ShouldBe(["b.md"]);
        document.BlockList.ShouldBe([(3, "sql", "SELECT 1;")]);
    }

    [Fact]
    public void Blocks_TildeFenceWithABacktickInItsInfo_IsAFence() =>
        UserDocs.Read(["~~~ a`b", "text", "~~~"]).BlockList.ShouldBe([(1, "a`b", "text")]);

    [Fact]
    public void Blocks_IndentedFence_LosesItsIndentationInTheText() =>
        UserDocs
            .Read(["  ```sql fragment", "  SELECT 1;", "    FROM t;", "  ```"])
            .BlockList.ShouldBe([(1, "sql fragment", "SELECT 1;\n  FROM t;")]);

    [Theory]
    [InlineData("https://github.com/mbcrawfo/SqlSource/blob/main/README.md#x", "README.md", "x")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/tree/main/docs/manual/", "docs/manual", "")]
    [InlineData("https://GitHub.com/MBCrawfo/sqlsource/blob/main/LICENSE", "LICENSE", "")]
    [InlineData("../manual/README.md#y", "docs/manual/README.md", "y")]
    [InlineData("#z", "docs/manual/page.md", "z")]
    public void Resolve_LinkToThisRepository_GivesItsPathAndAnchor(string target, string path, string anchor) =>
        UserDocs.Resolve("docs/manual/page.md", target).ShouldBe((path, anchor));

    [Theory]
    [InlineData("https://github.com/mbcrawfo/SqlSource/blob/main/README.md")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/tree/main/docs")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/tree/main")]
    public void RepositoryLinkProblem_BlobOrTreeOfMain_IsNone(string target)
    {
        UserDocs.IsRepositoryLink(target).ShouldBeTrue();
        UserDocs.RepositoryLinkProblem(target).ShouldBeNull();
    }

    [Theory]
    [InlineData("https://github.com/mbcrawfo/SqlSource/blob/v1.0.0/README.md")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/blob/0123abc/README.md")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/tree/dev/docs")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/raw/main/schemas/sidecar-v1.schema.json")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/blob/Main/README.md")]
    public void RepositoryLinkProblem_OtherRefOrRaw_SaysToUseBlobMain(string target)
    {
        UserDocs.IsRepositoryLink(target).ShouldBeTrue();
        UserDocs.RepositoryLinkProblem(target).ShouldNotBeNull().ShouldContain("blob/main");
    }

    [Theory]
    [InlineData("https://github.com/mbcrawfo/SqlSource")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/issues/3")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/actions/workflows/build.yml")]
    [InlineData("https://github.com/mbcrawfo/SqlSource/pulls")]
    [InlineData("https://www.npgsql.org/doc/connection-string-parameters.html")]
    [InlineData("mailto:someone@example.com")]
    public void Resolve_OtherSites_AreExternal(string target)
    {
        UserDocs.IsRepositoryLink(target).ShouldBeFalse();
        UserDocs.RepositoryLinkProblem(target).ShouldBeNull();
        UserDocs.Resolve("README.md", target).ShouldBeNull();
    }
}
