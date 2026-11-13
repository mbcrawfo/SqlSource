using System;
using System.Linq;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The guard of a cleanup of the tests: which mutants the first report detected that the second does not.
public sealed class ReportComparisonTests
{
    private static Mutant At(int line, MutantStatus status) =>
        new(new MutantKey("A.cs", line, 1, line, 5, "m", "r", 0), status);

    // A row names its statuses, since a public theory cannot take the tool's internal enum.
    private static MutantStatus Status(string name) => Enum.Parse<MutantStatus>(name);

    [Theory]
    [InlineData(nameof(MutantStatus.Killed), nameof(MutantStatus.Killed))]
    [InlineData(nameof(MutantStatus.Timeout), nameof(MutantStatus.Killed))]
    [InlineData(nameof(MutantStatus.Timeout), nameof(MutantStatus.Timeout))]
    public void Compare_DetectedBeforeAndAfter_Holds(string before, string after)
    {
        var comparison = ReportComparison.Compare([At(1, Status(before))], [At(1, Status(after))]);

        comparison.Holds.ShouldBeTrue();
        comparison.Detected.ShouldBe(1);
    }

    [Theory]
    [InlineData(nameof(MutantStatus.Killed), nameof(MutantStatus.Survived))]
    [InlineData(nameof(MutantStatus.Killed), nameof(MutantStatus.NoCoverage))]
    [InlineData(nameof(MutantStatus.Timeout), nameof(MutantStatus.Survived))]
    public void Compare_DetectedBeforeAndNotAfter_IsLost(string before, string after) =>
        ReportComparison
            .Compare([At(1, Status(before))], [At(1, Status(after))])
            .Lost.ShouldBe([new Change(At(1, Status(before)).Key, Status(before), Status(after))]);

    [Theory]
    [InlineData(nameof(MutantStatus.Ignored))]
    [InlineData(nameof(MutantStatus.CompileError))]
    [InlineData(nameof(MutantStatus.RuntimeError))]
    [InlineData(nameof(MutantStatus.Pending))]
    public void Compare_DetectedBeforeAndNotTestedAfter_IsNotComparable(string after)
    {
        var comparison = ReportComparison.Compare([At(1, MutantStatus.Killed)], [At(1, Status(after))]);

        comparison.Lost.ShouldBeEmpty();
        comparison.NotComparable.ShouldBe([new Change(At(1, Status(after)).Key, MutantStatus.Killed, Status(after))]);
    }

    // A mutant whose tests now run out of time may be one that no test fails on any more, with slow tests.
    [Fact]
    public void Compare_KilledBeforeAndTimeoutAfter_IsNotComparable()
    {
        var comparison = ReportComparison.Compare([At(1, MutantStatus.Killed)], [At(1, MutantStatus.Timeout)]);

        comparison.Lost.ShouldBeEmpty();
        comparison.NotComparable.ShouldBe([
            new Change(At(1, MutantStatus.Killed).Key, MutantStatus.Killed, MutantStatus.Timeout),
        ]);
    }

    [Fact]
    public void Compare_DetectedBeforeAndAbsentAfter_IsNotComparableWithNoStatus() =>
        ReportComparison
            .Compare([At(1, MutantStatus.Killed)], [])
            .NotComparable.ShouldBe([new Change(At(1, MutantStatus.Killed).Key, MutantStatus.Killed, null)]);

    [Theory]
    [InlineData(nameof(MutantStatus.Survived), nameof(MutantStatus.NoCoverage))]
    [InlineData(nameof(MutantStatus.Ignored), nameof(MutantStatus.Survived))]
    [InlineData(nameof(MutantStatus.NoCoverage), nameof(MutantStatus.Killed))]
    public void Compare_NotDetectedBefore_IsLeftOut(string before, string after)
    {
        var comparison = ReportComparison.Compare([At(1, Status(before))], [At(1, Status(after))]);

        comparison.Holds.ShouldBeTrue();
        comparison.Detected.ShouldBe(0);
    }

    [Fact]
    public void Compare_MutantOnlyInTheSecondReport_IsLeftOut() =>
        ReportComparison.Compare([], [At(1, MutantStatus.Survived)]).Holds.ShouldBeTrue();

    [Fact]
    public void Compare_SeveralLost_AreOrderedByPosition() =>
        ReportComparison
            .Compare(
                [At(9, MutantStatus.Killed), At(2, MutantStatus.Killed)],
                [At(9, MutantStatus.Survived), At(2, MutantStatus.Survived)]
            )
            .Lost.Select(change => change.Key.StartLine)
            .ShouldBe([2, 9]);
}
