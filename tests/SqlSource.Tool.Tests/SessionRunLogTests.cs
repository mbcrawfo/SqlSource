using Shouldly;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests;

// The log a describer is given: a connection's value is removed from every string of an event, at any depth.
public sealed class SessionRunLogTests
{
    private const string Secret = "Host=db;Password=hunter2";

    private readonly RecordingLog _inner = new();

    [Fact]
    public void Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach()
    {
        var log = new SessionRunLog(_inner, Secret);

        log.Write(
            "open " + Secret,
            new(Secret, Secret),
            new("inside", "cannot connect with '" + Secret + "' today"),
            new("list", LogValue.ListOf([Secret, LogValue.ListOfTexts([Secret])])),
            new("map", LogValue.MapOf(new LogMember(Secret + " key", LogValue.MapOf(new LogMember("deep", Secret)))))
        );

        var (name, members) = _inner.Events.ShouldHaveSingleItem();
        name.ShouldBe("open ***");
        members.ShouldBe([
            new("***", "***"),
            new("inside", "cannot connect with '***' today"),
            new("list", LogValue.ListOf(["***", LogValue.ListOfTexts(["***"])])),
            new("map", LogValue.MapOf(new LogMember("*** key", LogValue.MapOf(new LogMember("deep", "***"))))),
        ]);
    }

    [Fact]
    public void Write_ValuesThatAreNoStrings_AreAsTheyWere()
    {
        var log = new SessionRunLog(_inner, Secret);

        log.Write("e", new("number", 5), new("flag", true), new("nothing", LogValue.Null));

        _inner.Events[0].Members.ShouldBe([new("number", 5), new("flag", true), new("nothing", LogValue.Null)]);
    }

    // A describer gives the rest of the name, and cannot write an event of the run's.
    [Fact]
    public void Write_WithAPrefix_PutsItBeforeTheNameAfterTheValueWasRemoved()
    {
        var log = new SessionRunLog(_inner, "postgres.", "postgres.");

        log.Write("run.end postgres.");

        _inner.Events[0].Name.ShouldBe("postgres.run.end ***");
    }
}
