using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres.Walk;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

// The register walk: the names of a plan's name keys, registered with the scope.
internal static partial class PostgresPlanRenamer
{
    /// <summary>
    /// Registers the names of the plan's name keys, each one raw name in its key's class, but an <c>Alias</c> of the
    /// form <c>&lt;alias&gt;_&lt;n&gt;</c>, which is renamed through its alias.  Does nothing when the call's
    /// <c>answer.plan</c> is no string, or no plan: <see cref="Rename" /> refuses one that does not read.
    /// </summary>
    public static void Register(RenamingCall call, IRenamingScope scope)
    {
        if (
            call.Renamed[CaptureJson.AnswerKey] is not JsonObject answer
            || answer[PlanMember] is not JsonValue value
            || !value.TryGetValue<string>(out var plan)
        )
        {
            return;
        }

        using var document = Parse(plan);
        if (document is null || !IsPlanRoot(document.RootElement))
        {
            return;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            RegisterNode(element.GetProperty(PlanKeys.Plan), scope);
        }
    }

    private static void RegisterNode(JsonElement node, IRenamingScope scope)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var member in node.EnumerateObject())
        {
            if (!Kinds.TryGetValue(member.Name, out var kind))
            {
                continue;
            }

            var value = member.Value;
            if (kind == KeyKind.Structure)
            {
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in value.EnumerateArray())
                    {
                        RegisterNode(child, scope);
                    }
                }
                else
                {
                    RegisterNode(value, scope);
                }
            }
            else if (
                kind == KeyKind.Name
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is { } name
                && !(member.NameEquals(PlanKeys.Alias) && ChildOf(name) is not null)
            )
            {
                _ = scope.Name(name, quoted: true, NameKeyClasses[member.Name]);
            }
        }
    }
}
