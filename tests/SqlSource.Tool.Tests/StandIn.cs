using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SqlSource.Tool.Testing;

namespace SqlSource.Tool.Tests;

// The stand-in of the tool, tests/SqlSource.Tool.StandIn, built into this project's output, which a test gives a
// build of a fixture as its command.  It reads what to do from stand-in.json in the project's folder, and records each
// run in stand-in.log there; its Program.cs says how.
internal static class StandIn
{
    private const string ScriptFile = "stand-in.json";
    private const string LogFile = "stand-in.log";

    // The switch that makes the stand-in a build's command: the dotnet that started the tests running its DLL, each
    // path in quotes.  A value of a -p switch is escaped for MSBuild, so that no character of a path splits it or is
    // read as a property, and MSBuild gives the command its quotes back.
    public static string CommandSwitch =>
        "-p:SqlSourceToolCommand="
        + EscapedForMSBuild(
            $"\"{Programs.Dotnet}\" \"{Path.Combine(AppContext.BaseDirectory, "SqlSource.Tool.StandIn.dll")}\""
        );

    // Tells the stand-in what its runs in the folder do: the text of each file to create, by its path from the folder,
    // the paths of the files to delete, the lines to print to each stream, a line of a summary when no output is
    // given, and the code to exit with.
    public static void Tell(
        string folder,
        Dictionary<string, string>? create = null,
        IReadOnlyList<string>? delete = null,
        IReadOnlyList<string>? output = null,
        IReadOnlyList<string>? error = null,
        int exitCode = 0
    )
    {
        var script = new Dictionary<string, object> { ["exitCode"] = exitCode };
        Add("create", create);
        Add("delete", delete);
        Add("out", output);
        Add("error", error);
        File.WriteAllText(Path.Combine(folder, ScriptFile), JsonSerializer.Serialize(script));

        void Add(string name, object? value)
        {
            if (value is not null)
            {
                script[name] = value;
            }
        }
    }

    // The runs of the stand-in in the folder, in their order.
    public static IReadOnlyList<StandInRun> RunsIn(string folder)
    {
        var log = Path.Combine(folder, LogFile);
        return File.Exists(log) ? [.. File.ReadAllLines(log).Select(Read)] : [];
    }

    // A line of the log, which the stand-in writes from its own record of the same shape.
    private static StandInRun Read(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        var manifest = root.GetProperty("Manifest");
        return new StandInRun(
            Strings(root.GetProperty("Arguments")),
            manifest.ValueKind == JsonValueKind.Null ? null : Strings(manifest)
        );

        static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString()!)];
    }

    private static string EscapedForMSBuild(string value)
    {
        var escaped = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character) || "/\\.-_: ".Contains(character, StringComparison.Ordinal))
            {
                _ = escaped.Append(character);
            }
            else if (character < 0x80)
            {
                _ = escaped.Append('%').Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                _ = escaped.Append(character);
            }
        }

        return escaped.ToString();
    }
}

// A run of the stand-in: the arguments it was given, and the lines of the manifest they named, or none.
internal sealed record StandInRun(IReadOnlyList<string> Arguments, IReadOnlyList<string>? Manifest);
