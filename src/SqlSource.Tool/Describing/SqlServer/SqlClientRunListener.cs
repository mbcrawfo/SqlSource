using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// SqlClient's own event source, forwarded into the run's log as <c>mssql.driver</c> events while a session lives,
/// for a run that has a log.
/// </summary>
/// <remarks>
/// <para>
/// What is written is an allowlist, not a filter: an event whose name is listed is written with the members the list
/// names for it, and every other event, and every other member, is dropped.  The list holds no member that can name
/// the server, the database, the user or a password, and no free-form text, so a message that a later SqlClient adds
/// is dropped until someone lists it.  Every string still passes the describer's log, which removes the connection's
/// value and its passwords.
/// </para>
/// <para>
/// SqlClient 7.1.1, on a run of the container tests with a listener that kept every event and payload of its source,
/// writes under the keyword <c>ExecutionTrace</c> (1) two events alone, both at <c>Informational</c>:
/// <c>BeginExecute</c> (id 1), whose payload is <c>objectId</c>, <c>dataSource</c>, <c>database</c>,
/// <c>commandText</c> and <c>message</c>; and <c>EndExecute</c> (id 2), whose payload is <c>objectId</c>,
/// <c>compositestate</c>, all in lower case, <c>sqlExceptionNumber</c>, 0 on success, and <c>message</c>.
/// <c>dataSource</c> is the host and port, <c>database</c> the database's name, and each <c>message</c> repeats the
/// rest with the connection's id, so none of them is listed.  <c>commandText</c> is the tool's own text, or the name
/// of the procedure it calls, since a user's SQL reaches the server only as the value of a parameter.  The other
/// keywords write a <c>message</c> alone, free-form, and are not enabled.  A connection that is refused, or a login
/// that is rejected, runs no command and writes no event.
/// </para>
/// <para>
/// An <see cref="EventListener" /> is the process's: it sees SqlClient's commands on every connection while it lives,
/// which in the tool are the session's, since the run describes one database at a time.  So it lives only as long as
/// the session's connection: <see cref="SqlServerConnection" /> makes it, for a log that writes, when the session
/// opens, and disposes it in <c>DisposeAsync</c> when the session ends or fails to open, and a run without a log makes
/// none.  SqlClient writes from the thread that runs a command, so
/// <see cref="IRunLog.Write" /> is called from any thread; the run's log writes an event under a lock.
/// </para>
/// </remarks>
/// <param name="log">The describer's log.  It is the run's, and is not closed here.</param>
internal sealed class SqlClientRunListener(IRunLog log) : EventListener
{
    /// <summary>The name of the event, before the engine the describer's log puts in front of it.</summary>
    public const string EventName = "driver";

    /// <summary>The name of SqlClient's event source.</summary>
    public const string SourceName = "Microsoft.Data.SqlClient.EventSource";

    // SqlClient's keyword of the commands it starts and ends.
    private const EventKeywords ExecutionTrace = (EventKeywords)1;

    // The events that are written, each with the members of its payload that are, in the order written.
    private static readonly Dictionary<string, string[]> Listed = new(StringComparer.Ordinal)
    {
        ["BeginExecute"] = ["objectId", "commandText"],
        ["EndExecute"] = ["objectId", "compositestate", "sqlExceptionNumber"],
    };

    // Field initializers run before EventListener's constructor, which calls OnEventSourceCreated for every source that
    // already exists, and an event may be written from then on: so both fields are set by an initializer, and never in
    // a constructor's body, which would run too late.
    private readonly IRunLog _log = log;
    private readonly List<EventSource> _enabled = [];

    /// <summary>
    /// A listener on SqlClient's source, or null when the log keeps nothing and so nothing need listen.
    /// </summary>
    /// <param name="log">The describer's log.  It is the run's, and is not closed here.</param>
    public static SqlClientRunListener? For(IRunLog log) => log.Writes ? new SqlClientRunListener(log) : null;

    /// <summary>Writes one event of SqlClient's source, when it is listed, with its listed members alone.</summary>
    /// <param name="eventName">The event's name.</param>
    /// <param name="level">Its level.</param>
    /// <param name="eventId">Its id.</param>
    /// <param name="payloadNames">The names of its payload.</param>
    /// <param name="payload">The values of its payload, in the order of the names.</param>
    internal void Forward(
        string eventName,
        EventLevel level,
        int eventId,
        IReadOnlyList<string> payloadNames,
        IReadOnlyList<object?> payload
    )
    {
        if (!Listed.TryGetValue(eventName, out var listed))
        {
            return;
        }

        var members = new List<LogMember>(listed.Length + 4)
        {
            new("category", eventName),
            new("level", LevelOf(level)),
            new("eventId", eventId),
        };
        foreach (var name in listed)
        {
            members.Add(new(name, ValueOf(name, payloadNames, payload)));
        }

        members.Add(new("exception", LogValue.Null));
        _log.Write(EventName, [.. members]);
    }

    public override void Dispose()
    {
        // Each source is disabled before the listener is removed, so that it stops writing when no other listener
        // wants its events, whatever the runtime's own removal does.  EnableEvents and DisableEvents take the
        // runtime's lock of every listener, under which OnEventSourceCreated may be called, so neither is called
        // under this one.
        EventSource[] sources;
        lock (_enabled)
        {
            sources = [.. _enabled];
            _enabled.Clear();
        }

        foreach (var source in sources)
        {
            DisableEvents(source);
        }

        base.Dispose();
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (!string.Equals(eventSource.Name, SourceName, StringComparison.Ordinal))
        {
            return;
        }

        lock (_enabled)
        {
            _enabled.Add(eventSource);
        }

        EnableEvents(eventSource, EventLevel.Informational, ExecutionTrace);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData) =>
        Forward(
            eventData.EventName ?? string.Empty,
            eventData.Level,
            eventData.EventId,
            eventData.PayloadNames ?? (IReadOnlyList<string>)[],
            eventData.Payload ?? (IReadOnlyList<object?>)[]
        );

    // The value of a listed member, or null when the payload has none of that name.
    private static LogValue ValueOf(string name, IReadOnlyList<string> payloadNames, IReadOnlyList<object?> payload)
    {
        for (var i = 0; i < payloadNames.Count && i < payload.Count; i++)
        {
            if (string.Equals(payloadNames[i], name, StringComparison.Ordinal))
            {
                return payload[i] switch
                {
                    null => LogValue.Null,
                    string text => text,
                    bool flag => flag,
                    int number => number,
                    long number => number,
                    var other => Convert.ToString(other, CultureInfo.InvariantCulture),
                };
            }
        }

        return LogValue.Null;
    }

    private static string LevelOf(EventLevel level) =>
        level switch
        {
            EventLevel.LogAlways => "logalways",
            EventLevel.Critical => "critical",
            EventLevel.Error => "error",
            EventLevel.Warning => "warning",
            EventLevel.Informational => "informational",
            EventLevel.Verbose => "verbose",
            // An event is written on SqlClient's thread, where nothing may throw.
            _ => ((int)level).ToString(CultureInfo.InvariantCulture),
        };
}
