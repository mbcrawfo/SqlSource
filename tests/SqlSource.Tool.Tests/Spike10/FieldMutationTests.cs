using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Spike10;

// Finding 1 of the phase 10.0 spike: which members of a capture's calls the pipeline reads.  Each member of each call
// is changed in each way that fits it, and the replay of the changed capture is compared with the replay of the
// capture.  Run with SPIKE10_OUT=<folder> dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class
// "SqlSource.Tool.Tests.Spike10.FieldMutationTests" --explicit only.  SPIKE10_ONLY=<text> keeps the cases whose name
// has it.
public sealed class FieldMutationTests
{
    private static readonly string[] Sides = ["request", "answer"];

    private static readonly string[] Scopes = ["*", "1"];

    private static readonly Dictionary<string, (string Request, string Answer)> PostgresTypes = new(
        StringComparer.Ordinal
    )
    {
        ["open"] = ("PostgresOpenRequest", "PostgresOpenAnswer"),
        ["prepare"] = ("PostgresPrepareRequest", "PostgresPrepareAnswer"),
        ["deallocate"] = ("PostgresDeallocateRequest", "PostgresDeallocateAnswer"),
        ["types"] = ("PostgresTypesRequest", "PostgresTypesAnswer"),
        ["spellings"] = ("PostgresSpellingsRequest", "PostgresSpellingsAnswer"),
        ["columns"] = ("PostgresColumnsRequest", "PostgresColumnsAnswer"),
        ["tables"] = ("PostgresTablesRequest", "PostgresTablesAnswer"),
        ["check"] = ("PostgresCheckRequest", "PostgresCheckAnswer"),
        ["relations"] = ("PostgresRelationsRequest", "PostgresTablesAnswer"),
        ["overloads"] = ("PostgresOverloadsRequest", "PostgresOverloadsAnswer"),
        ["partitions"] = ("PostgresPartitionsRequest", "PostgresPartitionsAnswer"),
        ["explain"] = ("PostgresExplainRequest", "PostgresExplainAnswer"),
    };

    private static readonly Dictionary<string, (string Request, string Answer)> SqlServerTypes = new(
        StringComparer.Ordinal
    )
    {
        ["open"] = ("SqlServerOpenRequest", "SqlServerOpenAnswer"),
        ["parameters"] = ("SqlServerParametersRequest", "SqlServerParametersAnswer"),
        ["probe"] = ("SqlServerProbeRequest", "SqlServerProbeAnswer"),
        ["declared"] = ("SqlServerDeclaredRequest", "SqlServerDeclaredAnswer"),
        ["columns"] = ("SqlServerColumnsRequest", "SqlServerColumnsAnswer"),
        ["operands"] = ("SqlServerColumnsRequest", "SqlServerColumnsAnswer"),
        ["table"] = ("SqlServerTableRequest", "SqlServerColumnsAnswer"),
    };

    private static Dictionary<string, (string Request, string Answer)> TypesOf(string engine) =>
        engine == "postgres" ? PostgresTypes : SqlServerTypes;

    [Fact(Explicit = true)]
    public async Task Compilation_OfTheToolsSources_ListsItsErrors() =>
        await File.WriteAllLinesAsync(
            SpikeCases.Out("t2-compile-errors.txt"),
            CodeReaders.Instance.Errors(),
            TestContext.Current.CancellationToken
        );

    [Fact(Explicit = true)]
    public async Task Readers_EveryRecordMember_ListsWhoReadsIt()
    {
        foreach (var engine in new[] { "postgres", "mssql" })
        {
            var readers = CodeReaders.Instance.MembersOf(engine, TypesOf(engine));
            var table = new StringBuilder("method\tside\tpath\treader\n");
            foreach (var (key, list) in readers.OrderBy(static r => r.Key, StringComparer.Ordinal))
            {
                _ = table.Append(CultureInfo.InvariantCulture, $"{key}\t{ReaderText(list)}\n");
            }

            await File.WriteAllTextAsync(
                SpikeCases.Out($"t2-readers-{engine}.tsv"),
                table.ToString(),
                TestContext.Current.CancellationToken
            );
        }
    }

    [Fact(Explicit = true)]
    public async Task Mutate_EveryMemberOfEveryCall_RecordsWhatChanges()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = Stopwatch.StartNew();
        var only = Environment.GetEnvironmentVariable("SPIKE10_ONLY");
        var cases = SpikeCases
            .All()
            .Where(c => string.IsNullOrEmpty(only) || c.ToString().Contains(only, StringComparison.Ordinal))
            .ToList();
        var texts = cases.ToDictionary(static c => c, static c => c.Capture);
        var baselines = new ConcurrentDictionary<FixtureCase, SpikeOutcome>();
        await Parallel.ForEachAsync(
            cases,
            token,
            async (c, _) => baselines[c] = await SpikeReplay.RunAsync(c, texts[c])
        );

        // The groups of work: one for each call and side of each case.
        var groups = new List<Group>();
        var occurs = new Dictionary<string, Occurrence>(StringComparer.Ordinal);
        foreach (var c in cases)
        {
            var root = (JsonObject)JsonNode.Parse(texts[c])!;
            var calls = (JsonArray)root["calls"]!;
            for (var i = 0; i < calls.Count; i++)
            {
                var call = (JsonObject)calls[i]!;
                var method = call["method"]!.GetValue<string>();
                foreach (var side in Sides)
                {
                    var found = new Dictionary<string, List<JsonLocation>>(StringComparer.Ordinal);
                    JsonMutations.Collect(call[side], side, found);
                    groups.Add(new Group(c, i, method, side, [.. found.Keys]));
                    foreach (var (path, locations) in found)
                    {
                        var key = $"{c.Engine}\t{method}\t{side}\t{path}";
                        if (!occurs.TryGetValue(key, out var occurrence))
                        {
                            occurrence = new Occurrence();
                            occurs[key] = occurrence;
                        }

                        _ = occurrence.Cases.Add(c.ToString());
                        if (locations.Any(static l => l.Kind != System.Text.Json.JsonValueKind.Null))
                        {
                            _ = occurrence.NonNullCases.Add(c.ToString());
                        }
                    }
                }
            }
        }

        var results = new ConcurrentBag<Result>();
        await Parallel.ForEachAsync(
            groups,
            token,
            async (group, _) =>
            {
                foreach (var path in group.Paths)
                {
                    var fitting = Locations((JsonObject)JsonNode.Parse(texts[group.Case])!, group, path);
                    foreach (var mutation in JsonMutations.Fitting(fitting))
                    {
                        foreach (var scope in Scopes)
                        {
                            var root = (JsonObject)JsonNode.Parse(texts[group.Case])!;
                            var locations = Locations(root, group, path);
                            var targets = locations
                                .Where(l => JsonMutations.Fitting([l]).Contains(mutation, StringComparer.Ordinal))
                                .ToList();
                            if (scope == "1")
                            {
                                if (targets.Count < 2)
                                {
                                    continue;
                                }

                                targets = [targets[0]];
                            }

                            var applied = targets.Count(l => JsonMutations.Apply(l, mutation));
                            if (applied == 0)
                            {
                                continue;
                            }

                            var outcome = await SpikeReplay.RunAsync(group.Case, root.ToJsonString());
                            results.Add(
                                new Result(
                                    $"{group.Case.Engine}\t{group.Method}\t{group.Side}\t{path}",
                                    $"{mutation}@{scope}",
                                    group.Case.ToString(),
                                    JsonMutations.Label(baselines[group.Case], outcome)
                                )
                            );
                        }
                    }
                }
            }
        );

        await File.WriteAllTextAsync(
            SpikeCases.Out("t2-run.txt"),
            $"cases {cases.Count} groups {groups.Count} replays {results.Count}"
                + $" seconds {clock.Elapsed.TotalSeconds:F0}\n",
            token
        );
        foreach (var engine in new[] { "postgres", "mssql" })
        {
            await WriteMembersAsync(engine, occurs, results, token);
        }
    }

    private static List<JsonLocation> Locations(JsonObject root, Group group, string path)
    {
        var call = (JsonObject)((JsonArray)root["calls"]!)[group.Call]!;
        var found = new Dictionary<string, List<JsonLocation>>(StringComparer.Ordinal);
        JsonMutations.Collect(call[group.Side], group.Side, found);
        return found[path];
    }

    private static async Task WriteMembersAsync(
        string engine,
        Dictionary<string, Occurrence> occurs,
        ConcurrentBag<Result> results,
        System.Threading.CancellationToken token
    )
    {
        var readers = CodeReaders.Instance.MembersOf(engine, TypesOf(engine));
        var rows = new SortedDictionary<string, Row>(StringComparer.Ordinal);
        foreach (var (key, occurrence) in occurs.Where(o => o.Key.StartsWith(engine + "\t", StringComparison.Ordinal)))
        {
            rows[key[(engine.Length + 1)..]] = new Row { Occurrence = occurrence };
        }

        foreach (var key in readers.Keys.Where(k => !rows.ContainsKey(k)))
        {
            rows[key] = new Row();
        }

        foreach (var result in results.Where(r => r.Key.StartsWith(engine + "\t", StringComparison.Ordinal)))
        {
            rows[result.Key[(engine.Length + 1)..]].Results.Add(result);
        }

        var table = new StringBuilder("method\tside\tpath\tcases\tchangedBy\toutcomes\treader\tverdict\n");
        foreach (var (key, row) in rows)
        {
            var reader = readers.TryGetValue(key, out var list) ? list : null;
            var codeReaders =
                reader
                    ?.Where(static r =>
                        !r.StartsWith("live ", StringComparison.Ordinal)
                        && !r.StartsWith("log ", StringComparison.Ordinal)
                    )
                    .ToList()
                ?? [];
            var changed = row
                .Results.Where(static r => r.Label != "same")
                .Select(static r => r.Mutation)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var changedBy = string.Join(
                ',',
                changed
                    .OrderBy(static m => m, StringComparer.Ordinal)
                    .Select(m =>
                    {
                        var tried = row
                            .Results.Where(r => r.Mutation == m)
                            .Select(static r => r.Case)
                            .Distinct(StringComparer.Ordinal)
                            .Count();
                        var moved = row
                            .Results.Where(r => r.Mutation == m && r.Label != "same")
                            .Select(static r => r.Case)
                            .Distinct(StringComparer.Ordinal)
                            .Count();
                        return $"{m}({moved.ToString(CultureInfo.InvariantCulture)}"
                            + $"/{tried.ToString(CultureInfo.InvariantCulture)})";
                    })
            );
            var outcomes = string.Join(
                ',',
                row.Results.GroupBy(static r => r.Label)
                    .OrderByDescending(static g => g.Count())
                    .ThenBy(static g => g.Key, StringComparer.Ordinal)
                    .Select(static g => $"{g.Key}:{g.Count().ToString(CultureInfo.InvariantCulture)}")
            );
            var cases = row.Occurrence?.Cases.Count ?? 0;
            var nullOnly = row.Occurrence is null ? 0 : row.Occurrence.Cases.Count - row.Occurrence.NonNullCases.Count;
            if (row.Occurrence is null)
            {
                outcomes = "in no capture";
            }
            else if (nullOnly > 0)
            {
                outcomes +=
                    (outcomes.Length > 0 ? "," : string.Empty)
                    + $"alwaysNull:{nullOnly.ToString(CultureInfo.InvariantCulture)}";
            }

            var isRequest = key.Split('\t')[1] == "request";
            string verdict;
            if (changed.Count > 0)
            {
                verdict = isRequest && (row.Occurrence?.NonNullCases.Count ?? 0) > 0 ? "key" : "read";
            }
            else if (isRequest && (row.Occurrence?.NonNullCases.Count ?? 0) > 0)
            {
                verdict = "key";
            }
            else
            {
                verdict = codeReaders.Count > 0 ? "read, never moved" : "not read";
            }

            _ = table.Append(
                CultureInfo.InvariantCulture,
                $"{key}\t{cases}\t{(changedBy.Length > 0 ? changedBy : "-")}\t{(outcomes.Length > 0 ? outcomes : "-")}"
                    + $"\t{(reader is null ? "unmapped" : ReaderText(reader))}\t{verdict}\n"
            );
        }

        await File.WriteAllTextAsync(SpikeCases.Out($"t2-members-{engine}.tsv"), table.ToString(), token);
    }

    private static string ReaderText(IReadOnlyList<string> readers)
    {
        if (readers.Count == 0)
        {
            return "none";
        }

        var shown = readers.Take(8).ToList();
        return string.Join("; ", shown)
            + (
                readers.Count > shown.Count
                    ? $"; +{(readers.Count - shown.Count).ToString(CultureInfo.InvariantCulture)} more"
                    : string.Empty
            );
    }

    private sealed record Group(FixtureCase Case, int Call, string Method, string Side, IReadOnlyList<string> Paths);

    private sealed record Result(string Key, string Mutation, string Case, string Label);

    private sealed class Occurrence
    {
        public HashSet<string> Cases { get; } = [with(StringComparer.Ordinal)];

        public HashSet<string> NonNullCases { get; } = [with(StringComparer.Ordinal)];
    }

    private sealed class Row
    {
        public Occurrence? Occurrence { get; init; }

        public List<Result> Results { get; } = [];
    }
}
