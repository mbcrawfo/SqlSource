using System.Collections.Generic;
using SqlSource.Tool.Describing.Capture;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The mapping file of a renaming: each replacement with its class and its original.  It is the user's to keep
/// private, and is never part of a bundle.
/// </summary>
internal static class MappingFile
{
    /// <summary>The version of the file's layout.</summary>
    public const int Version = 1;

    private const string VersionKey = "mappingVersion";
    private const string NamesKey = "names";
    private const string ReplacementKey = "replacement";
    private const string ClassKey = "class";
    private const string OriginalKey = "original";

    /// <summary>The file: indented, with <c>\n</c> line ends and a final line break.</summary>
    public static string Write(IReadOnlyList<MappedName> names) =>
        JsonFile.Write(json =>
        {
            json.WriteStartObject();
            json.WriteNumber(VersionKey, Version);
            json.WriteStartArray(NamesKey);
            foreach (var name in names)
            {
                json.WriteStartObject();
                json.WriteString(ReplacementKey, name.Replacement);
                json.WriteString(ClassKey, NameClasses.WordOf(name.Class));
                json.WriteString(OriginalKey, name.Original);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        });
}
