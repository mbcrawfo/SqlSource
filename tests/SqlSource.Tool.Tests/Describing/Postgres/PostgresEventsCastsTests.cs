using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says in the run's log of the call "casts".
public sealed class PostgresEventsCastsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Casts_AnswerOfTheServer_WritesThePairsAndTheAnswer(bool anyCast)
    {
        var log = new RecordingLog();

        PostgresEvents.Casts(
            log,
            "ListUsers",
            new PostgresCastsRequest([25, 1082], [20, 20]),
            new PostgresCastsAnswer(anyCast, null),
            3
        );

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("casts");
        NamesOf(members).ShouldBe(["query", "pairs", "anyCast", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"ListUsers");
        Member(members, "pairs").ShouldBe((LogValue)2);
        Member(members, "anyCast").ShouldBe((LogValue)anyCast);
        Member(members, "durationMs").ShouldBe((LogValue)3L);
    }

    [Fact]
    public void Casts_ErrorOfTheServer_WritesItInThePlaceOfTheAnswer()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError("42501", "permission denied", null, null, null);

        PostgresEvents.Casts(
            log,
            "ListUsers",
            new PostgresCastsRequest([25], [20]),
            new PostgresCastsAnswer(null, refused),
            0
        );

        NamesOf(log.Events.ShouldHaveSingleItem().Members).ShouldBe(["query", "pairs", "error", "durationMs"]);
    }
}
