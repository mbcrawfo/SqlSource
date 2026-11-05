using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Spike10;

// Finding 1 of the phase 10.0 spike, for the keys of PostgreSQL's plan: the `plan` string of each `explain` answer is
// parsed, one key name at a time is removed or changed wherever it occurs, the plan is written back and the capture
// is replayed.  Run with SPIKE10_OUT=<folder> dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class
// "SqlSource.Tool.Tests.Spike10.PlanKeyMutationTests" --explicit only.
public sealed class PlanKeyMutationTests
{
    private const string Control = "(written back unchanged)";

    // The members of PlanNode that each key it reads becomes.
    private static readonly Dictionary<string, string[]> NodeMembers = new(StringComparer.Ordinal)
    {
        ["Plan"] = [],
        ["Plans"] = ["Plans"],
        ["Node Type"] = ["NodeType"],
        ["Join Type"] = ["JoinType"],
        ["Parent Relationship"] = ["ParentRelationship"],
        ["Output"] = ["Output"],
        ["Alias"] = ["Alias"],
        ["Relation Name"] = ["RelationName"],
        ["Schema"] = ["Schema"],
        ["Subplan Name"] = ["SubplanName"],
        ["CTE Name"] = ["CteName"],
        ["Command"] = ["Command"],
        ["Strategy"] = ["Strategy"],
        ["Grouping Sets"] = ["HasGroupingSets"],
        ["Group Key"] = ["HasGroupKey"],
        ["Filter"] = ["HasFilter"],
        ["Subplans Removed"] = ["SubplansRemoved"],
    };

    private static readonly string[] Wanted =
    [
        JsonMutations.Removed,
        JsonMutations.StringX,
        JsonMutations.NumberPlusOne,
        JsonMutations.BoolNegated,
        JsonMutations.ArrayDropLast,
    ];

    [Fact(Explicit = true)]
    public async Task Mutate_EveryKeyOfThePlan_RecordsWhatChanges()
    {
        var token = TestContext.Current.CancellationToken;
        var cases = SpikeCases.All().Where(static c => c.Engine == "postgres").ToList();
        var texts = cases.ToDictionary(static c => c, static c => c.Capture);
        var baselines = new ConcurrentDictionary<FixtureCase, SpikeOutcome>();
        await Parallel.ForEachAsync(
            cases,
            token,
            async (c, _) => baselines[c] = await SpikeReplay.RunAsync(c, texts[c])
        );

        // The keys of each case's plans.
        var keysOf = new Dictionary<FixtureCase, List<string>>();
        var cases2 = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var c in cases)
        {
            var plans = PlansOf((JsonObject)JsonNode.Parse(texts[c])!).Select(static p => p.Plan).ToList();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var plan in plans)
            {
                var found = new Dictionary<string, List<JsonLocation>>(StringComparer.Ordinal);
                Gather(plan, found);
                keys.UnionWith(found.Keys);
            }

            keysOf[c] = [.. keys];
            foreach (var key in keys)
            {
                if (!cases2.TryGetValue(key, out var set))
                {
                    set = [with(StringComparer.Ordinal)];
                    cases2[key] = set;
                }

                _ = set.Add(c.ToString());
            }
        }

        var results = new ConcurrentBag<(string Key, string Mutation, string Case, string Label)>();
        var items = cases
            .Where(c => keysOf[c].Count > 0)
            .SelectMany(c => keysOf[c].Select(k => (Case: c, Key: k)))
            .ToList();
        await Parallel.ForEachAsync(
            items,
            token,
            async (item, _) =>
            {
                var probe = (JsonObject)JsonNode.Parse(texts[item.Case])!;
                var mutations = new List<string>();
                foreach (var (_, plan) in PlansOf(probe))
                {
                    var found = new Dictionary<string, List<JsonLocation>>(StringComparer.Ordinal);
                    Gather(plan, found);
                    if (found.TryGetValue(item.Key, out var here))
                    {
                        mutations.AddRange(
                            JsonMutations.Fitting(here).Where(m => Wanted.Contains(m, StringComparer.Ordinal))
                        );
                    }
                }

                foreach (var mutation in mutations.Distinct(StringComparer.Ordinal))
                {
                    var text = Rewritten(texts[item.Case], item.Key, mutation);
                    var outcome = await SpikeReplay.RunAsync(item.Case, text);
                    results.Add(
                        (item.Key, mutation, item.Case.ToString(), JsonMutations.Label(baselines[item.Case], outcome))
                    );
                }
            }
        );

        // The control: every plan written back as it was.
        await Parallel.ForEachAsync(
            cases.Where(c => keysOf[c].Count > 0),
            token,
            async (c, _) =>
            {
                var outcome = await SpikeReplay.RunAsync(c, Rewritten(texts[c], null, null));
                results.Add((Control, "roundtrip", c.ToString(), JsonMutations.Label(baselines[c], outcome)));
            }
        );

        var readers = CodeReaders.Instance;
        var downstream = readers.ReadersOfMembers("PlanNode", "-");
        var table = new StringBuilder("key\tcases\tchangedBy\toutcomes\treader\tverdict\n");
        foreach (var key in cases2.Keys.Append(Control).OrderBy(static k => k, StringComparer.Ordinal))
        {
            var mine = results.Where(r => r.Key == key).ToList();
            var changed = mine.Where(static r => r.Label != "same")
                .Select(static r => r.Mutation)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static m => m, StringComparer.Ordinal)
                .ToList();
            var changedBy = string.Join(
                ',',
                changed.Select(m =>
                {
                    var tried = mine.Where(r => r.Mutation == m)
                        .Select(static r => r.Case)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    var moved = mine.Where(r => r.Mutation == m && r.Label != "same")
                        .Select(static r => r.Case)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    return $"{m}({moved.ToString(CultureInfo.InvariantCulture)}"
                        + $"/{tried.ToString(CultureInfo.InvariantCulture)})";
                })
            );
            var outcomes = string.Join(
                ',',
                mine.GroupBy(static r => r.Label)
                    .OrderByDescending(static g => g.Count())
                    .ThenBy(static g => g.Key, StringComparer.Ordinal)
                    .Select(static g => $"{g.Key}:{g.Count().ToString(CultureInfo.InvariantCulture)}")
            );
            var literal = key == Control ? [] : readers.LiteralReaders(key, "Postgres/");
            var via =
                key != Control && NodeMembers.TryGetValue(key, out var members)
                    ? members.SelectMany(m => downstream[m].Select(r => $"{m}<-{r}")).ToList()
                    : [];
            var reader = literal.Count == 0 ? "none" : string.Join("; ", literal);
            if (via.Count > 0)
            {
                reader += " | " + string.Join("; ", via);
            }

            var count = key == Control ? cases.Count(c => keysOf[c].Count > 0) : cases2[key].Count;
            var verdict = literal.Count > 0 ? "read, never moved" : "not read";
            if (changed.Count > 0)
            {
                verdict = "read";
            }

            _ = table.Append(
                CultureInfo.InvariantCulture,
                $"{key}\t{count}\t{(changedBy.Length > 0 ? changedBy : "-")}"
                    + $"\t{(outcomes.Length > 0 ? outcomes : "-")}\t{reader}\t{verdict}\n"
            );
        }

        await File.WriteAllTextAsync(SpikeCases.Out("t2-plan-keys.tsv"), table.ToString(), token);
    }

    // The capture with the key changed in every plan of its `explain` calls; no key changes the plan only.
    private static string Rewritten(string text, string? key, string? mutation)
    {
        var root = (JsonObject)JsonNode.Parse(text)!;
        foreach (var (answer, plan) in PlansOf(root))
        {
            if (key is not null)
            {
                var found = new Dictionary<string, List<JsonLocation>>(StringComparer.Ordinal);
                Gather(plan, found);
                if (found.TryGetValue(key, out var locations))
                {
                    foreach (var location in locations)
                    {
                        _ = JsonMutations.Apply(location, mutation!);
                    }
                }
            }

            answer["plan"] = plan.ToJsonString();
        }

        return root.ToJsonString();
    }

    // The answer and the parsed plan of each `explain` call that has one.
    private static List<(JsonObject Answer, JsonNode Plan)> PlansOf(JsonObject root)
    {
        var plans = new List<(JsonObject, JsonNode)>();
        foreach (var call in ((JsonArray)root["calls"]!).OfType<JsonObject>())
        {
            if (
                call["method"]?.GetValue<string>() == "explain"
                && call["answer"] is JsonObject answer
                && answer["plan"] is JsonValue text
                && text.TryGetValue<string>(out var json)
                && JsonNode.Parse(json) is { } plan
            )
            {
                plans.Add((answer, plan));
            }
        }

        return plans;
    }

    // Every location of every key name anywhere in the tree.
    private static void Gather(JsonNode? node, Dictionary<string, List<JsonLocation>> into)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToArray())
                {
                    if (!into.TryGetValue(name, out var list))
                    {
                        list = [];
                        into[name] = list;
                    }

                    list.Add(new JsonLocation(obj, name, -1));
                    Gather(value, into);
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    Gather(element, into);
                }

                break;
            default:
                break;
        }
    }
}
