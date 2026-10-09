using System;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Snapshot;

public class SidecarReaderAllocationTests
{
    // From phase 5 the generator reads a sidecar again each time the file changes, so what a read allocates is
    // tracked here.  A read of the first example allocated 1.4 bytes for each character when the budget was set, the
    // same in Debug and in Release.  The budget leaves room for differences between runtimes, not for a regression:
    // lower it when the reader improves, and do not raise it to make a change pass.
    private const double BudgetInBytesPerCharacter = 1.7;

    [Fact]
    public void Read_UsersExample_AllocatesWithinItsBudget()
    {
        const int Iterations = 20;
        var text = SidecarExamples.Read(SidecarExamples.Users);
        SidecarReader.Read(text).Sidecar.ShouldNotBeNull().Queries.Count.ShouldBe(4);

        // The first reads pay for one-off work: JIT compilation and static initialisers.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SidecarReader.Read(text);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SidecarReader.Read(text);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        (allocated / (double)(Iterations * text.Length)).ShouldBeLessThan(BudgetInBytesPerCharacter);
    }
}
