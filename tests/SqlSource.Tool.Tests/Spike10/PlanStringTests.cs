using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Spike10;

// Finding 2 of the phase 10.0 spike: every string of every PostgreSQL plan, by key, read through PlanTokens.  Run with
// SPIKE10_OUT=<folder> dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class
// "SqlSource.Tool.Tests.Spike10.PlanStringTests" --explicit only.
public sealed partial class PlanStringTests
{
    // Whether the strings of a key the walk reads are names or expressions; a key not listed is not read.
    private static readonly Dictionary<string, string> Shape = new(StringComparer.Ordinal)
    {
        ["Output"] = "expressions",
        ["Group Key"] = "expressions",
        ["Filter"] = "expression (presence only)",
        ["Alias"] = "name",
        ["Relation Name"] = "name",
        ["Schema"] = "name",
        ["CTE Name"] = "name",
        ["Subplan Name"] = "name (SubPlan n / InitPlan n)",
        ["Node Type"] = "fixed vocabulary",
        ["Join Type"] = "fixed vocabulary",
        ["Parent Relationship"] = "fixed vocabulary",
        ["Command"] = "fixed vocabulary",
        ["Strategy"] = "fixed vocabulary",
    };

    private sealed class Tally
    {
        public int Readable;
        public int Unreadable;
        public readonly Dictionary<PlanTokens.Kind, int> Kinds = [];
        public readonly List<string> ReadableExamples = [];
        public readonly List<string> UnreadableExamples = [];
        public readonly HashSet<string> Cases = [with(StringComparer.Ordinal)];
    }

    [Fact(Explicit = true)]
    public void Read_EveryStringOfEveryPlan_RecordsWhatReads()
    {
        var walk = WalkVerdicts();
        var tally = new SortedDictionary<string, Tally>(StringComparer.Ordinal);
        var unreadable = new StringBuilder("key\tcase\tstring\n");
        var stringTokens = new StringBuilder("key\tcase\tstring\n");
        var unknownWords = new Dictionary<string, (int Count, HashSet<string> Cases, string Example)>(
            StringComparer.Ordinal
        );
        var plans = 0;
        foreach (var fixture in SpikeCases.All().Where(static c => c.Engine == "postgres"))
        {
            var root = (JsonObject)JsonNode.Parse(fixture.Capture)!;
            var calls = ((JsonArray)root["calls"]!).OfType<JsonObject>().ToList();
            var known = KnownNames(calls);
            foreach (var call in calls.Where(static c => c["method"]?.GetValue<string>() == "explain"))
            {
                if (
                    call["answer"] is not JsonObject answer
                    || answer["plan"] is not JsonValue text
                    || !text.TryGetValue<string>(out var json)
                    || JsonNode.Parse(json) is not { } plan
                )
                {
                    continue;
                }

                plans++;
                foreach (var (key, value) in Strings(plan, null))
                {
                    if (!tally.TryGetValue(key, out var t))
                    {
                        t = new Tally();
                        tally[key] = t;
                    }

                    _ = t.Cases.Add(fixture.ToString());
                    var tokens = PlanTokens.Read(value);
                    if (tokens is null)
                    {
                        t.Unreadable++;
                        AddExample(t.UnreadableExamples, value);
                        _ = unreadable.Append(CultureInfo.InvariantCulture, $"{key}\t{fixture}\t{Escape(value)}\n");
                        continue;
                    }

                    t.Readable++;
                    AddExample(t.ReadableExamples, value);
                    foreach (var token in tokens)
                    {
                        t.Kinds[token.Kind] = t.Kinds.GetValueOrDefault(token.Kind) + 1;
                    }

                    if (tokens.Any(static token => token.Kind == PlanTokens.Kind.String))
                    {
                        _ = stringTokens.Append(CultureInfo.InvariantCulture, $"{key}\t{fixture}\t{Escape(value)}\n");
                    }

                    if (key == "Output")
                    {
                        foreach (var token in tokens.Where(static token => token.Kind == PlanTokens.Kind.Word))
                        {
                            var word = value[token.Start..token.End];
                            if (known.Contains(word.ToUpperInvariant()))
                            {
                                continue;
                            }

                            var (count, cases, example) = unknownWords.GetValueOrDefault(word.ToUpperInvariant());
                            cases ??= [with(StringComparer.Ordinal)];
                            _ = cases.Add(fixture.ToString());
                            unknownWords[word.ToUpperInvariant()] = (count + 1, cases, example ?? value);
                        }
                    }
                }
            }
        }

        var table = new StringBuilder("key\tstrings\treadable\tunreadable\tkinds\texamples\twalkReads\tshape\n");
        foreach (var (key, t) in tally)
        {
            var kinds = string.Join(',', t.Kinds.OrderBy(static k => k.Key).Select(static k => $"{k.Key}:{k.Value}"));
            var examples =
                "ok: "
                + string.Join(" | ", t.ReadableExamples.Select(Escape))
                + (
                    t.UnreadableExamples.Count > 0
                        ? " ;; null: " + string.Join(" | ", t.UnreadableExamples.Select(Escape))
                        : ""
                );
            _ = table.Append(
                CultureInfo.InvariantCulture,
                $"{key}\t{t.Readable + t.Unreadable}\t{t.Readable}\t{t.Unreadable}\t{(kinds.Length > 0 ? kinds : "-")}"
                    + $"\t{examples}\t{walk.GetValueOrDefault(key, "not in task 2 table")}"
                    + $"\t{Shape.GetValueOrDefault(key, "-")}\n"
            );
        }

        File.WriteAllText(SpikeCases.Out("t3-plan-strings.tsv"), table.ToString());
        File.WriteAllText(SpikeCases.Out("t3-plan-unreadable.tsv"), unreadable.ToString());
        File.WriteAllText(SpikeCases.Out("t3-plan-string-tokens.tsv"), stringTokens.ToString());
        var words = new StringBuilder("word\tuses\tcases\texampleOutput\n");
        foreach (
            var (word, (count, cases, example)) in unknownWords
                .OrderByDescending(static p => p.Value.Count)
                .ThenBy(static p => p.Key, StringComparer.Ordinal)
        )
        {
            _ = words.Append(CultureInfo.InvariantCulture, $"{word}\t{count}\t{cases.Count}\t{Escape(example)}\n");
        }

        File.WriteAllText(SpikeCases.Out("t3-plan-output-words.tsv"), words.ToString());
        File.WriteAllText(SpikeCases.Out("t3-plan-count.txt"), $"plans {plans}\n");
    }

    // Finding 5: every error of every case's capture.  The corpora hold only queries that were described, so the
    // table is expected to hold no row below its header; the counts say how many answers were looked at.
    [Fact(Explicit = true)]
    public void Read_EveryErrorOfEveryCapture_RecordsCallCodeAndParts()
    {
        var table = new StringBuilder("engine\tcase\tcall\tproperty\tcode\tposition\tdetail\thint\n");
        var examined = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var fixture in SpikeCases.All())
        {
            var root = (JsonObject)JsonNode.Parse(fixture.Capture)!;
            foreach (var call in ((JsonArray)root["calls"]!).OfType<JsonObject>())
            {
                var method = call["method"]!.GetValue<string>();
                examined[fixture.Engine + ":" + method] = examined.GetValueOrDefault(fixture.Engine + ":" + method) + 1;
                foreach (var (name, value) in call["answer"] is JsonObject answer ? answer : [])
                {
                    if (!name.Contains("error", StringComparison.OrdinalIgnoreCase) || value is null)
                    {
                        continue;
                    }

                    if (value is JsonArray { Count: 0 })
                    {
                        continue;
                    }

                    _ = table.Append(
                        CultureInfo.InvariantCulture,
                        $"{fixture.Engine}\t{fixture}\t{method}\t{name}\t{Escape(value.ToJsonString())}\t\t\t\n"
                    );
                }
            }
        }

        File.WriteAllText(SpikeCases.Out("t3-captured-errors.tsv"), table.ToString());
        File.WriteAllText(
            SpikeCases.Out("t3-captured-errors-examined.txt"),
            string.Join('\n', examined.Select(static p => $"{p.Key}\t{p.Value}")) + "\n"
        );
    }

    private static void AddExample(List<string> into, string value)
    {
        if (into.Count < 5 && !into.Contains(value, StringComparer.Ordinal))
        {
            into.Add(value);
        }
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);

    // Task 2's verdict of each key.
    private static Dictionary<string, string> WalkVerdicts()
    {
        var verdicts = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = SpikeCases.Out("t2-plan-keys.tsv");
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var columns = line.Split('\t');
            verdicts[columns[0]] = columns[5];
        }

        return verdicts;
    }

    // Every string value and string array element of the tree, with the key it belongs to.
    private static IEnumerable<(string Key, string Value)> Strings(JsonNode? node, string? key)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    foreach (var found in Strings(value, name))
                    {
                        yield return found;
                    }
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    foreach (var found in Strings(element, key))
                    {
                        yield return found;
                    }
                }

                break;
            case JsonValue value when key is not null && value.TryGetValue<string>(out var text):
                yield return (key, text);
                break;
            default:
                break;
        }
    }

    // The upper-cased words of every string of the case's catalog answers and requests (not the plan's), plus the
    // names the plan itself gives to relations.
    private static HashSet<string> KnownNames(List<JsonObject> calls)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in calls)
        {
            if (call["method"]?.GetValue<string>() == "explain")
            {
                if (
                    call["answer"]?["plan"] is JsonValue text
                    && text.TryGetValue<string>(out var json)
                    && JsonNode.Parse(json) is { } plan
                )
                {
                    foreach (
                        var (_, value) in Strings(plan, null)
                            .Where(static p => p.Key is "Alias" or "Relation Name" or "Schema" or "CTE Name")
                    )
                    {
                        AddWords(known, value);
                    }
                }

                continue;
            }

            foreach (var node in new[] { call["answer"], call["request"] })
            {
                foreach (var (_, value) in Strings(node, "x"))
                {
                    AddWords(known, value);
                }
            }
        }

        return known;
    }

    private static void AddWords(HashSet<string> into, string value)
    {
        foreach (Match match in WordPattern().Matches(value))
        {
            _ = into.Add(match.Value.ToUpperInvariant());
        }
    }

    [GeneratedRegex(@"[\p{L}_][\p{L}\p{N}_$]*")]
    private static partial Regex WordPattern();
}
