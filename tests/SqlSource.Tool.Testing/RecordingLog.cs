using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Testing;

// A log that keeps what it was given, from any thread, as SqlClient writes from the thread that runs its command.
internal sealed class RecordingLog : IRunLog
{
    private readonly Lock _gate = new();
    private readonly List<(string Name, LogMember[] Members)> _events = [];

    public bool Writes => true;

    // What was written so far, in order: a copy, so that a write that follows does not change it.
    public IReadOnlyList<(string Name, LogMember[] Members)> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public void Write(string name, params LogMember[] members)
    {
        lock (_gate)
        {
            _events.Add((name, members));
        }
    }

    // The value of the one member of that name.
    public static LogValue Member(LogMember[] members, string name) =>
        members.Single(member => member.Name == name).Value;

    public static string[] NamesOf(LogMember[] members) => [.. members.Select(member => member.Name)];
}
