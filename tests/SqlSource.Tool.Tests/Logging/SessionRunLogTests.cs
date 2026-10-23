using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Logging;

// The log a describer is given: a connection's value is removed from every string of an event, at any depth, and
// from the strings of an event that make it together.
public sealed class SessionRunLogTests
{
    // Secrets.Connection cut in the middle of "Password".
    private const string FirstHalf = "Host=db;Pass";
    private const string SecondHalf = "word=hunter2";

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
    public void Write_ValueSplitOverTwoMembers_IsTheMarkInBoth()
    {
        (FirstHalf + SecondHalf).ShouldBe(Secrets.Connection);
        var log = new SessionRunLog(_inner, new ConnectionSecret(Secrets.Connection));

        log.Write("e", new("a", "x " + FirstHalf), new("b", SecondHalf + " y"), new("c", "plain"));

        _inner.Events[0].Members.ShouldBe([new("a", "x ***"), new("b", "*** y"), new("c", "plain")]);
    }

    [Fact]
    public void Write_ValueSplitOverTheNameAndAMember_IsTheMarkInBoth()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret(Secrets.Connection));

        log.Write("open " + FirstHalf, new LogMember(SecondHalf + " key", "plain"));

        var (name, members) = _inner.Events.ShouldHaveSingleItem();
        name.ShouldBe("open ***");
        members.ShouldBe([new("*** key", "plain")]);
    }

    // The names of the members between the halves are no part of the value: the values are searched without them too.
    [Fact]
    public void Write_ValueSplitOverATextOfAListAndATextOfAMapAfterIt_IsTheMarkInBoth()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret(Secrets.Connection));

        log.Write(
            "e",
            new LogMember(
                "m",
                LogValue.ListOf(["x " + FirstHalf, LogValue.MapOf(new LogMember("k", SecondHalf + " y"))])
            )
        );

        _inner
            .Events[0]
            .Members.ShouldBe([new("m", LogValue.ListOf(["x ***", LogValue.MapOf(new LogMember("k", "*** y"))]))]);
    }

    [Fact]
    public void Write_ValueWithALineBreakOverTwoItemsOfAList_IsTheMarkInBoth()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret("Host=db;\nPassword=pw"));

        log.Write("e", new LogMember("lines", LogValue.ListOfTexts(["Host=db;", "Password=pw"])));

        _inner.Events[0].Members.ShouldBe([new("lines", LogValue.ListOfTexts(["***", "***"]))]);
    }

    [Fact]
    public void Write_RewriteWithACarriageReturnOverTwoItemsOfAList_IsTheMarkInBoth()
    {
        var log = new SessionRunLog(
            _inner,
            new ConnectionSecret("host=db;password=pw", forms: ["Host=db;\r\nPassword=pw"])
        );

        log.Write("e", new LogMember("lines", LogValue.ListOfTexts(["a Host=db;", "Password=pw b"])));

        _inner.Events[0].Members.ShouldBe([new("lines", LogValue.ListOfTexts(["a ***", "*** b"]))]);
    }

    [Fact]
    public void Write_PasswordSplitOverTwoMembers_IsTheMarkInBoth()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret("Host=db;Password=hunter22", ["hunter22"]));

        log.Write("e", new("a", "pw hunt"), new("b", "er22 ok"));

        _inner.Events[0].Members.ShouldBe([new("a", "pw ***"), new("b", "*** ok")]);
    }

    [Fact]
    public void Write_PartsOfAWordInTwoMembers_AreLeft()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret("Host=db;Password=hunter22", ["hunter22"]));

        log.Write("e", new("a", "latest_hunt"), new("b", "er22x"));

        _inner.Events[0].Members.ShouldBe([new("a", "latest_hunt"), new("b", "er22x")]);
    }

    // The password that is a token in the second member is not cleaned before the whole value is found.
    [Fact]
    public void Write_WholeSplitOverTwoMembersWhoseSecondHoldsAPassword_IsTheMarkInBoth()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret("Host=db;Password=hunter22", ["hunter22"]));

        log.Write("e", new("a", "Host=db;Pass"), new("b", "word=hunter22"));

        _inner.Events[0].Members.ShouldBe([new("a", "***"), new("b", "***")]);
    }

    [Fact]
    public void Write_WithAPrefixAndAValueSplitInTheName_PutsThePrefixBeforeTheMark()
    {
        var log = new SessionRunLog(_inner, new ConnectionSecret(Secrets.Connection), "postgres.");

        log.Write("open " + FirstHalf, new LogMember(SecondHalf + " key", "plain"));

        var (name, members) = _inner.Events.ShouldHaveSingleItem();
        name.ShouldBe("postgres.open ***");
        members.ShouldBe([new("*** key", "plain")]);
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
