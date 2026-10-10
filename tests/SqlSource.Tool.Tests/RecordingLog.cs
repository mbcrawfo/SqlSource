using System.Collections.Generic;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Tests;

// A log that keeps what it was given.
internal sealed class RecordingLog : IRunLog
{
    public List<(string Name, LogMember[] Members)> Events { get; } = [];

    public void Write(string name, params LogMember[] members) => Events.Add((name, members));
}
