using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Logging;

// The log a describer is given: a connection's value is removed from every string of an event, at any depth.
public sealed class SessionRunLogTests
{
    private readonly RecordingLog _inner = new();

    [Fact]
    public void Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret(Secrets.Connection));

        log.Write(
            "open " + Secrets.Connection,
            new(Secrets.Connection, Secrets.Connection),
            new("inside", "cannot connect with '" + Secrets.Connection + "' today"),
            new("list", LogValue.ListOf([Secrets.Connection, LogValue.ListOfTexts([Secrets.Connection])])),
            new(
                "map",
                LogValue.MapOf(
                    new LogMember(
                        Secrets.Connection + " key",
                        LogValue.MapOf(new LogMember("deep", Secrets.Connection))
                    )
                )
            )
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
        var log = new SessionRunLog(_inner, new ConnectionSecret(Secrets.Connection));

        log.Write("e", new("number", 5), new("flag", true), new("nothing", LogValue.Null));

        _inner.Events[0].Members.ShouldBe([new("number", 5), new("flag", true), new("nothing", LogValue.Null)]);
    }

    // A password that the describer gave is removed where it stands as a token, and left inside a word.
    [Fact]
    public void Write_PasswordPartInAMember_IsTheMarkThereAndAWordThatHoldsItIsLeft()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret("Host=db;Password=test", ["test"]));

        log.Write("e", new LogMember("message", "password 'test' was refused at latest_orders"));

        _inner.Events[0].Members.ShouldBe([new("message", "password '***' was refused at latest_orders")]);
    }

    // A describer gives the rest of the name, and cannot write an event of the run's.
    [Fact]
    public void Write_WithAPrefix_PutsItBeforeTheNameAfterTheValueWasRemoved()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret("postgres."), "postgres.");

        log.Write("run.end postgres.");

        _inner.Events[0].Name.ShouldBe("postgres.run.end ***");
    }

    [Fact]
    public void Writes_OverALog_IsWhatTheInnerLogSays()
    {
        new SessionRunLog(NullRunLog.Instance, new ConnectionSecret("x"), "postgres.").Writes.ShouldBeFalse();
        new SessionRunLog(new RecordingLog(), new ConnectionSecret("x"), "postgres.").Writes.ShouldBeTrue();
    }
}
