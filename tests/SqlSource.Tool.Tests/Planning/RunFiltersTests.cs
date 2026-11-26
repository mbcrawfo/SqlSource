using System;
using System.IO;
using Shouldly;
using SqlSource.Generation.Paths;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

public sealed class RunFiltersTests
{
    private static readonly string WorkingDirectory = Path.GetTempPath();

    [Fact]
    public void Create_SqlPath_PlacesIt()
    {
        var filters = RunFilters.Create([new UsageValue(null, "Q.sql", 1)], [], WorkingDirectory, []);

        filters.Files.ShouldHaveSingleItem().NormalizedPath.ShouldEndWith("/Q.sql");
        filters.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Create_NoSqlPath_HasNoFiles() =>
        RunFilters.Create([], [new UsageValue(null, "billing", 1)], WorkingDirectory, []).Files.ShouldBeEmpty();

    // The path is kept, so that the planner reports it and it selects no file.
    [Fact]
    public void Create_SqlPathThatClimbsAboveTheRoot_KeepsItWithNoNormalizedPath()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), AboveTheRoot.SkipReason);

        var filters = RunFilters.Create(
            [new UsageValue(null, AboveTheRoot.SqlFile, 1) { MayBePrintedAsPath = true }],
            [],
            WorkingDirectory,
            []
        );

        filters
            .Files.ShouldHaveSingleItem()
            .ShouldBe(new FileFilter(null, $"'{Path.GetFullPath(AboveTheRoot.SqlFile, WorkingDirectory)}'"));
        filters.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Create_SqlPathThatClimbsAboveTheRootGivenTwice_KeepsItOnce()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), AboveTheRoot.SkipReason);

        var filters = RunFilters.Create(
            [
                new UsageValue(null, AboveTheRoot.SqlFile, 1),
                new UsageValue(null, AboveTheRoot.SqlFile.ToUpperInvariant(), 2),
            ],
            [],
            WorkingDirectory,
            []
        );

        _ = filters.Files.ShouldHaveSingleItem();
    }

    // A project named through a link has its files listed through it, as MSBuild gives them.
    [Fact]
    public void Create_RealPathOfAFileListedThroughALink_TakesTheListedPath()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), SymbolicLinks.SkipReason);
        using var folder = new TempFolder();
        var real = folder.WriteFile("Real/Q.sql");
        _ = Directory.CreateSymbolicLink(folder.PathOf("Link"), folder.PathOf("Real"));
        var listed = folder.PathOf("Link/Q.sql");

        var filters = RunFilters.Create([new UsageValue(null, real, 1)], [], folder.Path, [listed]);

        filters.Files.ShouldHaveSingleItem().NormalizedPath.ShouldBe(SqlPath.Normalize(listed));
    }

    [Fact]
    public void Create_PathThroughALinkToNoListedFile_KeepsItsOwnPath()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), SymbolicLinks.SkipReason);
        using var folder = new TempFolder();
        var listed = folder.WriteFile("Real/Q.sql");
        _ = folder.WriteFile("Real/Other.sql");
        _ = Directory.CreateSymbolicLink(folder.PathOf("Link"), folder.PathOf("Real"));
        var other = folder.PathOf("Link/Other.sql");

        var filters = RunFilters.Create([new UsageValue(null, other, 1)], [], folder.Path, [listed]);

        filters.Files.ShouldHaveSingleItem().NormalizedPath.ShouldBe(SqlPath.Normalize(other));
    }
}
