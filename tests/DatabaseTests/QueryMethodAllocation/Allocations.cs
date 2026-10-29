using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

// Every test counts what every thread of the process allocates, so no two run at once, and the collections of the two
// engines run one after the other.
[assembly: Parallelization(Mode = ParallelMode.None)]

namespace SqlSource.DatabaseTests.QueryMethodAllocation;

// What a test measures with: the calls made before the first pair to warm both methods up, the pairs of calls, and the
// least bytes a call of either method must allocate for the measure to be of the call at all.
internal sealed record MeasureSettings(int WarmUpCalls, int Pairs, long LeastBytesPerCall);

// The assertions of the allocation tests of both engines, through CallAllocations, which explains the measure.
internal static class Allocations
{
    // Puts the connection in the state a test measures in: opened for Open, left as it is for Closed.
    public static Task InStateAsync(DbConnection connection, ConnectionState state, CancellationToken cancellationToken)
    {
        if (state == ConnectionState.Open)
        {
            return connection.OpenAsync(cancellationToken);
        }

        return state == ConnectionState.Closed
            ? Task.CompletedTask
            : throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "A test measures an open or a closed connection."
            );
    }

    // The generated method may allocate no more than the margin beyond the hand-written one.
    public static async Task ShouldAllocateNoMoreThanByHandAsync<TGenerated, TByHand>(
        Func<Task<TGenerated>> generated,
        Func<Task<TByHand>> byHand,
        long marginInBytesPerCall,
        MeasureSettings settings
    )
    {
        var pairs = await MeasureAsync(generated, byHand, settings);
        pairs.Generated.Bytes.ShouldBeGreaterThan(settings.LeastBytesPerCall);
        pairs.ByHand.Bytes.ShouldBeGreaterThan(settings.LeastBytesPerCall);
        pairs.GeneratedBeyondByHand.ShouldBeLessThanOrEqualTo(marginInBytesPerCall);
    }

    // The generated method must allocate at least the saving less than the hand-written one.
    public static async Task ShouldSaveAtLeastAsync<TGenerated, TByHand>(
        Func<Task<TGenerated>> generated,
        Func<Task<TByHand>> byHand,
        long leastSavingInBytesPerCall,
        MeasureSettings settings
    )
    {
        var pairs = await MeasureAsync(generated, byHand, settings);
        pairs.Generated.Bytes.ShouldBeGreaterThan(settings.LeastBytesPerCall);
        pairs.ByHand.Bytes.ShouldBeGreaterThan(settings.LeastBytesPerCall);
        pairs.GeneratedBeyondByHand.ShouldBeLessThanOrEqualTo(-leastSavingInBytesPerCall);
    }

    private static async Task<CallPairs> MeasureAsync<TGenerated, TByHand>(
        Func<Task<TGenerated>> generated,
        Func<Task<TByHand>> byHand,
        MeasureSettings settings
    )
    {
        var pairs = await CallAllocations.MeasureAsync(generated, byHand, settings.WarmUpCalls, settings.Pairs);
        TestContext.Current.TestOutputHelper?.WriteLine($"Of {settings.Pairs} pairs of calls, {pairs}.");
        return pairs;
    }
}
