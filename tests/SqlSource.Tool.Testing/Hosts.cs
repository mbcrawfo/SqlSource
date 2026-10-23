using System;
using System.Collections.Generic;
using System.IO;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Processes;

namespace SqlSource.Tool.Testing;

// A host for a test that calls a part of the tool and not the whole command: its output and its errors go nowhere,
// and a file it makes is made on disk, as the released tool makes it.  Its clock stands still, unless the test brings
// one.  Its descriptions of .NET and of the system are its own, so a test of the log can fix them.
internal static class Hosts
{
    public const string Runtime = ".NET of the test";

    public const string OperatingSystem = "the system of the test";

    public static ToolHost Create(
        string workingDirectory,
        IProcessRunner processes,
        string tempDirectory,
        int processorCount = 4,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeProvider? time = null
    ) =>
        new(
            TextWriter.Null,
            TextWriter.Null,
            workingDirectory,
            name => environment?.GetValueOrDefault(name),
            processes,
            tempDirectory,
            processorCount,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance,
            time ?? new TestClock(),
            ToolHost.NewFile,
            Runtime,
            OperatingSystem
        );

    // The environment of the test process, for a run of the real "dotnet".
    public static ToolHost Real(string workingDirectory, string tempDirectory, TimeProvider? time = null) =>
        new(
            TextWriter.Null,
            TextWriter.Null,
            workingDirectory,
            Environment.GetEnvironmentVariable,
            new ProcessRunner(),
            tempDirectory,
            Environment.ProcessorCount,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance,
            time ?? new TestClock(),
            ToolHost.NewFile,
            Runtime,
            OperatingSystem
        );
}
