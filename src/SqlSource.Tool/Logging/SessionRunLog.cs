using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Logging;

/// <summary>
/// A log over the run's that writes one database's connection secret as <c>***</c>: the whole value wherever it
/// stands, and each secret part where it stands as a token, in the event's name and in every string of it at any
/// depth, the names of members included, and in the strings of the event that make it together.
/// </summary>
/// <remarks>
/// <para>
/// A describer is given one, with the engine as its prefix, so that a describer that logs the value by mistake, or
/// passes on a driver's message that holds it, leaks nothing, and so that it cannot write an event of the run's.
/// The run writes its own events of a database through one too, without a prefix: they hold what a describer gave.
/// </para>
/// <para>
/// Each string is searched alone, and then all of them joined (<see cref="Redaction.OfJoined" />), in the order they
/// were written: the name, then each member's name and the strings of its value, depth first; and then the texts of
/// the values joined without the names between them.  A describer can write a value half in one member and half in
/// the next, or a value that holds a line break over the items of a list.
/// </para>
/// <para>
/// The parts are those the describer said are secret (<see cref="ConnectionSecret" />): the tool does not read a
/// connection string itself.  <c>docs/tech-debt/TD-0034</c> says what that leaves.
/// </para>
/// </remarks>
/// <param name="inner">The log of the run.</param>
/// <param name="secret">The connection's value and its secret parts.</param>
/// <param name="prefix">What stands before each event's name: the engine and a dot, for a describer.</param>
internal sealed class SessionRunLog(IRunLog inner, ConnectionSecret secret, string? prefix = null) : IRunLog
{
    public bool Writes => inner.Writes;

    public void Write(string name, params LogMember[] members)
    {
        List<string> strings = [name];
        List<int> values = [];
        foreach (var member in members)
        {
            strings.Add(member.Name);
            Flatten(member.Value, strings, values);
        }

        var cleaned = Redaction.OfJoined(strings, secret);
        // The names stand between the values in the order of the event but are no part of what a describer wrote of a
        // value in two members, so the values are searched joined without them too.
        var cleanedValues = Redaction.OfJoined([.. values.Select(index => cleaned[index])], secret);
        for (var i = 0; i < values.Count; i++)
        {
            cleaned[values[i]] = cleanedValues[i];
        }

        var rest = new Queue<string>(cleaned);
        var cleanedName = prefix + rest.Dequeue();
        inner.Write(cleanedName, [.. members.Select(member => Rebuild(member, rest))]);
    }

    // The strings of a value in the order Rebuild takes them back: depth first, as written.  The indexes of those that
    // are the text of a value, and not the name of a member, are kept.
    private static void Flatten(LogValue value, List<string> strings, List<int> values)
    {
        switch (value)
        {
            case LogValue.Text text:
                values.Add(strings.Count);
                strings.Add(text.Value);
                break;
            case LogValue.Items items:
                foreach (var item in items.Values)
                {
                    Flatten(item, strings, values);
                }

                break;
            case LogValue.Members map:
                foreach (var member in map.Values)
                {
                    strings.Add(member.Name);
                    Flatten(member.Value, strings, values);
                }

                break;
            // The kinds that hold no string.  The set is closed: a new kind is decided here, not let through.
            case LogValue.Nothing or LogValue.Flag or LogValue.Number:
                break;
            default:
                throw new InvalidOperationException($"Unknown kind of log value {value.GetType().Name}.");
        }
    }

    private static LogMember Rebuild(LogMember member, Queue<string> cleaned) =>
        new(cleaned.Dequeue(), Rebuild(member.Value, cleaned));

    private static LogValue Rebuild(LogValue value, Queue<string> cleaned) =>
        value switch
        {
            LogValue.Text => cleaned.Dequeue(),
            LogValue.Items items => LogValue.ListOf(items.Values.Select(item => Rebuild(item, cleaned))),
            LogValue.Members map => LogValue.MapOf([.. map.Values.Select(member => Rebuild(member, cleaned))]),
            // The kinds that hold no string.  The set is closed: a new kind is decided here, not let through.
            LogValue.Nothing or LogValue.Flag or LogValue.Number => value,
            _ => throw new InvalidOperationException($"Unknown kind of log value {value.GetType().Name}."),
        };
}
