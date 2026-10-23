using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Documentation;

// The links of the documents a user reads.  A link that goes to a file or an anchor that is not there leaves the
// reader at the top of a long page, or on a page that is not found, so each is followed here.
public sealed class UserDocsLinksTests
{
    private const string Manual = "docs/manual/";

    // The two readmes are packed into NuGet packages and shown on nuget.org, away from the repository.
    private static readonly string[] PackedReadmes = ["README.md", "src/SqlSource.Tool/README.md"];

    // What a user's document does not send the reader into: the notes of the project's own work.
    private static readonly string[] InternalFolders =
    [
        "docs/tech-debt/",
        "docs/superpowers/",
        "docs/deferred/",
        "src/",
    ];

    // The tool's readme is a part of the project that a user reads.
    private const string ToolReadme = "src/SqlSource.Tool/README.md";

    [Theory]
    [InlineData("Projects that share internals", "projects-that-share-internals")]
    [InlineData("The `Parameters` property", "the-parameters-property")]
    [InlineData("What a dialect changes?", "what-a-dialect-changes")]
    [InlineData("A [linked](other.md#x) heading", "a-linked-heading")]
    [InlineData("Under_score and dash-ed", "under_score-and-dash-ed")]
    [InlineData("Cafe\u0301 au lait", "cafe\u0301-au-lait")]
    public void Slug_Heading_FollowsGitHub(string heading, string slug) => UserDocs.Slug(heading).ShouldBe(slug);

    [Fact]
    public void Anchors_HeadingsOfAPage_AreTheirSlugs()
    {
        var anchors = UserDocs.Anchors("docs/manual/diagnostics.md");

        anchors.ShouldContain("sqlsrc001");
        anchors.ShouldContain("sqlsrc236");
    }

    // A parser that finds nothing would make every other test of this folder pass.
    [Fact]
    public void Links_OfTheUserDocs_AreFound()
    {
        UserDocs.Files.ShouldNotBeEmpty();
        UserDocs.Links().ShouldNotBeEmpty();
        UserDocs.Links().Select(link => link.File).Distinct().ShouldContain("README.md");
        UserDocs.Links().Select(link => link.File).Distinct().ShouldContain("docs/manual/diagnostics.md");
    }

    // The tests read inline links and "#" headings.  Another form is not read, and what it names would not be checked.
    [Fact]
    public void UnsupportedForms_OfTheUserDocs_AreNone() =>
        UserDocs.UnsupportedForms().Select(form => $"{form.File}:{form.Line}: {form.Message}").ShouldBeEmpty();

    [Fact]
    public void RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist() =>
        Problems((file, target) => UserDocs.IsRepositoryLink(target) ? null : Follow(file, target)).ShouldBeEmpty();

    [Fact]
    public void RepositoryUrls_OfTheUserDocs_ReachAFileAndAnchorThatExist() =>
        Problems((file, target) => UserDocs.IsRepositoryLink(target) ? Follow(file, target) : null).ShouldBeEmpty();

    [Fact]
    public void Links_OfThePackedReadmes_AreOnlyAbsolute() =>
        UserDocs
            .Links()
            .Where(link => PackedReadmes.Contains(link.File))
            .Where(link => !link.Target.StartsWith("https://", StringComparison.Ordinal))
            .Select(link => $"{link.File}:{link.Line}: {link.Target}")
            .ShouldBeEmpty();

    [Fact]
    public void Links_OfTheUserDocs_ReachNoInternalDocument() =>
        UserDocs
            .Links()
            .Select(link => (link.File, link.Line, link.Target, Resolved: UserDocs.Resolve(link.File, link.Target)))
            .Where(link => link.Resolved is { } resolved && IsInternal(resolved.Path))
            .Select(link => $"{link.File}:{link.Line}: {link.Target}")
            .ShouldBeEmpty();

    [Fact]
    public void Manual_EntryPage_ListsEveryPage()
    {
        var entry = Manual + "README.md";
        var linked = UserDocs
            .Links()
            .Where(link => link.File == entry)
            .Select(link => UserDocs.Resolve(link.File, link.Target)?.Path)
            .ToHashSet();

        UserDocs
            .Files.Where(file => file.StartsWith(Manual, StringComparison.Ordinal) && file != entry)
            .Where(file => !linked.Contains(file))
            .ShouldBeEmpty();
    }

    private static bool IsInternal(string path) =>
        path != ToolReadme
        && InternalFolders.Any(folder =>
            (path + "/").StartsWith(folder, StringComparison.Ordinal)
            || path.StartsWith(folder, StringComparison.Ordinal)
        );

    // The links that `follow` has a complaint about, each as "file:line: target: complaint".
    private static string[] Problems(Func<string, string, string?> follow) =>
        [
            .. UserDocs
                .Links()
                .Select(link => (link.File, link.Line, link.Target, Complaint: follow(link.File, link.Target)))
                .Where(link => link.Complaint is not null)
                .Select(link => $"{link.File}:{link.Line}: {link.Target}: {link.Complaint}"),
        ];

    // What is wrong with a link: the file or folder is not there, or the anchor is not in the file.  Null when it
    // reaches what it names, and for a link that goes to another site.
    private static string? Follow(string file, string target)
    {
        if (UserDocs.RepositoryLinkProblem(target) is { } problem)
        {
            return problem;
        }

        if (UserDocs.Resolve(file, target) is not var (path, anchor))
        {
            return null;
        }

        if (path.StartsWith("..", StringComparison.Ordinal) || !UserDocs.Exists(path))
        {
            return $"no file or folder '{path}'";
        }

        return
            anchor.Length > 0
            && path.EndsWith(".md", StringComparison.Ordinal)
            && !UserDocs.Anchors(path).Contains(anchor)
            ? $"no anchor '{anchor}' in '{path}'"
            : null;
    }
}
