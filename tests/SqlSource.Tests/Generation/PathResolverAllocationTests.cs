using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generation;

public class PathResolverAllocationTests
{
    // The generator resolves every attributed type again whenever the list of .sql files changes, so what resolving
    // one type allocates must not grow with the files of the rest of the project.  For a type with five files in a
    // project with 3,000 others it allocated 728 bytes when the budget was set, the same in Debug and in Release, and
    // 153,040 before the search compared paths in place.  The budget leaves room for differences between runtimes,
    // not for a regression: lower it when the code improves, and do not raise it to make a change pass.
    private const int BudgetInBytes = 900;

    private const int OwnFiles = 5;

    [Fact]
    public void Resolve_ProjectWithManyOtherFiles_AllocatesWithinItsBudget()
    {
        const int Iterations = 20;
        var type = TestModels.Type();
        var paths = CreatePaths();
        PathResolver.Resolve(type, paths, isSupportedFramework: true).Files.Count.ShouldBe(OwnFiles);

        // The first calls pay for one-off work: JIT compilation and static initialisers.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = PathResolver.Resolve(type, paths, isSupportedFramework: true);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = PathResolver.Resolve(type, paths, isSupportedFramework: true);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        (allocated / Iterations).ShouldBeLessThan(BudgetInBytes);
    }

    // The type's own files, 1,000 files in folders below its folder, which sort among its own, and 2,000 migration
    // scripts elsewhere.
    private static EquatableArray<string> CreatePaths()
    {
        var paths = new List<string>();
        for (var file = 0; file < OwnFiles; file++)
        {
            paths.Add(string.Create(CultureInfo.InvariantCulture, $"app/Repo/Queries{file}.sql"));
        }

        for (var file = 0; file < 1000; file++)
        {
            paths.Add(string.Create(CultureInfo.InvariantCulture, $"app/Repo/Part{file % 20}/Queries{file}.sql"));
        }

        for (var file = 0; file < 2000; file++)
        {
            paths.Add(string.Create(CultureInfo.InvariantCulture, $"db/migrations/{file:0000}.sql"));
        }

        return TestModels.Array([.. paths.OrderBy(path => path, SqlPath.Comparer)]);
    }
}
