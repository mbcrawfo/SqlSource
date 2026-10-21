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
        filters.NamesFiles.ShouldBeTrue();
    }

    [Fact]
    public void Create_NoSqlPath_NamesNoFiles() =>
        RunFilters.Create([], [new UsageValue(null, "billing", 1)], WorkingDirectory).NamesFiles.ShouldBeFalse();

    // Outside Windows a backslash is no separator to Path.GetFullPath, which keeps the .. segments that the generator's
    // rule resolves, and these climb above the root.
    [Fact]
    public void Create_SqlPathThatClimbsAboveTheRoot_PlacesNothingAndNamesFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Path.GetFullPath resolves a backslash's .. on Windows.");
        var climbing = "a" + string.Concat(Enumerable.Repeat(@"\..", 64)) + @"\q.sql";

        var filters = RunFilters.Create([new UsageValue(null, climbing, 1)], [], WorkingDirectory);

        filters.Files.ShouldBeEmpty();
        filters.NamesFiles.ShouldBeTrue();
        filters.IsEmpty.ShouldBeTrue();
    }
}
