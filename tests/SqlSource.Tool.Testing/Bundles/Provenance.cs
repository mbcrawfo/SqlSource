using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Testing.Bundles;

// Where an entry's answers came from, which a renaming must not change though it changes every name: the plan, the
// table match, each column's nullableSource and each parameter's typeSource, one line each and by position, so that
// two entries whose names differ compare equal.
internal static class Provenance
{
    // The lines of the one entry of a sidecar's text.
    public static string Of(string expected)
    {
        var sidecar =
            JsonNode.Parse(expected) as JsonObject
            ?? throw new ArgumentException("The sidecar is no JSON object.", nameof(expected));
        var entries =
            sidecar[SidecarKeys.Queries] as JsonObject
            ?? throw new ArgumentException("The sidecar has no queries.", nameof(expected));
        if (entries.Count != 1 || entries.Single().Value is not JsonObject entry)
        {
            throw new ArgumentException("The sidecar does not hold exactly one entry.", nameof(expected));
        }

        var lines = new List<string>
        {
            $"{SidecarKeys.Plan} {TextOf(entry[SidecarKeys.Plan])}",
            $"{SidecarKeys.TableMatch} {TextOf(entry[SidecarKeys.TableMatch])}",
        };
        lines.AddRange(Each(entry, SidecarKeys.Columns, SidecarKeys.NullableSource));
        lines.AddRange(Each(entry, SidecarKeys.Parameters, SidecarKeys.TypeSource));
        return string.Join('\n', lines);
    }

    private static IEnumerable<string> Each(JsonObject entry, string list, string member) =>
        (entry[list] as JsonArray ?? []).Select((item, index) => $"{list}[{index}].{member} {TextOf(item?[member])}");

    private static string TextOf(JsonNode? node) => node?.ToJsonString() ?? "null";
}
