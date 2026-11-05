using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SqlSource.Tool.Tests.Spike10;

// Where a value sits in a JSON tree: the object or array that holds it, and its name or index there.
internal sealed record JsonLocation(JsonNode Parent, string? Name, int Index)
{
    public bool IsMember => Name is not null;

    public bool Exists => !IsMember || ((JsonObject)Parent).ContainsKey(Name!);

    public JsonNode? Value => IsMember ? ((JsonObject)Parent)[Name!] : ((JsonArray)Parent)[Index];

    public JsonValueKind Kind =>
        Value switch
        {
            null => JsonValueKind.Null,
            JsonObject => JsonValueKind.Object,
            JsonArray => JsonValueKind.Array,
            var value => value.GetValueKind(),
        };

    public void Set(JsonNode? value)
    {
        if (IsMember)
        {
            ((JsonObject)Parent)[Name!] = value;
        }
        else
        {
            ((JsonArray)Parent)[Index] = value;
        }
    }
}

// The mutations of the phase 10.0 spike, and the labels of what a mutated replay gave.
internal static class JsonMutations
{
    public const string Removed = "removed";
    public const string StringX = "string_x";
    public const string StringEmpty = "string_empty";
    public const string NumberPlusOne = "number_plus1";
    public const string BoolNegated = "bool_negated";
    public const string ArrayDropLast = "array_dropLast";
    public const string ArraySwapFirstTwo = "array_swapFirstTwo";

    // Every location in the tree by its normalized path: a member is "parent.name", an index is "[]".  An object in an
    // array has no location of its own; the array has.
    public static void Collect(JsonNode? node, string path, Dictionary<string, List<JsonLocation>> into)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToArray())
                {
                    var child = path + "." + name;
                    Add(into, child, new JsonLocation(obj, name, -1));
                    Collect(value, child, into);
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var element = array[i];
                    var child = path + "[]";
                    if (element is not JsonObject)
                    {
                        Add(into, child, new JsonLocation(array, null, i));
                    }

                    Collect(element, child, into);
                }

                break;
            default:
                break;
        }
    }

    private static void Add(Dictionary<string, List<JsonLocation>> into, string path, JsonLocation location)
    {
        if (!into.TryGetValue(path, out var list))
        {
            list = [];
            into[path] = list;
        }

        list.Add(location);
    }

    // The mutations that fit at least one of the locations.
    public static IReadOnlyList<string> Fitting(IReadOnlyList<JsonLocation> locations)
    {
        var names = new List<string>();
        if (locations.Any(static l => l.IsMember))
        {
            names.Add(Removed);
        }

        foreach (var kind in locations.Select(static l => l.Kind).Distinct())
        {
            switch (kind)
            {
                case JsonValueKind.String:
                    names.Add(StringX);
                    names.Add(StringEmpty);
                    break;
                case JsonValueKind.Number:
                    names.Add(NumberPlusOne);
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (!names.Contains(BoolNegated, StringComparer.Ordinal))
                    {
                        names.Add(BoolNegated);
                    }

                    break;
                case JsonValueKind.Array:
                    names.Add(ArrayDropLast);
                    if (locations.Any(static l => l.Value is JsonArray { Count: >= 2 } a && a[0] is JsonObject))
                    {
                        names.Add(ArraySwapFirstTwo);
                    }

                    break;
                case JsonValueKind.Undefined:
                case JsonValueKind.Object:
                case JsonValueKind.Null:
                default:
                    break;
            }
        }

        return names;
    }

    // Applies the mutation at the location when it fits there, and says whether it did.
    public static bool Apply(JsonLocation location, string mutation)
    {
        if (!location.Exists)
        {
            return false;
        }

        var value = location.Value;
        var kind = location.Kind;
        switch (mutation)
        {
            case Removed when location.IsMember:
                _ = ((JsonObject)location.Parent).Remove(location.Name!);
                return true;
            case StringX when kind == JsonValueKind.String:
                location.Set(JsonValue.Create(value!.GetValue<string>() + "_x"));
                return true;
            case StringEmpty when kind == JsonValueKind.String:
                location.Set(JsonValue.Create(string.Empty));
                return true;
            case NumberPlusOne when kind == JsonValueKind.Number:
                var element = value!.GetValue<JsonElement>();
                location.Set(
                    element.TryGetInt64(out var whole)
                        ? JsonValue.Create(whole + 1)
                        : JsonValue.Create(element.GetDouble() + 1)
                );
                return true;
            case BoolNegated when kind is JsonValueKind.True or JsonValueKind.False:
                location.Set(JsonValue.Create(kind == JsonValueKind.False));
                return true;
            case ArrayDropLast when value is JsonArray { Count: > 0 } array:
                array.RemoveAt(array.Count - 1);
                return true;
            case ArraySwapFirstTwo when value is JsonArray { Count: >= 2 } array && array[0] is JsonObject:
                var second = array[1];
                array.RemoveAt(1);
                var first = array[0];
                array.RemoveAt(0);
                array.Insert(0, second);
                array.Insert(1, first);
                return true;
            default:
                return false;
        }
    }

    // `same` when the replay gave what the baseline gave, `entry-meta` when only the hash or database of the entry
    // differ, `entry` for any other entry, `failure:<id>`, `miss:<method>`, `unreadable` or `thrown`.
    public static string Label(SpikeOutcome baseline, SpikeOutcome got)
    {
        switch (got.Kind)
        {
            case SpikeKind.Entry:
                if (
                    baseline.Kind == SpikeKind.Entry
                    && string.Equals(baseline.Text, got.Text, StringComparison.Ordinal)
                )
                {
                    return "same";
                }

                return
                    baseline.Kind == SpikeKind.Entry
                    && string.Equals(WithoutMeta(baseline.Text), WithoutMeta(got.Text), StringComparison.Ordinal)
                    ? "entry-meta"
                    : "entry";
            case SpikeKind.Failure:
                return "failure:" + got.Text.Split(' ')[0];
            case SpikeKind.Miss:
                return "miss:" + got.Text.Split(' ')[0];
            case SpikeKind.Unreadable:
                return "unreadable";
            case SpikeKind.Thrown:
            default:
                return "thrown";
        }
    }

    private static string WithoutMeta(string sidecar)
    {
        var root = (JsonObject)JsonNode.Parse(sidecar)!;
        if (root["queries"] is JsonObject queries)
        {
            foreach (var (_, entry) in queries)
            {
                if (entry is JsonObject query)
                {
                    _ = query.Remove("hash");
                    _ = query.Remove("database");
                }
            }
        }

        return root.ToJsonString();
    }
}
