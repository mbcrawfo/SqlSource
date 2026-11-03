using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace SqlSource.Tool.StandIn;

// The stand-in of the sqlsource tool.  The build runs it in the folder of the project it describes, where the test
// leaves stand-in.json, what the run does: the files to create, each a path and its text, and to delete, the lines to
// print to each stream, and the code to exit with.  What the file leaves out the run does not do, but for the lines of
// the standard output, which are then a line of a summary, as of a run that described nothing, and the code, which is
// then 0: so without the file it prints that line and exits with 0.  Each run first adds a line to stand-in.log in the
// same folder, with its arguments and the lines of the manifest it was given, so that a test counts the runs and reads
// what each was given.
internal static class Program
{
    private const string ScriptFile = "stand-in.json";
    private const string LogFile = "stand-in.log";

    // A line of a summary in the form of the tool's own under --online.
    private const string SummaryLine = "sqlsource: summary: billing (postgres): 0 described, 1 skipped, 0 failed";

    private static async Task<int> Main(string[] args)
    {
        var manifestAt = Array.IndexOf(args, "--manifest") + 1;
        var manifest =
            manifestAt > 0 && manifestAt < args.Length ? await File.ReadAllLinesAsync(args[manifestAt]) : null;
        await File.AppendAllTextAsync(
            LogFile,
            JsonSerializer.Serialize(new StandInRun(args, manifest)) + Environment.NewLine
        );

        using var script = JsonDocument.Parse(File.Exists(ScriptFile) ? await File.ReadAllTextAsync(ScriptFile) : "{}");
        var root = script.RootElement;
        var files = root.TryGetProperty("create", out var create) ? create.EnumerateObject() : default;
        foreach (var file in files)
        {
            await File.WriteAllTextAsync(file.Name, file.Value.GetString());
        }

        foreach (var path in Strings(root, "delete"))
        {
            File.Delete(path);
        }

        var lines = root.TryGetProperty("out", out _) ? Strings(root, "out") : [SummaryLine];
        foreach (var line in lines)
        {
            await Console.Out.WriteLineAsync(line);
        }

        foreach (var line in Strings(root, "error"))
        {
            await Console.Error.WriteLineAsync(line);
        }

        return root.TryGetProperty("exitCode", out var exitCode) ? exitCode.GetInt32() : 0;
    }

    // The strings of an array of the script, none when it is left out.
    private static IEnumerable<string> Strings(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var array))
        {
            foreach (var item in array.EnumerateArray())
            {
                yield return item.GetString()!;
            }
        }
    }
}

// A line of stand-in.log.
internal sealed record StandInRun(IReadOnlyList<string> Arguments, IReadOnlyList<string>? Manifest);
