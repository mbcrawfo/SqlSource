using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The mapping file of a renaming: each replacement with its class and its original.  It is the user's to keep
/// private, and is never part of a bundle.
/// </summary>
internal static class MappingFile
{
    /// <summary>The version of the file's layout.</summary>
    public const int Version = 1;

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The file: indented, with <c>\n</c> line ends and a final line break.</summary>
    public static string Write(IReadOnlyList<MappedName> names)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, Options))
        {
            json.WriteStartObject();
            json.WriteNumber("mappingVersion", Version);
            json.WriteStartArray("names");
            foreach (var name in names)
            {
                json.WriteStartObject();
                json.WriteString("replacement", name.Replacement);
                json.WriteString("class", NameClasses.WordOf(name.Class));
                json.WriteString("original", name.Original);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
}
