using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What of Npgsql's own log reaches the run's: the ids listed for their category, and without the exception's message.
public sealed class NpgsqlRunLoggerTests
{
    private readonly RecordingLog _recording = new();

    // Every id that is forwarded, at the level Npgsql 10.0.3 writes it.
    public static TheoryData<string, int, LogLevel> Listed =>
        new()
        {
            { "Npgsql.Command", NpgsqlEventId.ExecutingCommand, LogLevel.Debug },
            { "Npgsql.Command", NpgsqlEventId.CommandExecutionCompleted, LogLevel.Information },
            { "Npgsql.Command", NpgsqlEventId.ExecutingInternalCommand, LogLevel.Debug },
            { "Npgsql.Connection", NpgsqlEventId.CancellingCommand, LogLevel.Debug },
            { "Npgsql.Connection", NpgsqlEventId.ExceptionWhenClosingPhysicalConnection, LogLevel.Warning },
            { "Npgsql.Transaction", NpgsqlEventId.StartedTransaction, LogLevel.Debug },
            { "Npgsql.Transaction", NpgsqlEventId.CommittedTransaction, LogLevel.Debug },
            { "Npgsql.Transaction", NpgsqlEventId.RolledBackTransaction, LogLevel.Debug },
            { "Npgsql.Transaction", NpgsqlEventId.ExceptionDuringTransactionDispose, LogLevel.Error },
        };

    // Ids that are not listed: the messages that name the endpoint or quote the server, id 0, ids of features the
    // describer does not use, and an id that Npgsql 10.0.3 does not have.
    public static TheoryData<string, int> NotListed =>
        new()
        {
            { "Npgsql.Connection", NpgsqlEventId.OpenedConnection },
            { "Npgsql.Connection", NpgsqlEventId.ClosedConnection },
            { "Npgsql.Connection", NpgsqlEventId.OpenedPhysicalConnection },
            { "Npgsql.Connection", NpgsqlEventId.ClosedPhysicalConnection },
            { "Npgsql.Connection", NpgsqlEventId.ReceivedNotice },
            { "Npgsql.Connection", 0 },
            { "Npgsql.Command", 0 },
            { "Npgsql.Command", NpgsqlEventId.PreparingCommandExplicitly },
            { "Npgsql.Transaction", NpgsqlEventId.CreatingSavepoint },
            { "Npgsql.Command", 2999 },
        };

    // The levels that are ever forwarded.
    private static readonly LogLevel[] Forwardable =
    [
        .. Enum.GetValues<LogLevel>().Where(static l => l is >= LogLevel.Debug and < LogLevel.None),
    ];

    [Theory]
    [MemberData(nameof(Listed))]
    public void Log_ListedIdAtItsLevel_IsWritten(string category, int eventId, LogLevel level)
    {
        Write(category, level, "text", eventId: eventId);

        _recording.Events.ShouldHaveSingleItem().Name.ShouldBe(NpgsqlRunLogger.EventName);
    }

    [Theory]
    [MemberData(nameof(NotListed))]
    public void Log_IdNotListed_IsDropped(string category, int eventId)
    {
        foreach (var level in Forwardable)
        {
            Write(category, level, "Opened connection to db.example:5432/app", eventId: eventId);
        }

        _recording.Events.ShouldBeEmpty();
    }

    // A listed id is listed for the category that writes it, and a new version that moves it is read again.
    [Theory]
    [InlineData("Npgsql.Command", NpgsqlEventId.CancellingCommand)]
    [InlineData("Npgsql.Connection", NpgsqlEventId.ExecutingCommand)]
    [InlineData("Npgsql.Exception", NpgsqlEventId.ExecutingCommand)]
    public void Log_ListedIdOfAnotherCategory_IsDropped(string category, int eventId)
    {
        Write(category, LogLevel.Warning, "text", eventId: eventId);

        _recording.Events.ShouldBeEmpty();
    }

    // Npgsql.Exception writes only at id 0, the server's text among it, and a category the tool does not know has no
    // listed id: neither is enabled, so Npgsql does not build their messages.
    [Theory]
    [InlineData("Npgsql.Exception")]
    [InlineData("Npgsql.Copy")]
    [InlineData("Npgsql.Replication")]
    [InlineData("Npgsql.Pool")]
    public void IsEnabled_CategoryWithNoListedId_IsFalseAtEveryLevel(string category)
    {
        var logger = Logger(category);

        Enum.GetValues<LogLevel>().ShouldAllBe(level => !logger.IsEnabled(level));
    }

    [Theory]
    [InlineData("Npgsql.Command")]
    [InlineData("Npgsql.Connection")]
    [InlineData("Npgsql.Transaction")]
    public void IsEnabled_CategoryWithAListedId_IsTrueFromDebugAndNeverAtTraceOrNone(string category)
    {
        var logger = Logger(category);

        Forwardable.ShouldAllBe(level => logger.IsEnabled(level));
        logger.IsEnabled(LogLevel.Trace).ShouldBeFalse();
        logger.IsEnabled(LogLevel.None).ShouldBeFalse();
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.None)]
    public void Log_ListedIdAtTraceOrNone_IsDropped(LogLevel level)
    {
        Write("Npgsql.Command", level, "text", eventId: NpgsqlEventId.ExecutingCommand);

        _recording.Events.ShouldBeEmpty();
    }

    // A message that is dropped is never built.
    [Fact]
    public void Log_IdNotListed_DoesNotFormatTheMessage()
    {
        Logger("Npgsql.Connection")
            .Log(
                LogLevel.Debug,
                new EventId(NpgsqlEventId.OpenedConnection),
                "state",
                null,
                (_, _) => throw new InvalidOperationException("formatted")
            );

        _recording.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Log_Message_HasTheMembersInOrder()
    {
        Write(
            "Npgsql.Command",
            LogLevel.Information,
            "Command execution completed (duration=1ms): SELECT 1",
            eventId: NpgsqlEventId.CommandExecutionCompleted
        );

        _recording
            .Events.ShouldHaveSingleItem()
            .Members.ShouldBe([
                new("category", "Npgsql.Command"),
                new("level", "information"),
                new("eventId", 2001L),
                new("message", "Command execution completed (duration=1ms): SELECT 1"),
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
        Write("Npgsql.Command", level, "text", eventId: NpgsqlEventId.ExecutingCommand);

        _recording.Events[0].Members[1].ShouldBe(new LogMember("level", word));
    }

    // The exception's message is the server's or the socket's text, which can name the endpoint, a role or a database.
    [Theory]
    [InlineData("Npgsql.Connection", NpgsqlEventId.ExceptionWhenClosingPhysicalConnection)]
    [InlineData("Npgsql.Transaction", NpgsqlEventId.ExceptionDuringTransactionDispose)]
    [InlineData("Npgsql.Command", NpgsqlEventId.ExecutingCommand)]
    public void Log_Exception_IsItsTypeAlone(string category, int eventId)
    {
        Write(
            category,
            LogLevel.Error,
            "text",
            new InvalidOperationException("Failed to connect to db.example:5432"),
            eventId
        );

        _recording
            .Events.ShouldHaveSingleItem()
            .Members[4]
            .ShouldBe(new LogMember("exception", "System.InvalidOperationException"));
    }

    [Fact]
    public void Log_MessageThatHoldsTheConnection_IsWrittenWithoutIt()
    {
        var log = new SessionRunLog(_recording, new ConnectionSecret(Secrets.Connection), "postgres.");
        var factory = NpgsqlRunLogger.For(log).ShouldNotBeNull();

        factory
            .CreateLogger("Npgsql.Command")
            .Log(
                LogLevel.Debug,
                new EventId(NpgsqlEventId.ExecutingCommand),
                "state",
                null,
                (_, _) => "Executing command: " + Secrets.Connection
            );

        var (name, members) = _recording.Events.ShouldHaveSingleItem();
        name.ShouldBe("postgres.driver");
        var message = members[3].Value.ShouldBeOfType<LogValue.Text>().Value;
        message.ShouldContain("***");
        message.ShouldNotContain(Secrets.Connection);
    }

    // The list was read from this version's NpgsqlEventId.cs and LogMessages.cs.  An update of Npgsql reads the new
    // version's, adds the ids that are safe and that the describer reaches, and moves this test to it.
    [Fact]
    public void Npgsql_Loaded_IsTheVersionTheListWasReadFrom() =>
        typeof(NpgsqlEventId)
            .Assembly.GetName()
            .Version.ShouldBe(
                new Version(10, 0, 3, 0),
                "Read the new Npgsql's NpgsqlEventId.cs and LogMessages.cs and update the list of NpgsqlRunLogger."
            );

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

    private ILogger Logger(string category) => NpgsqlRunLogger.For(_recording).ShouldNotBeNull().CreateLogger(category);

    private void Write(string category, LogLevel level, string message, Exception? exception = null, int eventId = 0) =>
        Logger(category).Log(level, new EventId(eventId), "state", exception, (_, _) => message);
}
