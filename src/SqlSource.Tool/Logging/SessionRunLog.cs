using System;
using System.Linq;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Logging;

/// <summary>
/// A log over the run's that writes every occurrence of one database's connection value as <c>***</c>: in the
/// event's name and in every string of it at any depth, the names of members included.
/// </summary>
/// <remarks>
/// <para>
/// A describer is given one, with the engine as its prefix, so that a describer that logs the value by mistake, or
/// passes on a driver's message that holds it, leaks nothing, and so that it cannot write an event of the run's.
/// The run writes its own events of a database through one too, without a prefix: they hold what a describer gave.
/// </para>
/// <para>
/// The whole value is removed and nothing less: the tool does not read a connection string, so it knows no part
/// of one.  <c>docs/tech-debt/TD-0034</c> says what that leaves.
/// </para>
/// </remarks>
/// <param name="inner">The log of the run.</param>
/// <param name="secret">The connection's value.</param>
/// <param name="prefix">What stands before each event's name: the engine and a dot, for a describer.</param>
internal sealed class SessionRunLog(IRunLog inner, string secret, string? prefix = null) : IRunLog
{
    public void Write(string name, params LogMember[] members) =>
        inner.Write(prefix + Redaction.Of(name, secret), [.. members.Select(Clean)]);

    private LogMember Clean(LogMember member) => new(Redaction.Of(member.Name, secret), Clean(member.Value));

    private LogValue Clean(LogValue value) =>
        value switch
        {
            LogValue.Text text => Redaction.Of(text.Value, secret),
            LogValue.Items items => LogValue.ListOf(items.Values.Select(Clean)),
            LogValue.Members map => LogValue.MapOf([.. map.Values.Select(Clean)]),
            // The kinds that hold no string.  The set is closed: a new kind is decided here, not let through.
            LogValue.Nothing or LogValue.Flag or LogValue.Number => value,
            _ => throw new InvalidOperationException($"Unknown kind of log value {value.GetType().Name}."),
        };
}
