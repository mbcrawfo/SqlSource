using System;
using System.Collections.Generic;
using System.IO;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Processes;

namespace SqlSource.Tool.Testing;

// A host for a test that calls a part of the tool and not the whole command: nothing is written anywhere.
internal static class Hosts
{
    // The files of a host: the same as the released tool's, which makes or empties the file on disk.
    public static Stream CreateFile(string path) =>
        new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);

    public static ToolHost Create(
        string workingDirectory,
        IProcessRunner processes,
        string tempDirectory,
        int processorCount = 4,
        IReadOnlyDictionary<string, string>? environment = null
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
            TimeProvider.System,
            CreateFile
        );

    // The environment of the test process, for a run of the real "dotnet".
    public static ToolHost Real(string workingDirectory, string tempDirectory) =>
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
            TimeProvider.System,
            CreateFile
        );
}
