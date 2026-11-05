using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// Every difference between the entry a bundle expects and the one its replay gave, as a sidecar writes them.
/// </summary>
/// <remarks>
/// <para>
/// Each entry is written by <c>SidecarWriter</c> as a sidecar of that one entry, with one format version and one tool
/// version for both, and the two are compared member by member: so a difference is one of the file, in its names, and
/// a member the writer leaves out for one entry is missing.  Unlike <c>describe --check</c>, every member is compared,
/// the server's version and the provenance among them, since a replay must give the bundle's answer whole.
/// </para>
/// <para>
/// A difference is <c>&lt;path&gt;: &lt;expected JSON&gt; -&gt; &lt;replayed JSON&gt;</c>.  The path starts inside the
/// entry, as <c>columns[2].nullable</c>, and a member or an element that one side lacks is <c>(none)</c>.  The query's
/// own key is the entry's root, so a query of another name is the one difference <c>(key)</c>.
/// </para>
/// </remarks>
internal static class EntryDifferences
{
    private const string None = "(none)";

    // Any version: both entries are written with it.
    private const string ToolVersion = "replay";

    private static readonly JsonSerializerOptions ValueOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The differences in the order of the file, or none when the two entries are written the same.</summary>
    public static IReadOnlyList<string> Of(SidecarEntry expected, SidecarEntry replayed)
    {
        if (!string.Equals(expected.Name, replayed.Name, StringComparison.Ordinal))
        {
            return [Difference("(key)", Text(JsonValue.Create(expected.Name)), Text(JsonValue.Create(replayed.Name)))];
        }

        List<string> differences = [];
        Compare(string.Empty, EntryOf(expected), EntryOf(replayed), differences);
        return differences;
    }

    // The entry's object, as the sidecar of it alone writes it.
    private static JsonObject EntryOf(SidecarEntry entry)
    {
        var text = SidecarWriter.Write(
            new Sidecar(SidecarFormat.Version, ToolVersion, new EquatableArray<SidecarEntry>([entry]))
        );
        var queries =
            JsonNode.Parse(text)?["queries"]?.AsObject()
            ?? throw new InvalidOperationException("The sidecar of an entry has no queries.");
        return queries.Single().Value?.AsObject()
            ?? throw new InvalidOperationException("The entry of a sidecar is not an object.");
    }

    // A null node is the JSON null; a member that is missing is not compared here but named as (none) by its parent.
    private static void Compare(string path, JsonNode? expected, JsonNode? replayed, List<string> differences)
    {
        if (expected is JsonObject left && replayed is JsonObject right)
        {
            foreach (
                var key in left.Select(static member => member.Key).Union(right.Select(static member => member.Key))
            )
            {
                var at = path.Length == 0 ? key : path + "." + key;
                var inLeft = left.TryGetPropertyValue(key, out var leftValue);
                var inRight = right.TryGetPropertyValue(key, out var rightValue);
                if (inLeft && inRight)
                {
                    Compare(at, leftValue, rightValue, differences);
                }
                else
                {
                    differences.Add(Difference(at, inLeft ? Text(leftValue) : None, inRight ? Text(rightValue) : None));
                }
            }

            return;
        }

        if (expected is JsonArray leftItems && replayed is JsonArray rightItems)
        {
            for (var index = 0; index < Math.Max(leftItems.Count, rightItems.Count); index++)
            {
                var at = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                if (index < leftItems.Count && index < rightItems.Count)
                {
                    Compare(at, leftItems[index], rightItems[index], differences);
                }
                else
                {
                    differences.Add(
                        Difference(
                            at,
                            index < leftItems.Count ? Text(leftItems[index]) : None,
                            index < rightItems.Count ? Text(rightItems[index]) : None
                        )
                    );
                }
            }

            return;
        }

        // A value, or an object against what is not one: both were written by one writer, so their texts compare.
        var expectedText = Text(expected);
        var replayedText = Text(replayed);
        if (!string.Equals(expectedText, replayedText, StringComparison.Ordinal))
        {
            differences.Add(Difference(path, expectedText, replayedText));
        }
    }

    private static string Difference(string path, string expected, string replayed) =>
        $"{path}: {expected} -> {replayed}";

    // A value as JSON on one line: the writer escapes a line break in a string.
    private static string Text(JsonNode? value) => value?.ToJsonString(ValueOptions) ?? "null";
}
