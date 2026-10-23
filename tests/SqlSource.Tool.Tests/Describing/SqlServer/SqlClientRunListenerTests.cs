using System;
using System.Diagnostics.Tracing;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// What of SqlClient's event source reaches the run's log: the listed events, each with its listed members alone.  The
// payloads are those SqlClient 7.1.1 writes, as the container's DriverLogTests saw them.
public sealed class SqlClientRunListenerTests : IDisposable
{
    private static readonly string[] BeginExecuteNames =
    [
        "objectId",
        "dataSource",
        "database",
        "commandText",
        "message",
    ];

    private static readonly string[] EndExecuteNames = ["objectId", "compositestate", "sqlExceptionNumber", "message"];

    private readonly RecordingLog _recording = new();
    private readonly SqlClientRunListener _listener;

    public SqlClientRunListenerTests()
    {
        _listener = SqlClientRunListener.For(_recording).ShouldNotBeNull();
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void For_LogThatKeepsNothing_IsNull()
    {
        SqlClientRunListener.For(NullRunLog.Instance).ShouldBeNull();
        SqlClientRunListener
            .For(new SessionRunLog(NullRunLog.Instance, new ConnectionSecret("x"), "mssql."))
            .ShouldBeNull();
    }

    // The source and the database name the server and the database, and the message repeats the text with the
    // connection's id: none of them is written.
    [Fact]
    public void Forward_ListedEvent_WritesListedMembersAlone()
    {
        _listener.Forward(
            "BeginExecute",
            EventLevel.Informational,
            1,
            BeginExecuteNames,
            [9, "db.example,1433", "billing", "SELECT 1", "SqlCommand.WriteBeginExecuteEvent | INFO | db.example"]
        );

        var (name, members) = _recording.Events.ShouldHaveSingleItem();
        name.ShouldBe(SqlClientRunListener.EventName);
        members.ShouldBe([
            new("category", "BeginExecute"),
            new("level", "informational"),
            new("eventId", 1L),
            new("objectId", 9L),
            new("commandText", "SELECT 1"),
            new("exception", LogValue.Null),
        ]);
    }

    // The message of an error names nothing of the connection here, but it is free-form, and so not written.
    [Fact]
    public void Forward_EndExecute_WritesItsStateAndTheErrorsNumberAlone()
    {
        _listener.Forward(
            "EndExecute",
            EventLevel.Informational,
            2,
            EndExecuteNames,
            [13, 2, 208, "SqlCommand.WriteEndExecuteEvent | INFO | Sql Exception Number 208"]
        );

        _recording
            .Events.ShouldHaveSingleItem()
            .Members.ShouldBe([
                new("category", "EndExecute"),
                new("level", "informational"),
                new("eventId", 2L),
                new("objectId", 13L),
                new("compositestate", 2L),
                new("sqlExceptionNumber", 208L),
                new("exception", LogValue.Null),
            ]);
    }

    // A later SqlClient that drops or renames a listed member gives the event the same members, the missing one null.
    [Fact]
    public void Forward_ListedMemberNotInThePayload_IsNull()
    {
        _listener.Forward("BeginExecute", EventLevel.Informational, 1, ["objectId"], [9]);

        _recording.Events.ShouldHaveSingleItem().Members[4].ShouldBe(new LogMember("commandText", LogValue.Null));
    }

    [Theory]
    [InlineData("Trace", 3)]
    [InlineData("AdvancedTrace", 12)]
    [InlineData("ScopeEnter", 4)]
    [InlineData("beginexecute", 1)]
    public void Forward_UnlistedEvent_WritesNothing(string eventName, int eventId)
    {
        _listener.Forward(
            eventName,
            EventLevel.Informational,
            eventId,
            ["message"],
            ["<prov.DbConnectionHelper.ConnectionString_Set|API> db.example; Password=hunter2"]
        );

        _recording.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(EventLevel.LogAlways, "logalways")]
    [InlineData(EventLevel.Critical, "critical")]
    [InlineData(EventLevel.Error, "error")]
    [InlineData(EventLevel.Warning, "warning")]
    [InlineData(EventLevel.Informational, "informational")]
    [InlineData(EventLevel.Verbose, "verbose")]
    public void Forward_Level_IsWrittenInLowerCase(EventLevel level, string word)
    {
        _listener.Forward("BeginExecute", level, 1, BeginExecuteNames, [9, "db", "app", "SELECT 1", "m"]);

        _recording.Events.ShouldHaveSingleItem().Members[1].ShouldBe(new LogMember("level", word));
    }

    [Fact]
    public void Forward_StringWithTheSecret_PassesTheLogsRedaction()
    {
        var log = new SessionRunLog(_recording, new ConnectionSecret(Secrets.Connection), "mssql.");
        using var listener = SqlClientRunListener.For(log).ShouldNotBeNull();

        listener.Forward(
            "BeginExecute",
            EventLevel.Informational,
            1,
            BeginExecuteNames,
            [9, "db", "app", "SELECT '" + Secrets.Connection + "'", "m"]
        );

        var (name, members) = _recording.Events.ShouldHaveSingleItem();
        name.ShouldBe("mssql.driver");
        members[4].ShouldBe(new LogMember("commandText", "SELECT '***'"));
    }
}
