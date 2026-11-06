using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Tool.Describing.Capture;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The steps of the renaming over the calls of a capture: what is dropped, which names are found first, and every
/// string renamed by its member's rule.  Each walks a call's <c>request</c> and <c>answer</c> in
/// <see cref="RenamingCall.Renamed" />, and asks the engine's part the rule of each member by its path from the call.
/// </summary>
internal static class CaptureRenamer
{
    /// <summary>The member of a call that holds what was asked.</summary>
    public const string Request = "request";

    /// <summary>The member of a call that holds what was answered.</summary>
    public const string Answer = "answer";

    /// <summary>A call as a refusal names it: <c>capture.json call &lt;n&gt; '&lt;method&gt;'</c>.</summary>
    public static string PlaceOf(RenamingCall call) => PlaceOf(call.Index, call.Method);

    /// <summary>A member of a call as a refusal names it, by its path from the call.</summary>
    public static string PlaceOf(RenamingCall call, string path) => PlaceOf(call) + " " + path;

    /// <summary>A member of a call as a refusal names it, as an engine's part is given it.</summary>
    public static string PlaceOf(RenamingField field) => PlaceOf(field.Call, field.Method) + " " + field.Path;

    /// <summary>The calls of a capture, each as captured and as a copy to rename.</summary>
    public static IReadOnlyList<RenamingCall> CallsOf(DescribeCapture capture) =>
        [.. capture.Calls.Select(static (call, index) => new RenamingCall(index, call.Method, Of(call), Of(call)))];

    /// <summary>A capture of the engine named that holds the calls as they are renamed.</summary>
    public static DescribeCapture CaptureOf(string engine, IReadOnlyList<RenamingCall> calls)
    {
        var capture = new DescribeCapture(engine);
        foreach (var call in calls)
        {
            capture.Add(
                new CaptureCall(call.Method, ElementOf(call.Renamed[Request]), ElementOf(call.Renamed[Answer]))
            );
        }

        return capture;
    }

    /// <summary>
    /// Removes every member whose rule is <see cref="FieldRuleKind.Drop" />, at any depth, and empties an array whose
    /// elements' rule is.
    /// </summary>
    public static void Drop(IEngineRenaming engine, IReadOnlyList<RenamingCall> calls)
    {
        foreach (var call in calls)
        {
            Drop(engine, call.Method, call.Renamed, string.Empty);
        }
    }

    /// <summary>
    /// Finds the names of the catalog's members, of every answer and then of every request: a name of a row that is
    /// the engine's is kept, and any other is given its replacement, so that a name takes its class from where a
    /// catalog first holds it.  Then the engine's part registers its own names, call by call.
    /// </summary>
    public static void Register(IEngineRenaming engine, IReadOnlyList<RenamingCall> calls, RenamingRun run)
    {
        foreach (var part in (string[])[Answer, Request])
        {
            foreach (var call in calls)
            {
                JsonStrings.Visit(
                    call.Renamed[part],
                    part,
                    found =>
                    {
                        if (engine.RuleOf(call.Method, found.Path) is { Kind: FieldRuleKind.Name, Class: { } cls })
                        {
                            run.Register(found.Value, cls, IsEngineRow(engine, call.Method, found));
                        }

                        return null;
                    }
                );
            }
        }

        foreach (var call in calls)
        {
            engine.Register(call, run.At(PlaceOf(call)));
        }
    }

    /// <summary>
    /// Renames every string of each call by its member's rule, a string with no rule being refused at its place, and
    /// then lets the engine's part derive what its pipeline makes, call by call.
    /// </summary>
    public static void Rename(IEngineRenaming engine, IReadOnlyList<RenamingCall> calls, RenamingRun run)
    {
        foreach (var call in calls)
        {
            foreach (var part in (string[])[Request, Answer])
            {
                JsonStrings.Visit(call.Renamed[part], part, found => Field(engine, call, found, run));
            }
        }

        foreach (var call in calls)
        {
            engine.Derive(call, run.At(PlaceOf(call)));
        }
    }

    private static string? Field(IEngineRenaming engine, RenamingCall call, JsonString found, RenamingRun run)
    {
        var place = PlaceOf(call, found.Path);
        if (engine.RuleOf(call.Method, found.Path) is not { } rule)
        {
            run.Refuse(new RenamingRefusal(RefusalStep.Token, place, found.Value));
            return null;
        }

        var field = new RenamingField(call.Index, call.Method, found.Path, found.Value, found.Row);
        var engineRow = rule.Kind == FieldRuleKind.Name && IsEngineRow(engine, call.Method, found);
        return run.Field(rule, field, engineRow, place);
    }

    private static string PlaceOf(int index, string method) =>
        $"{BundleFiles.Capture} call {index.ToString(CultureInfo.InvariantCulture)} '{method}'";

    private static bool IsEngineRow(IEngineRenaming engine, string method, JsonString found) =>
        found.Row is { } row && engine.IsEngineRow(method, found.RowPath, row);

    private static void Drop(IEngineRenaming engine, string method, JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject members:
                foreach (var (key, value) in members.ToArray())
                {
                    var at = JsonStrings.MemberOf(path, key);
                    if (engine.RuleOf(method, at) is { Kind: FieldRuleKind.Drop })
                    {
                        _ = members.Remove(key);
                    }
                    else
                    {
                        Drop(engine, method, value, at);
                    }
                }

                break;
            case JsonArray elements:
                var element = JsonStrings.ElementOf(path);
                if (engine.RuleOf(method, element) is { Kind: FieldRuleKind.Drop })
                {
                    elements.Clear();
                    break;
                }

                foreach (var value in elements)
                {
                    Drop(engine, method, value, element);
                }

                break;
            default:
                break;
        }
    }

    // A call as the renaming walks it, and a request or an answer of it as a capture keeps it.
    private static JsonObject Of(CaptureCall call) =>
        new()
        {
            [Request] = JsonNode.Parse(call.Request.GetRawText()),
            [Answer] = JsonNode.Parse(call.Answer.GetRawText()),
        };

    private static JsonElement ElementOf(JsonNode? node)
    {
        using var document = JsonDocument.Parse(node?.ToJsonString(CaptureJson.Options) ?? "null");
        return document.RootElement.Clone();
    }
}
