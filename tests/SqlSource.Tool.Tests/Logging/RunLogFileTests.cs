using System;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Logging;

// The writer of a log's file: one JSON object on a line for each event.
public sealed class RunLogFileTests : IDisposable
{
    private const string Head = "{\"event\":\"e\",\"time\":\"" + TestClock.StartIso + "\"";

    private readonly TestClock _clock = new();
    private readonly TempFolder _folder = new();
    private readonly MemoryStream _stream = new();

    public void Dispose()
    {
        _stream.Dispose();
        _folder.Dispose();
    }

    private string Written() => Encoding.UTF8.GetString(_stream.ToArray());

    [Fact]
    public void Write_Event_IsOneObjectOnALineWithItsNameItsTimeAndItsMembersInOrder()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write(
            "e",
            new("text", "a"),
            new("number", 12),
            new("flag", true),
            new("nothing", LogValue.Null),
            new("list", LogValue.ListOfTexts(["b", "c"])),
            new(
                "map",
                LogValue.MapOf(new("inner", LogValue.Of((long?)null)), new("deep", LogValue.ListOf([LogValue.MapOf()])))
            )
        );

        Written()
            .ShouldBe(
                Head
                    + ",\"text\":\"a\",\"number\":12,\"flag\":true,\"nothing\":null,\"list\":[\"b\",\"c\"],"
                    + "\"map\":{\"inner\":null,\"deep\":[{}]}}\n"
            );
    }

    // The writer owns "event", "time" and "truncated" at the top of a line, so a line has each key once and a
    // reader cannot take a caller's member for the run's.
    [Fact]
    public void Write_TopLevelMembersNamedLikeTheWritersOwn_AreWrittenWithAnUnderscoreAndTheWritersStandAlone()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new("event", "run.end"), new("time", "never"), new("truncated", false));

        Written().ShouldBe(Head + ",\"_event\":\"run.end\",\"_time\":\"never\",\"_truncated\":false}\n");
        var line = LogReader.Parse(Written().TrimEnd('\n'));
        line.GetProperty("event").GetString().ShouldBe("e");
        line.GetProperty("time").GetString().ShouldBe(TestClock.StartIso);
        line.TryGetProperty("truncated", out _).ShouldBeFalse();
    }

    [Fact]
    public void Write_TopLevelMemberNamedTruncatedAndAStringThatIsCut_HasTheWritersMarkOnceAndTheCallersUnderscored()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new LogMember("truncated", "mine"), new("big", new string('a', 2 * RunLogFile.MaxStringBytes)));

        var line = LogReader.Parse(Written().TrimEnd('\n'));
        line.GetProperty("_truncated").GetString().ShouldBe("mine");
        line.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Write_MemberOfANestedMapNamedLikeTheWritersOwn_KeepsItsName()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new LogMember("map", LogValue.MapOf(new("time", "kept"), new("event", "kept"))));

        Written().ShouldBe(Head + ",\"map\":{\"time\":\"kept\",\"event\":\"kept\"}}\n");
    }

    [Fact]
    public void Write_TwoEvents_AreTwoLinesEachWithTheTimeItWasWrittenAt()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e");
        _clock.Advance(TimeSpan.FromMilliseconds(1500));
        log.Write("e");

        Written().ShouldBe(Head + "}\n{\"event\":\"e\",\"time\":\"2026-10-09T14:02:12.912Z\"}\n");
    }

    // A log is for people: a letter outside ASCII is written as it is, and only what JSON must escape is escaped.
    [Fact]
    public void Write_TextOutsideAsciiAndAQuote_IsReadableAndStillOneLine()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new LogMember("text", "é <b> \"q\"\nnext"));

        Written().ShouldBe(Head + ",\"text\":\"é <b> \\\"q\\\"\\nnext\"}\n");
    }

    [Fact]
    public void Write_StringOverOneMebibyte_IsCutToThatSizeAndTheObjectIsMarked()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new LogMember("big", new string('a', 2 * RunLogFile.MaxStringBytes)), new("small", "b"));

        var line = LogReader.Parse(Written().TrimEnd('\n'));
        line.GetProperty("big").GetString().ShouldBe(new string('a', RunLogFile.MaxStringBytes));
        line.GetProperty("small").GetString().ShouldBe("b");
        line.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    // The size is counted in UTF-8, and a character of four bytes is never cut in two.
    [Fact]
    public void Write_LongStringOfCharactersOfFourBytes_IsCutBetweenTwoOfThem()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new LogMember("big", string.Concat(Enumerable.Repeat("😀", 300_000))));

        var line = LogReader.Parse(Written().TrimEnd('\n'));
        var kept = line.GetProperty("big").GetString().ShouldNotBeNull();
        Encoding.UTF8.GetByteCount(kept).ShouldBe(RunLogFile.MaxStringBytes);
        kept.ShouldBe(string.Concat(Enumerable.Repeat("😀", RunLogFile.MaxStringBytes / 4)));
        line.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Write_ShortString_IsNotMarked()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new LogMember("text", "a"));

        Written().ShouldNotContain("truncated");
    }

    // A server's message may hold half a character.  The line must still be JSON.
    [Fact]
    public void Write_TextWithHalfACharacter_IsWrittenWithTheReplacementCharacter()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new LogMember("text", "a\uD83Db"));

        LogReader.Parse(Written().TrimEnd('\n')).GetProperty("text").GetString().ShouldBe("a\uFFFDb");
    }

    // A full disk must not end the run: the writer goes quiet and says why when it is asked.
    [Fact]
    public void Write_StreamThatFails_DoesNotThrowAndKeepsTheFailure()
    {
        using var broken = new BrokenStream();
        using var log = new RunLogFile("/log", broken, _clock);

        log.Write("e");
        log.Write("e");

        log.Failure.ShouldBeOfType<IOException>().Message.ShouldBe("disk full");
        broken.Writes.ShouldBe(1);
    }

    [Fact]
    public void Write_AfterDispose_WritesNothing()
    {
        var log = new RunLogFile("/log", _stream, _clock);
        log.Dispose();

        log.Write("e");

        Written().ShouldBeEmpty();
        log.Failure.ShouldBeNull();
    }

    [Fact]
    public void Open_RelativePath_MakesTheFileInTheWorkingDirectoryAndEmptiesOneThatIsThere()
    {
        var expected = _folder.WriteFile("run.log", "of an earlier run\n");

        using var log = RunLogFile.Open("run.log", _folder.Path, _clock).Value.ShouldNotBeNull();

        log.Path.ShouldBe(expected);
        log.Write("e");
        log.Dispose();
        File.ReadAllText(expected).ShouldBe(Head + "}\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-such-folder/run.log")]
    [InlineData("folder")]
    public void Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason(string given)
    {
        _ = _folder.CreateFolder("folder");

        var opened = RunLogFile.Open(given, _folder.Path, _clock);
        opened.Value?.Dispose();

        opened.Succeeded.ShouldBeFalse();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.ShouldBeSameAs(ToolDiagnostics.FileCannotBeChanged);
        // The error is at the path, the full one when the system could make one, and quotes it first.
        failure.Arguments[0].ShouldBe(failure.Path);
        failure.Arguments[1].ShouldBe("written");
        failure.Arguments[2].ShouldNotBeEmpty();
    }

    // A slip of the command line, "--log Users.sql", must not empty a file of the run: nothing is opened or made.
    [Theory]
    [InlineData("Users.sql")]
    [InlineData("Users.sql.json")]
    [InlineData("App.csproj")]
    [InlineData("App.sln")]
    [InlineData("App.slnx")]
    [InlineData("USERS.SQL")]
    [InlineData("sub/App.CsProj")]
    public void Open_PathOfAFileOfTheRun_GivesNoLogAndTheReasonAndLeavesTheFileAsItWas(string given)
    {
        var existing = _folder.WriteFile(given, "of the run\n");

        var opened = RunLogFile.Open(given, _folder.Path, _clock);
        opened.Value?.Dispose();

        opened.Failure.ShouldBe(
            ToolDiagnostic.ForFile(
                ToolDiagnostics.FileCannotBeChanged,
                existing,
                "written",
                "a log is not written over a .sql file, a sidecar, a project or a solution"
            )
        );
        File.ReadAllText(existing).ShouldBe("of the run\n");
    }

    [Fact]
    public void Open_PathOfAFileOfTheRunThatIsNotThere_MakesNothing()
    {
        var opened = RunLogFile.Open("New.sql", _folder.Path, _clock);
        opened.Value?.Dispose();

        opened.Succeeded.ShouldBeFalse();
        File.Exists(_folder.PathOf("New.sql")).ShouldBeFalse();
    }

    [Fact]
    public void Open_PathThatOnlyEndsLikeAFileOfTheRun_IsALog()
    {
        var opened = RunLogFile.Open("run.sql.log", _folder.Path, _clock);
        using var log = opened.Value;

        opened.Succeeded.ShouldBeTrue();
    }

    private sealed class BrokenStream : MemoryStream
    {
        public int Writes { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Writes++;
            throw new IOException("disk full");
        }

        public override void Write(ReadOnlySpan<byte> buffer) => Write([], 0, 0);
    }
}
