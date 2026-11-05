using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>A string of a JSON tree as the renaming meets it.</summary>
/// <param name="Path">
/// Its path from where the walk started: members joined by <c>.</c>, an element of an array as <c>[]</c>, so that every
/// element has its array's rule.
/// </param>
/// <param name="Value">The string.</param>
/// <param name="Row">The object that holds it as a member, or null for an element of an array.</param>
/// <param name="RowPath">The path of the object or the array that holds it.</param>
internal readonly record struct JsonString(string Path, string Value, JsonObject? Row, string RowPath);

/// <summary>The paths and the strings of a JSON tree, as the renaming's steps walk it.</summary>
internal static class JsonStrings
{
    /// <summary>The path of a member of the object at <paramref name="parent" />.</summary>
    public static string MemberOf(string parent, string key) => parent.Length == 0 ? key : parent + "." + key;

    /// <summary>The path of an element of the array at <paramref name="array" />.</summary>
    public static string ElementOf(string array) => array + "[]";

    /// <summary>
    /// Visits every string of the tree in the order of its text.  What the visit returns replaces the string, unless
    /// it is null; numbers, booleans and nulls are not visited.
    /// </summary>
    /// <param name="node">The tree, or null.</param>
    /// <param name="path">The path of the tree's root.</param>
    /// <param name="visit">Takes each string, and gives its replacement or null.</param>
    public static void Visit(JsonNode? node, string path, Func<JsonString, string?> visit)
    {
        switch (node)
        {
            case JsonObject members:
                // A copy, so that a member is replaced while the object is walked.
                foreach (var (key, value) in members.ToArray())
                {
                    var at = MemberOf(path, key);
                    if (StringOf(value) is { } text)
                    {
                        if (visit(new JsonString(at, text, members, path)) is { } replaced)
                        {
                            members[key] = replaced;
                        }
                    }
                    else
                    {
                        Visit(value, at, visit);
                    }
                }

                break;
            case JsonArray elements:
                var element = ElementOf(path);
                for (var index = 0; index < elements.Count; index++)
                {
                    if (StringOf(elements[index]) is { } text)
                    {
                        if (visit(new JsonString(element, text, null, path)) is { } replaced)
                        {
                            elements[index] = replaced;
                        }
                    }
                    else
                    {
                        Visit(elements[index], element, visit);
                    }
                }

                break;
            default:
                break;
        }
    }

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
