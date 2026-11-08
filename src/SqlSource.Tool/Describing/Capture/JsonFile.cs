using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// How the tool writes a JSON file: indented, with <c>\n</c> line ends on every platform, and with the relaxed
/// encoder, so that a reviewer reads a value's quotes and angle brackets as they are.
/// </summary>
internal static class JsonFile
{
    /// <summary>The options of every JSON file the tool writes.</summary>
    public static JsonWriterOptions Options { get; } =
        new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The file that <paramref name="write" /> writes: UTF-8 text with a final line break.</summary>
    public static string Write(Action<Utf8JsonWriter> write) => Text(write) + "\n";

    /// <summary>The same as <see cref="Write" /> with no final line break: a value held inside another file.</summary>
    public static string Text(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, Options))
        {
            write(json);
        }

        // The writer indents with the platform's line end.  A line break inside a value is written as an escape,
        // so only the writer's own are replaced.
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
