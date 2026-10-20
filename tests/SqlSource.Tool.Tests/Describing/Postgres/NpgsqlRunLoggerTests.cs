using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What of Npgsql's own log reaches the run's, at which levels, and without what.
public sealed class NpgsqlRunLoggerTests
{
    private readonly RecordingLog _recording = new();

    public static TheoryData<string, LogLevel> CommandOrTransactionAtDebugOrAbove =>
        Cross(["Npgsql.Command", "Npgsql.Transaction"], AtOrAbove(LogLevel.Debug));

    public static TheoryData<string, LogLevel> OtherBelowWarning =>
        Cross(
            ["Npgsql.Connection", "Npgsql.Copy", "Npgsql.Exception", "Npgsql.Replication"],
            [LogLevel.Trace, LogLevel.Debug, LogLevel.Information]
        );

    public static TheoryData<string, LogLevel> OtherAtWarningOrAbove =>
        Cross(
            ["Npgsql.Connection", "Npgsql.Copy", "Npgsql.Exception", "Npgsql.Replication"],
            [LogLevel.Warning, LogLevel.Error, LogLevel.Critical]
        );

    [Theory]
    [MemberData(nameof(CommandOrTransactionAtDebugOrAbove))]
    public void Log_CommandOrTransactionAtDebugOrAbove_IsWritten(string category, LogLevel level)
    {
        Write(category, level, "Executing command: SELECT 1");

        _recording.Events.ShouldHaveSingleItem().Name.ShouldBe(NpgsqlRunLogger.EventName);
    }

    [Theory]
    [InlineData("Npgsql.Command")]
    [InlineData("Npgsql.Transaction")]
    public void Log_CommandOrTransactionAtTrace_IsDropped(string category)
    {
        var logger = Logger(category);

        Write(category, LogLevel.Trace, "text");

        _recording.Events.ShouldBeEmpty();
        logger.IsEnabled(LogLevel.Trace).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(OtherBelowWarning))]
    public void Log_OtherCategoryBelowWarning_IsDropped(string category, LogLevel level)
    {
        Write(category, level, "text");

        _recording.Events.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(OtherAtWarningOrAbove))]
    public void Log_OtherCategoryAtWarningOrAbove_IsWritten(string category, LogLevel level)
    {
        Write(category, level, "text");

        _ = _recording.Events.ShouldHaveSingleItem();
    }

    [Fact]
    public void Log_AnyCategoryAtNone_IsDropped()
    {
        var logger = Logger("Npgsql.Command");

        logger.IsEnabled(LogLevel.None).ShouldBeFalse();
        Write("Npgsql.Command", LogLevel.None, "text");
        _recording.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Log_Message_HasTheMembersInOrder()
    {
        Write("Npgsql.Command", LogLevel.Debug, "Executing command: SELECT 1", eventId: 2001);

        _recording
            .Events.ShouldHaveSingleItem()
            .Members.ShouldBe([
                new("category", "Npgsql.Command"),
                new("level", "debug"),
                new("eventId", 2001L),
                new("message", "Executing command: SELECT 1"),
                new("exception", LogValue.Null),
            ]);
    }

    [Theory]
    [InlineData(LogLevel.Debug, "debug")]
    [InlineData(LogLevel.Information, "information")]
    [InlineData(LogLevel.Warning, "warning")]
    [InlineData(LogLevel.Error, "error")]
    [InlineData(LogLevel.Critical, "critical")]
    public void Log_Level_IsWrittenInLowerCase(LogLevel level, string word)
    {
        Write("Npgsql.Command", level, "text");

        _recording.Events[0].Members[1].ShouldBe(new LogMember("level", word));
    }

    [Fact]
    public void Log_Exception_IsItsTypeAndMessage()
    {
        Write("Npgsql.Command", LogLevel.Error, "text", new InvalidOperationException("boom"));

        _recording
            .Events.ShouldHaveSingleItem()
            .Members[4]
            .ShouldBe(new LogMember("exception", "System.InvalidOperationException: boom"));
    }

    // A failed connect names the endpoint in its exception, which the log must not hold.
    [Fact]
    public void Log_ExceptionOfConnection_IsNotWritten()
    {
        Write(
            "Npgsql.Connection",
            LogLevel.Error,
            "text",
            new InvalidOperationException("Failed to connect to db.example:5432")
        );

        _recording.Events.ShouldHaveSingleItem().Members[4].ShouldBe(new LogMember("exception", LogValue.Null));
    }

    [Fact]
    public void Log_MessageThatHoldsTheConnection_IsWrittenWithoutIt()
    {
        var log = new SessionRunLog(_recording, new ConnectionSecret(Secrets.Connection), "postgres.");
        var factory = NpgsqlRunLogger.For(log).ShouldNotBeNull();

        factory
            .CreateLogger("Npgsql.Command")
            .Log(LogLevel.Debug, new EventId(2001), "state", null, (_, _) => "Opening " + Secrets.Connection);

        var (name, members) = _recording.Events.ShouldHaveSingleItem();
        name.ShouldBe("postgres.driver");
        var message = members[3].Value.ShouldBeOfType<LogValue.Text>().Value;
        message.ShouldContain("***");
        message.ShouldNotContain(Secrets.Connection);
    }

    [Fact]
    public void BeginScope_AnyState_IsANoOpThatCanBeDisposed()
    {
        var scope = Logger("Npgsql.Command").BeginScope("state");

        _ = scope.ShouldNotBeNull();
        scope.Dispose();
        _recording.Events.ShouldBeEmpty();
    }

    [Fact]
    public void For_LogThatKeepsNothing_GivesNoFactory()
    {
        NpgsqlRunLogger.For(NullRunLog.Instance).ShouldBeNull();
        NpgsqlRunLogger
            .For(new SessionRunLog(NullRunLog.Instance, new ConnectionSecret("x"), "postgres."))
            .ShouldBeNull();
    }

    [Fact]
    public void For_LogThatWrites_GivesAFactory() =>
        NpgsqlRunLogger.For(new RecordingLog()).ShouldBeOfType<NpgsqlRunLogger>();

    private static LogLevel[] AtOrAbove(LogLevel level) =>
        [.. Enum.GetValues<LogLevel>().Where(l => l >= level && l != LogLevel.None)];

    private static TheoryData<string, LogLevel> Cross(string[] categories, LogLevel[] levels)
    {
        var data = new TheoryData<string, LogLevel>();
        foreach (var category in categories)
        {
            foreach (var level in levels)
            {
                data.Add(category, level);
            }
        }

        return data;
    }

    private ILogger Logger(string category) => NpgsqlRunLogger.For(_recording).ShouldNotBeNull().CreateLogger(category);

    private void Write(string category, LogLevel level, string message, Exception? exception = null, int eventId = 1) =>
        Logger(category).Log(level, new EventId(eventId), "state", exception, (_, _) => message);
}
