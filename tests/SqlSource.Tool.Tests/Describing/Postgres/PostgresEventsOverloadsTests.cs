using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says in the run's log of the call "overloads".
public sealed class PostgresEventsOverloadsTests
{
    // The event is built from what it is given; the tests of the session say when it is asked.
    [Fact]
    public void Overloads_NamesOfTheServer_WritesWhatWasAskedAndWhatIsHeld()
    {
        var log = new RecordingLog();
        var request = new PostgresOverloadsRequest(["array_agg", "count", "max"], ["*", "+"]);
        var answer = new PostgresOverloadsAnswer(["count", "max"], ["*"], null);

        PostgresEvents.Overloads(log, "ListUsers", request, answer, 3);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("overloads");
        NamesOf(members).ShouldBe(["query", "functions", "operators", "held", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"ListUsers");
        Member(members, "functions").ShouldBe(LogValue.ListOfTexts(["array_agg", "count", "max"]));
        Member(members, "operators").ShouldBe(LogValue.ListOfTexts(["*", "+"]));
        Member(members, "held")
            .ShouldBe(
                LogValue.MapOf(
                    new("functions", LogValue.ListOfTexts(["count", "max"])),
                    new("operators", LogValue.ListOfTexts(["*"]))
                )
            );
        Member(members, "durationMs").ShouldBe((LogValue)3L);
    }

    // A call that failed has its error in the place of what is held.
    [Fact]
    public void Overloads_ErrorOfTheServer_WritesItInThePlaceOfWhatIsHeld()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError("42501", "permission denied", null, null, null);

        PostgresEvents.Overloads(
            log,
            "ListUsers",
            new PostgresOverloadsRequest(["count"], []),
            new PostgresOverloadsAnswer(null, null, refused),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "functions", "operators", "error", "durationMs"]);
        Member(members, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "42501"),
                    new("message", "permission denied"),
                    new("detail", LogValue.Null),
                    new("hint", LogValue.Null),
                    new("position", LogValue.Null)
                )
            );
    }
}
