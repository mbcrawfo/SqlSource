using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says in the run's log of the call "triggers".
public sealed class PostgresEventsTriggersTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Triggers_AnswerOfTheServer_WritesTheAnswer(bool anyBeforeInsert)
    {
        var log = new RecordingLog();

        PostgresEvents.Triggers(log, "AddUser", new PostgresTriggersAnswer(anyBeforeInsert, null), 3);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("triggers");
        NamesOf(members).ShouldBe(["query", "anyBeforeInsert", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"AddUser");
        Member(members, "anyBeforeInsert").ShouldBe((LogValue)anyBeforeInsert);
        Member(members, "durationMs").ShouldBe((LogValue)3L);
    }

    [Fact]
    public void Triggers_ErrorOfTheServer_WritesItInThePlaceOfTheAnswer()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError("42501", "permission denied", null, null, null);

        PostgresEvents.Triggers(log, "AddUser", new PostgresTriggersAnswer(null, refused), 0);

        NamesOf(log.Events.ShouldHaveSingleItem().Members).ShouldBe(["query", "error", "durationMs"]);
    }
}
