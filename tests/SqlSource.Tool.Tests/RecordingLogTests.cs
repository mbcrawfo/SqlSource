using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// RecordingLog stands in for the run's log in the tests of every describer, and the driver's events reach it from the
// threads that run its commands.  This holds that it keeps each event, and its lookup of a member.
public sealed class RecordingLogTests
{
    [Fact]
    public async Task Write_FromParallelTasks_KeepsEveryEvent()
    {
        var log = new RecordingLog();

        await Parallel.ForAsync(
            0,
            1_000,
            TestContext.Current.CancellationToken,
            (index, _) =>
            {
                log.Write("event", new LogMember("index", index));
                return ValueTask.CompletedTask;
            }
        );

        log.Events.Count.ShouldBe(1_000);
    }

    [Fact]
    public void Events_AfterAWrite_IsACopyThatLaterWritesDoNotChange()
    {
        var log = new RecordingLog();
        log.Write("first");

        var events = log.Events;
        log.Write("second");

        events.Count.ShouldBe(1);
        log.Events.Count.ShouldBe(2);
    }

    [Fact]
    public void Member_NameOfOneMember_IsItsValue()
    {
        LogMember[] members = [new("a", 1), new("b", "two")];

        RecordingLog.Member(members, "b").ShouldBe("two");
    }

    [Fact]
    public void Member_NameOfNoMember_Throws() =>
        Should.Throw<InvalidOperationException>(() => RecordingLog.Member([new("a", 1)], "b"));

    [Fact]
    public void NamesOf_Members_AreTheirNamesInOrder() =>
        RecordingLog.NamesOf([new("b", 1), new("a", 2)]).ShouldBe(["b", "a"]);
}
