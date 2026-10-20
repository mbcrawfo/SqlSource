using System;
using Microsoft.Extensions.Logging;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Npgsql's own log, forwarded into the run's log as <c>postgres.driver</c> events, for a run that has a log.
/// </summary>
/// <remarks>
/// <para>
/// <c>Npgsql.Command</c> and <c>Npgsql.Transaction</c> are forwarded from Debug up, and every other category from
/// Warning up.  Trace is never forwarded, and <see cref="LogLevel.None" /> is never enabled.  Parameter logging is
/// off, and a scope is not kept.
/// </para>
/// <para>
/// <c>Npgsql.Connection</c> is kept at Warning like the categories the tool does not know, and its exception is
/// not written: an exception of a failed connect names the endpoint, which the log must not hold.  The levels
/// at which the other categories are forwarded trust Npgsql's messages not to name the host, the user or the
/// database; <c>docs/tech-debt/TD-0048</c> says what that rests on.  The log the describer is given removes the
/// connection's whole value from a message, and its passwords where they stand as tokens, and no other part of
/// it, as <c>docs/tech-debt/TD-0034</c> says.
/// </para>
/// </remarks>
/// <param name="log">The describer's log.  It is the run's, and is not closed here.</param>
internal sealed class NpgsqlRunLogger(IRunLog log) : ILoggerFactory
{
    /// <summary>The name of the event, before the engine the describer's log puts in front of it.</summary>
    public const string EventName = "driver";

    private const string CommandCategory = "Npgsql.Command";
    private const string TransactionCategory = "Npgsql.Transaction";
    private const string ConnectionCategory = "Npgsql.Connection";

    /// <summary>The driver's factory, or null when the log keeps nothing and so the driver need not log.</summary>
    /// <param name="log">The describer's log.</param>
    public static ILoggerFactory? For(IRunLog log) => log.Writes ? new NpgsqlRunLogger(log) : null;

    public ILogger CreateLogger(string categoryName) =>
        new Category(
            log,
            categoryName,
            categoryName is CommandCategory or TransactionCategory ? LogLevel.Debug : LogLevel.Warning
        );

    public void AddProvider(ILoggerProvider provider)
    {
        // The log of the run is the one place the driver's messages go.
    }

    public void Dispose()
    {
        // The log is the run's, and the run closes it.
    }

    private sealed class Category(IRunLog log, string category, LogLevel minimum) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NoScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimum;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            log.Write(
                EventName,
                new("category", category),
                new("level", LevelOf(logLevel)),
                new("eventId", eventId.Id),
                new("message", formatter(state, exception)),
                new("exception", ExceptionOf(exception))
            );
        }

        private LogValue ExceptionOf(Exception? exception) =>
            exception is null || string.Equals(category, ConnectionCategory, StringComparison.Ordinal)
                ? LogValue.Null
                : exception.GetType().FullName + ": " + exception.Message;

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
