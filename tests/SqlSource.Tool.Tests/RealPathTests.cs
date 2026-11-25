using System;
using System.IO;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class RealPathTests : IDisposable
{
    private readonly TempFolder _folder = new();

    // The temporary folder of macOS is itself under a link, /var to /private/var, so each expected path is made from
    // the real path of the test's folder.
    private readonly string _real;

    public RealPathTests()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), SymbolicLinks.SkipReason);
        _ = _folder.CreateFolder("Real/Sub");
        _ = _folder.WriteFile("Real/Sub/Q.sql");
        _real = RealPath.Of(_folder.Path);
    }

    public void Dispose() => _folder.Dispose();

    private string InReal(string relativePath) =>
        Path.Combine(_real, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private void Link(string relativePath, string target) =>
        _ = Directory.CreateSymbolicLink(_folder.PathOf(relativePath), target);

    // What the other tests expect from: the test's folder with no link on the way, and the folder itself.
    [Fact]
    public void Of_TheTestsFolder_HasNoLinkOnTheWay()
    {
        Directory.Exists(_real).ShouldBeTrue();
        File.Exists(Path.Combine(_real, "Real", "Sub", "Q.sql")).ShouldBeTrue();
        for (var folder = _real; folder is not null; folder = Path.GetDirectoryName(folder))
        {
            new DirectoryInfo(folder).LinkTarget.ShouldBeNull(folder);
        }
    }

    [Fact]
    public void Of_PathWithNoLink_IsThePath() =>
        RealPath.Of(InReal("Real/Sub/Q.sql")).ShouldBe(InReal("Real/Sub/Q.sql"));

    [Fact]
    public void Of_LinkToTheFolder_IsTheFolder()
    {
        Link("Link", _folder.PathOf("Real/Sub"));

        RealPath.Of(_folder.PathOf("Link/Q.sql")).ShouldBe(InReal("Real/Sub/Q.sql"));
    }

    [Fact]
    public void Of_LinkToAFolderAbove_IsThePathUnderThatFolder()
    {
        Link("Link", _folder.PathOf("Real"));

        RealPath.Of(_folder.PathOf("Link/Sub/Q.sql")).ShouldBe(InReal("Real/Sub/Q.sql"));
    }

    [Fact]
    public void Of_LinkToTheFile_IsTheFile()
    {
        _ = File.CreateSymbolicLink(_folder.PathOf("Link.sql"), _folder.PathOf("Real/Sub/Q.sql"));

        RealPath.Of(_folder.PathOf("Link.sql")).ShouldBe(InReal("Real/Sub/Q.sql"));
    }

    [Fact]
    public void Of_RelativeTarget_IsResolvedFromTheLinksFolder()
    {
        _ = _folder.CreateFolder("Links");
        Link("Links/Link", Path.Combine("..", "Real"));

        RealPath.Of(_folder.PathOf("Links/Link/Sub/Q.sql")).ShouldBe(InReal("Real/Sub/Q.sql"));
    }

    [Fact]
    public void Of_TargetThroughAnotherLink_IsTheRealPath()
    {
        Link("Outer", _folder.PathOf("Real"));
        Link("Inner", _folder.PathOf("Outer/Sub"));

        RealPath.Of(_folder.PathOf("Inner/Q.sql")).ShouldBe(InReal("Real/Sub/Q.sql"));
    }

    [Fact]
    public void Of_MissingPart_IsKeptAsTextAfterTheRealFolder()
    {
        Link("Link", _folder.PathOf("Real"));

        RealPath.Of(_folder.PathOf("Link/Missing/Q.sql")).ShouldBe(InReal("Real/Missing/Q.sql"));
    }

    [Fact]
    public void Of_LinkToNothing_IsItsTarget()
    {
        Link("Link", _folder.PathOf("Gone"));

        RealPath.Of(_folder.PathOf("Link/Q.sql")).ShouldBe(InReal("Gone/Q.sql"));
    }

    [Fact]
    public void Of_LoopOfLinks_IsThePathAsGiven()
    {
        Link("A", _folder.PathOf("B"));
        Link("B", _folder.PathOf("A"));
        var path = _folder.PathOf("A/Q.sql");

        RealPath.Of(path).ShouldBe(path);
    }

    [Fact]
    public void Of_RelativePath_IsThePath() => RealPath.Of("Q.sql").ShouldBe("Q.sql");
}
