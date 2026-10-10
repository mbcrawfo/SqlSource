using System.IO;
using System.Linq;
using System.Text.Json;

namespace SqlSource.Tool.Tests;

// The lines of a log, each parsed: a line that is not one JSON object fails the test that reads it.
internal static class LogReader
{
    public static JsonElement[] Read(string path) => [.. File.ReadAllLines(path).Select(Parse)];

    public static string?[] Names(string path) => [.. Read(path).Select(line => line.GetProperty("event").GetString())];

    public static JsonElement Parse(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement.Clone()
            : throw new InvalidDataException($"A line of the log is {document.RootElement.ValueKind}, not an object.");
    }
}
