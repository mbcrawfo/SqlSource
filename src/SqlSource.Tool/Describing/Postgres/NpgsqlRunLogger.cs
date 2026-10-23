using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Npgsql;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Npgsql's own log, forwarded into the run's log as <c>postgres.driver</c> events, for a run that has a log.
/// </summary>
/// <remarks>
/// <para>
/// What is written is an allowlist of event ids, not a level: a message is forwarded only when its category lists its
/// id, and every other message is dropped before it is formatted.  The list was read from the source of Npgsql 10.0.3,
/// its <c>NpgsqlEventId.cs</c> and <c>LogMessages.cs</c>, and holds the ids whose message is the tool's own command
/// text, a fixed text or a number, of the features the describer uses.  So a message that a later Npgsql adds is
/// dropped until someone reads it, and a test fails on any other version of Npgsql until the list is read again.
/// </para>
/// <para>
/// Left out: the messages of an opened or closed connection, which name <c>{Host}:{Port}/{Database}</c>; a notice of
/// the server, whose text can name a role or a database; every message of id 0, the id of the messages that Npgsql
/// writes without <c>LoggerMessage</c>, some of which quote the user's principal or the server's text; and so the
/// whole of <c>Npgsql.Exception</c>, which writes nothing else.  Trace is never forwarded, a category with no listed
/// id is never enabled, and a scope is not kept.  Parameter logging is off: <see cref="PostgresConnection" /> turns
/// off the connection string's <c>Log Parameters</c>, which Npgsql honours whatever the data source's own setting.
/// </para>
/// <para>
/// An exception is written as its type alone: its message is the server's or the socket's text, which can name the
/// endpoint, a role or a database.  The log the describer is given removes each whole form of the connection's value
/// from a message, and its passwords where they stand as tokens, and no other part of it, as
/// <c>docs/tech-debt/TD-0034</c> says.
/// </para>
/// </remarks>
/// <param name="log">The describer's log.  It is the run's, and is not closed here.</param>
internal sealed class NpgsqlRunLogger(IRunLog log) : ILoggerFactory
{
    /// <summary>The name of the event, before the engine the describer's log puts in front of it.</summary>
    public const string EventName = "driver";

    // The ids forwarded, by the category that writes them.  Each is the tool's command text, a fixed text or a number
    // in Npgsql 10.0.3.  CancellingCommand is written on the connection's logger, not the command's.
    private static readonly Dictionary<string, HashSet<int>> Forwarded = new(StringComparer.Ordinal)
    {
        ["Npgsql.Command"] =
        [
            NpgsqlEventId.ExecutingCommand,
            NpgsqlEventId.CommandExecutionCompleted,
            NpgsqlEventId.ExecutingInternalCommand,
        ],
        ["Npgsql.Connection"] = [NpgsqlEventId.CancellingCommand, NpgsqlEventId.ExceptionWhenClosingPhysicalConnection],
        ["Npgsql.Transaction"] =
        [
            NpgsqlEventId.StartedTransaction,
            NpgsqlEventId.CommittedTransaction,
            NpgsqlEventId.RolledBackTransaction,
            NpgsqlEventId.ExceptionDuringTransactionDispose,
        ],
    };

    private static readonly HashSet<int> None = [];

    /// <summary>The driver's factory, or null when the log keeps nothing and so the driver need not log.</summary>
    /// <param name="log">The describer's log.</param>
    public static ILoggerFactory? For(IRunLog log) => log.Writes ? new NpgsqlRunLogger(log) : null;

    public ILogger CreateLogger(string categoryName) =>
        new Category(log, categoryName, Forwarded.GetValueOrDefault(categoryName, None));

    public void AddProvider(ILoggerProvider provider)
    {
        // The log of the run is the one place the driver's messages go.
    }

    public void Dispose()
    {
        // The log is the run's, and the run closes it.
    }

    private sealed class Category(IRunLog log, string category, HashSet<int> ids) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NoScope.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            ids.Count != 0 && logLevel != LogLevel.None && logLevel >= LogLevel.Debug;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel) || !ids.Contains(eventId.Id))
            {
                return;
            }

            log.Write(
                EventName,
                new("category", category),
                new("level", LevelOf(logLevel)),
                new("eventId", eventId.Id),
                new("message", formatter(state, exception)),
                new("exception", exception?.GetType().FullName)
            );
        }

        private static string LevelOf(LogLevel level) =>
            level switch
            {
                LogLevel.Debug => "debug",
                LogLevel.Information => "information",
                LogLevel.Warning => "warning",
                LogLevel.Error => "error",
                LogLevel.Critical => "critical",
                LogLevel.Trace or LogLevel.None or _ => throw new ArgumentOutOfRangeException(
                    nameof(level),
                    level,
                    "Not a level that is forwarded."
                ),
            };
    }

    private sealed class NoScope : IDisposable
    {
        public static NoScope Instance { get; } = new();

        public void Dispose()
        {
            // A scope holds nothing.
        }
    }
}
