using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SqlSource.Tool.Testing;

// The lines of a log, each parsed: a line that is not one JSON object fails the test that reads it.
internal static class LogReader
{
    public static JsonElement[] Read(string path) => [.. File.ReadAllLines(path).Select(Parse)];

    public static string?[] Names(string path) => [.. Read(path).Select(EventOf)];

    // The name of the event a line logs.
    public static string? EventOf(JsonElement line) => line.GetProperty("event").GetString();

    // The lines of the log that log the event of the name, in order.
    public static IReadOnlyList<JsonElement> Events(string path, string name) =>
        [.. Read(path).Where(line => EventOf(line) == name)];

    public static JsonElement Parse(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement.Clone()
            : throw new InvalidDataException($"A line of the log is {document.RootElement.ValueKind}, not an object.");
    }
}
