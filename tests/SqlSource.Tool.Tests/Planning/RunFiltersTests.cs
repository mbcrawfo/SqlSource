using System;
using System.IO;
using Shouldly;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

public sealed class RunFiltersTests
{
    private static readonly string WorkingDirectory = Path.GetTempPath();

    [Fact]
    public void Create_SqlPath_PlacesIt()
    {
        var filters = RunFilters.Create([new UsageValue(null, "Q.sql", 1)], [], WorkingDirectory);

        filters.Files.ShouldHaveSingleItem().NormalizedPath.ShouldEndWith("/Q.sql");
        filters.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Create_NoSqlPath_HasNoFiles() =>
        RunFilters.Create([], [new UsageValue(null, "billing", 1)], WorkingDirectory).Files.ShouldBeEmpty();

    // The path is kept, so that the planner reports it and it selects no file.
    [Fact]
    public void Create_SqlPathThatClimbsAboveTheRoot_KeepsItWithNoNormalizedPath()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), AboveTheRoot.SkipReason);

        var filters = RunFilters.Create([new UsageValue(null, AboveTheRoot.SqlFile, 1)], [], WorkingDirectory);

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
            WorkingDirectory
        );

        _ = filters.Files.ShouldHaveSingleItem();
    }
}
