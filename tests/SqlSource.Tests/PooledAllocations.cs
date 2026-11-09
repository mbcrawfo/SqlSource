using System;

namespace SqlSource.Tests;

// What code that rents from ArrayPool<T>.Shared allocates on a call.  A gen-2 collection under memory pressure, which
// any thread's allocations can bring, empties the pool, its arrays held by each thread included, and the next calls
// allocate their arrays again.  It adds to the one measure it falls in, and the next measure finds the pool full, so
// the least of a few measures is what the code allocates once the pool holds its arrays; an allocation the code makes
// of its own is in every measure.
internal static class PooledAllocations
{
    private const int Iterations = 20;

    private const int Measures = 5;

    // The bytes that Iterations calls allocate, the least of Measures measures, once the first calls have paid for
    // one-off work and filled the pool.  Generic, so that a result that is a struct is not boxed; the last result is
    // kept alive after the measures, so the runtime cannot drop the call.
    public static long Of<T>(Func<T> call)
    {
        var last = default(T);
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            last = call();
        }

        var least = long.MaxValue;
        for (var measure = 0; measure < Measures; measure++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < Iterations; iteration++)
            {
                last = call();
            }

            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        GC.KeepAlive(last);
        return least;
    }
}
