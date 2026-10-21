using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

public sealed class RunFiltersTests
{
    private static readonly string WorkingDirectory = Path.GetTempPath();

    [Fact]
    public void Create_SqlPath_PlacesItAndNamesFiles()
    {
        var filters = RunFilters.Create([new UsageValue(null, "Q.sql", 1)], [], WorkingDirectory);

        filters.Files.ShouldHaveSingleItem().NormalizedPath.ShouldEndWith("/Q.sql");
        filters.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Create_NoSqlPath_NamesNoFiles() =>
        RunFilters.Create([], [new UsageValue(null, "billing", 1)], WorkingDirectory).Files.ShouldBeEmpty();

    // Outside Windows a backslash is no separator to Path.GetFullPath, which keeps the .. segments that the generator's
    // rule resolves, and these climb above the root.
    private static readonly string Climbing = "a" + string.Concat(Enumerable.Repeat(@"\..", 64)) + @"\q.sql";

    // The path is kept, so that the planner reports it and it selects no file.
    [Fact]
    public void Create_SqlPathThatClimbsAboveTheRoot_KeepsItWithNoNormalizedPath()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Path.GetFullPath resolves a backslash's .. on Windows.");

        var filters = RunFilters.Create([new UsageValue(null, Climbing, 1)], [], WorkingDirectory);

        filters
            .Files.ShouldHaveSingleItem()
            .ShouldBe(new FileFilter(null, $"'{Path.GetFullPath(Climbing, WorkingDirectory)}'"));
        filters.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Create_SqlPathThatClimbsAboveTheRootGivenTwice_KeepsItOnce()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Path.GetFullPath resolves a backslash's .. on Windows.");

        var filters = RunFilters.Create(
            [new UsageValue(null, Climbing, 1), new UsageValue(null, Climbing.ToUpperInvariant(), 2)],
            [],
            WorkingDirectory
        );

        _ = filters.Files.ShouldHaveSingleItem();
    }
}
