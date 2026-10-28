using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The bytes that a call of a generated method allocates beyond a call of a hand-written method of the same query, for
// the allocation tests of tests/DatabaseTests/QueryMethodAllocation.  The calls are made in pairs, one of each method,
// one right after the other, each measured on its own, and what the generated method allocates beyond the hand-written
// one is the commonest difference of a pair.
//
// A call allocates its method's own objects, and then more that it does not choose, by the path the driver takes:
// SqlClient allocates 48 or 296 bytes more on some calls, and Npgsql takes one of a few paths up to some hundreds of
// bytes apart, by when the server's packets arrive and by the state of its buffers, and the share of each path changes
// from run to run and with the load of the machine.  GC.GetTotalAllocatedBytes counts every thread, so what another
// thread allocates in the meantime, such as the test platform's telemetry, is counted in the call it falls in, and now
// and then the driver's thread is still allocating when a call returns, so that the next call is counted with it and
// the call reads hundreds of bytes less than it allocated; so the least of the calls is no measure either.  A total or
// a median of blocks of calls carries the driver's share, which differs from block to block, and on a busy runner in
// CI by more than the margin of the SQL Server test.  The commonest difference of a pair does not move with it.  Two
// calls on the same path differ by the methods' own bytes alone, to the byte, and no other difference can be as common:
// the pairs that differ by another amount are pairs of two paths, each path with at most one partner path that far
// from it, so that over many pairs they are fewer than the pairs of the same path.  A pair that another thread or the
// next call disturbed scatters over differences that do not recur.  An object that a generated method allocates on
// each call is in every pair, and so is its state machine, boxed on every call because each call waits for the server.
//
// That holds only while what else the process allocates during a call is rare.  Under coverage it is not: the collector
// allocates in the process it measures, some 4.4 to 4.7 KB on a fifth of the calls here and on half of them on a busy
// runner in CI, where the calls that bear it alternate, so that the pairs that differ by it outnumber those that do
// not, and the commonest difference became the collector's bytes and failed the tests at random.  So a measure skips
// under coverage, or any other profiler, and the nightly jobs that run the allocation tests run them without it.
internal static class CallAllocations
{
    // The variables a coverage collector or a profiler sets in the process it measures: CODE_COVERAGE_SESSION_NAME of
    // Microsoft.Testing.Extensions.CodeCoverage, which the tests' --coverage runs, and CORECLR_ENABLE_PROFILING of
    // any profiler the runtime loads.
    private static bool IsMeasuredByAnother =>
        Environment.GetEnvironmentVariable("CODE_COVERAGE_SESSION_NAME") is { Length: > 0 }
        || Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING") == "1";

    // Calls each method warmUpCalls times, then makes the pairs, the first of each pair changing from one to the next.
    // Each delegate is made once by the caller, and calling it allocates nothing of its own; each call is measured
    // here, around the call alone, so that no state machine of the measure is counted in it.  The two methods may
    // return different types, since a generated method returns what the query declares and a hand-written one the
    // type it builds, and the result of a call is dropped.
    public static async Task<CallPairs> MeasureAsync<TGenerated, TByHand>(
        Func<Task<TGenerated>> generated,
        Func<Task<TByHand>> byHand,
        int warmUpCalls,
        int pairs
    )
    {
        Assert.SkipWhen(
            IsMeasuredByAnother,
            "Coverage or a profiler allocates in the process on some calls and not others, which this measure counts."
        );

        for (var call = 0; call < warmUpCalls; call++)
        {
            _ = await generated();
            _ = await byHand();
        }

        var generatedCalls = new long[pairs];
        var byHandCalls = new long[pairs];
        for (var pair = 0; pair < pairs; pair++)
        {
            for (var turn = 0; turn < 2; turn++)
            {
                var isGenerated = (pair + turn) % 2 == 0;
                var before = GC.GetTotalAllocatedBytes(precise: true);
                if (isGenerated)
                {
                    _ = await generated();
                }
                else
                {
                    _ = await byHand();
                }

                var bytes = GC.GetTotalAllocatedBytes(precise: true) - before;
                (isGenerated ? generatedCalls : byHandCalls)[pair] = bytes;
            }
        }

        return CallPairs.Of(generatedCalls, byHandCalls);
    }
}

// The commonest bytes of a call of each method, and the commonest difference of a pair, the generated call's bytes
// less the hand-written one's, with the other differences that recurred most, which show what the driver and the
// other threads did to the other pairs.
internal sealed record CallPairs(Commonest Generated, Commonest ByHand, Commonest Difference, string OtherDifferences)
{
    public long GeneratedBeyondByHand => Difference.Bytes;

    public static CallPairs Of(long[] generatedCalls, long[] byHandCalls)
    {
        var differences = generatedCalls.Zip(byHandCalls, static (generated, byHand) => generated - byHand).ToArray();
        var difference = Commonest.Of(differences);
        var others = differences
            .Where(bytes => bytes != difference.Bytes)
            .GroupBy(static bytes => bytes)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Take(5)
            .Select(static group => string.Create(CultureInfo.InvariantCulture, $"{group.Key} x{group.Count()}"));
        return new CallPairs(
            Commonest.Of(generatedCalls),
            Commonest.Of(byHandCalls),
            difference,
            string.Join(", ", others)
        );
    }

    public override string ToString() =>
        $"the commonest bytes of a call: generated {Generated}, by hand {ByHand}; the commonest difference of a pair, "
        + $"generated less by hand: {Difference}, and the others {OtherDifferences}";
}

// The value that most of the values had, the larger of two that tie, how many had it and how many there were.
internal sealed record Commonest(long Bytes, int Times, int Count)
{
    public static Commonest Of(long[] values)
    {
        var commonest = values
            .GroupBy(static bytes => bytes)
            .OrderByDescending(static group => group.Count())
            .ThenByDescending(static group => group.Key)
            .First();
        return new Commonest(commonest.Key, commonest.Count(), values.Length);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Bytes} ({Times} of {Count})");
}
