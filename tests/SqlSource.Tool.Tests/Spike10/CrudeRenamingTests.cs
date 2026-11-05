using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

#pragma warning disable CA1308 // PostgreSQL folds a name to lower case, and the lists are lower case.

namespace SqlSource.Tool.Tests.Spike10;

// Finding 4 of the phase 10.0 spike: every case renamed by the crude renamer and replayed, under the design's steps as
// written and under each rule the spike added.  Run with SPIKE10_OUT=<folder> dotnet test --project
// tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Spike10.CrudeRenamingTests" --explicit only
public sealed class CrudeRenamingTests
{
    // The runs: the design as written, every rule, and every rule but one.
    private static readonly (string Name, CrudeRules Rules)[] Runs =
    [
        ("design", CrudeRules.None),
        ("all", CrudeRules.All),
        ("all-but-copies", CrudeRules.All & ~CrudeRules.CopiesThroughTheirParameter),
        ("all-but-digits", CrudeRules.All & ~CrudeRules.DigitNamesKept),
        ("all-but-order", CrudeRules.All & ~CrudeRules.RelationsInNameOrder),
        ("all-but-cte", CrudeRules.All & ~CrudeRules.CteSubplanName),
        ("all-but-child-aliases", CrudeRules.All & ~CrudeRules.ChildAliasesThroughTheirAlias),
        ("all-but-marks", CrudeRules.All & ~CrudeRules.NullabilityMarksKept),
    ];

    // The members of an entry whose values are the sidecar's own: not the user's, and not searched for a leak.
    private static readonly HashSet<string> ClosedMembers =
    [
        with(StringComparer.Ordinal),
        "_WARNING",
        "$schema",
        "toolVersion",
        "hash",
        "engine",
        "serverVersion",
        "resultKind",
        "plan",
        "tableMatch",
        "nullableSource",
        "kind",
        "typeSource",
        "assemblyQualifiedName",
    ];

    // For the run with every rule: each original name kept verbatim, and each name the catalog did not give, by engine.
    private static readonly SortedDictionary<string, SortedSet<string>> Kept = [with(StringComparer.Ordinal)];

    private static readonly SortedDictionary<string, SortedSet<string>> NotFromCatalog = [with(StringComparer.Ordinal)];

    private sealed record Result(
        int SelfTestLeaks,
        string Run,
        FixtureCase Case,
        string Outcome,
        string Provenance,
        List<RequestDiff> Diffs,
        List<string> Notes,
        List<string> Leaks,
        string Operands
    );

    internal sealed record RequestDiff(string Method, string Asked, string Recorded);

    [Fact(Explicit = true)]
    public async Task Rename_EveryCase_ReplaysWithTheSameProvenance()
    {
        var cases = SpikeCases.All();
        var results = new List<Result>();
        foreach (var (name, rules) in Runs)
        {
            foreach (var fixture in cases)
            {
                results.Add(await EvaluateAsync(name, rules, fixture));
            }
        }

        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(SpikeCases.Out("t5-renaming.tsv"), Table(results, "all"), token);
        await File.WriteAllTextAsync(SpikeCases.Out("t5-renaming-design.tsv"), Table(results, "design"), token);
        await File.WriteAllTextAsync(SpikeCases.Out("t5-request-diffs.tsv"), DiffTable(results), token);
        await File.WriteAllTextAsync(SpikeCases.Out("t5-leaks.tsv"), LeakTable(results), token);
        await File.WriteAllTextAsync(SpikeCases.Out("t5-counts.tsv"), Counts(results), token);
        await File.WriteAllTextAsync(SpikeCases.Out("t5-notes.tsv"), NoteTable(results), token);
        await File.WriteAllTextAsync(SpikeCases.Out("t5-rule-effects.tsv"), RuleEffects(results), token);
        await File.WriteAllTextAsync(
            SpikeCases.Out("t5-kept.tsv"),
            Grouped("engine\tword\tcases\texamples\n", Kept),
            token
        );
        await File.WriteAllTextAsync(
            SpikeCases.Out("t5-names-not-from-catalog.tsv"),
            Grouped("engine\tclass\toriginal\tfirstFoundIn\tcases\texamples\n", NotFromCatalog),
            token
        );
        results.Count.ShouldBeGreaterThan(0);
    }

    private static async Task<Result> EvaluateAsync(string run, CrudeRules rules, FixtureCase fixture)
    {
        var folder = $"t5-{run}-{(SpikeCases.IsRealistic(fixture) ? "r-" : "")}{fixture.Name}";
        CrudeRenaming renaming;
        try
        {
            renaming = CrudeRenamer.Rename(fixture, rules, folder);
        }
#pragma warning disable CA1031 // The spike reports any exception as an outcome.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return new Result(0, run, fixture, "renamer-threw " + exception.Message, "n/a", [], [], [], "");
        }

        var outcome = await SpikeReplay.RunAsync(renaming.Renamed);
        var (diffs, asked, given) = await DiffReplayAsync(renaming.Renamed);
        string text;
        var provenance = "n/a";
        switch (outcome.Kind)
        {
            case SpikeKind.Entry:
                var difference = FirstDifference(
                    WithoutHash(JsonNode.Parse(outcome.Text)),
                    WithoutHash(JsonNode.Parse(renaming.Renamed.Expected)),
                    ""
                );
                text = difference is null ? "entry" : "entry-differs " + difference;
                provenance = ProvenanceOf(fixture.Expected, outcome.Text);
                break;
            case SpikeKind.Miss:
                var method = outcome.Text[..outcome.Text.IndexOf(' ', StringComparison.Ordinal)];
                var first = diffs.FirstOrDefault(diff => diff.Method == method);
                text = first is null
                    ? "miss " + method
                    : $"miss {method}: asked «{Cut(first.Asked, first.Recorded)}»"
                        + $" recorded «{Cut(first.Recorded, first.Asked)}»";
                break;
            case SpikeKind.Failure:
            case SpikeKind.Unreadable:
            case SpikeKind.Thrown:
                text = $"{outcome.Kind.ToString().ToLowerInvariant()} {outcome.Text}";
                break;
            default:
                text = $"{outcome.Kind.ToString().ToLowerInvariant()} {outcome.Text}";
                break;
        }

        var operands = fixture.Engine == "mssql" ? OperandsOf(fixture, asked, given, diffs) : "";
        if (run == "all")
        {
            Collect(fixture, renaming);
        }

        return new Result(
            LeaksOf(fixture, fixture, static _ => false).Count,
            run,
            fixture,
            text,
            provenance,
            diffs,
            [.. renaming.Notes],
            LeaksOf(fixture, renaming.Renamed, renaming.IsKept),
            operands
        );
    }

    // The operands requests of the original capture, of the replay of the copy, and how many the copy matched exactly.
    private static string OperandsOf(
        FixtureCase fixture,
        List<string> asked,
        List<string> given,
        List<RequestDiff> diffs
    )
    {
        var original = CaptureJson.Read(fixture.Capture).Value!.Calls.Count(static call => call.Method == "operands");
        var made = asked.Count(static method => method == "operands");
        var differing = diffs.Count(static diff => diff.Method == "operands");
        var copies = given.Count(static method => method == "probe");
        return $"operands {original}/{made}/{made - differing}" + (copies > 0 ? $" probe {copies}" : "");
    }

    // ---- The replay that answers a request it lacks from the nearest one of its method, and records the difference.

    private static async Task<(List<RequestDiff> Diffs, List<string> Asked, List<string> Given)> DiffReplayAsync(
        FixtureCase fixture
    )
    {
        var capture = CaptureJson.Read(fixture.Capture).Value!;
        var exchange = new DiffingExchange(capture);
        try
        {
            var token = TestContext.Current.CancellationToken;
            var (planned, dialect) = FixtureReplay.Plan(fixture, token);
            var describer = fixture.Engine switch
            {
                "postgres" => (IQueryDescriber)new PostgresDescriber(fixture.ExplainSettings),
                "mssql" => new SqlServerDescriber(),
                _ => throw new InvalidOperationException("no describer"),
            };
            var opened = await describer.OpenAsync(planned.Database!, FixtureReplay.Connection, exchange);
            if (opened.Value is { } session)
            {
                await using (session)
                {
                    var query = planned.Query;
                    var described = await session.DescribeAsync(
                        new DescribeRequest(query.Name, SampleSql.Build(query), query.Parameters),
                        token
                    );
                    if (described.Value is { } description)
                    {
                        _ = EntryBuilder.Build(planned, dialect, planned.Database!, session.Server, description);
                    }
                }
            }
        }
#pragma warning disable CA1031 // The spike reports any exception as an outcome.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            exchange.Diffs.Add(new RequestDiff("(stopped)", exception.Message, ""));
        }

        return (exchange.Diffs, exchange.Asked, exchange.Given);
    }

    private sealed class DiffingExchange(DescribeCapture capture) : IDescribeExchange
    {
        private readonly List<(CaptureCall Call, string Key)> _calls =
        [
            .. capture.Calls.Select(static call => (call, CaptureJson.KeyOf(call.Request))),
        ];

        private readonly HashSet<int> _given = [];

        public List<RequestDiff> Diffs { get; } = [];

        public List<string> Asked { get; } = [];

        public List<string> Given { get; } = [];

        public Task<TAnswer> AskAsync<TRequest, TAnswer>(
            string method,
            TRequest request,
            Func<TRequest, CancellationToken, Task<TAnswer>> live,
            CancellationToken cancellationToken
        )
        {
            Asked.Add(method);
            var key = CaptureJson.KeyOf(CaptureJson.ToElement(request));
            var open = Enumerable
                .Range(0, _calls.Count)
                .Where(index => !_given.Contains(index) && _calls[index].Call.Method == method)
                .ToList();
            if (open.Count == 0)
            {
                throw CaptureMissException.NoAnswer(method, key);
            }

            var exact = open.FirstOrDefault(index => _calls[index].Key == key, -1);
            var chosen = exact >= 0 ? exact : open.MaxBy(index => CommonPrefix(_calls[index].Key, key));
            if (exact < 0)
            {
                Diffs.Add(new RequestDiff(method, key, _calls[chosen].Key));
            }

            _ = _given.Add(chosen);
            Given.Add(method);
            var answer =
                _calls[chosen].Call.Answer.Deserialize<TAnswer>(CaptureJson.Options)
                ?? throw CaptureMissException.Unreadable(method, typeof(TAnswer), "it is null");
            return AnswerNulls.Find(answer) is { } member
                ? throw CaptureMissException.Unreadable(method, typeof(TAnswer), $"'{member}' is null")
                : Task.FromResult(answer);
        }
    }

    private static int CommonPrefix(string one, string other)
    {
        var length = 0;
        while (length < one.Length && length < other.Length && one[length] == other[length])
        {
            length++;
        }

        return length;
    }

    // The first 80 characters around where the two texts first differ: 20 before, 60 from it.
    private static string Cut(string text, string other)
    {
        var at = CommonPrefix(text, other);
        var start = Math.Max(0, at - 20);
        return text.Substring(start, Math.Min(80, text.Length - start)).Replace("\n", "\\n", StringComparison.Ordinal);
    }

    // ---- The entry and its provenance.

    private static JsonNode? WithoutHash(JsonNode? sidecar)
    {
        if (sidecar?["queries"] is JsonObject queries)
        {
            foreach (var (_, entry) in queries)
            {
                _ = ((JsonObject)entry!).Remove("hash");
            }
        }

        return sidecar;
    }

    private static string? FirstDifference(JsonNode? actual, JsonNode? expected, string path)
    {
        switch (actual, expected)
        {
            case (JsonObject one, JsonObject other):
                foreach (var name in one.Select(static p => p.Key).Union(other.Select(static p => p.Key)))
                {
                    if (!one.ContainsKey(name) || !other.ContainsKey(name))
                    {
                        var where = one.ContainsKey(name) ? "the replay" : "the renamed expected";
                        return $"{path}.{name} (only in {where})";
                    }

                    if (FirstDifference(one[name], other[name], path + "." + name) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            case (JsonArray one, JsonArray other):
                for (var index = 0; index < Math.Max(one.Count, other.Count); index++)
                {
                    if (index >= one.Count || index >= other.Count)
                    {
                        return $"{path}[{index}] (lengths {one.Count} and {other.Count})";
                    }

                    if (FirstDifference(one[index], other[index], $"{path}[{index}]") is { } found)
                    {
                        return found;
                    }
                }

                return null;
            default:
                return JsonNode.DeepEquals(actual, expected)
                    ? null
                    : $"{path}: «{actual?.ToJsonString()}» against «{expected?.ToJsonString()}»";
        }
    }

    // Whether the replayed entry gives each column's nullableSource, the plan, the table match and each parameter's
    // typeSource as the original's did.
    private static string ProvenanceOf(string original, string replayed)
    {
        var one = Single(JsonNode.Parse(original)!);
        var other = Single(JsonNode.Parse(replayed)!);
        var differences = new List<string>();
        foreach (var member in new[] { "plan", "tableMatch" })
        {
            if (one[member]?.ToJsonString() != other[member]?.ToJsonString())
            {
                differences.Add($"{member} {one[member]?.ToJsonString()}->{other[member]?.ToJsonString()}");
            }
        }

        foreach (var (list, member) in new[] { ("columns", "nullableSource"), ("parameters", "typeSource") })
        {
            var a = one[list] as JsonArray ?? [];
            var b = other[list] as JsonArray ?? [];
            for (var index = 0; index < Math.Max(a.Count, b.Count); index++)
            {
                var x = index < a.Count ? a[index]?[member]?.ToJsonString() : "none";
                var y = index < b.Count ? b[index]?[member]?.ToJsonString() : "none";
                if (x != y)
                {
                    differences.Add($"{list}[{index}].{member} {x}->{y}");
                }
            }
        }

        return differences.Count == 0 ? "same" : string.Join("; ", differences);
    }

    private static JsonObject Single(JsonNode sidecar) => (JsonObject)((JsonObject)sidecar["queries"]!).Single().Value!;

    // ---- Leaks: a name or literal of schema.sql or query.sql that was not kept, found in a file of the copy.

    private static void Collect(FixtureCase fixture, CrudeRenaming renaming)
    {
        var rules = fixture.Engine == "postgres" ? SqlDialectRules.PostgreSql : SqlDialectRules.SqlServer;
        var names = new HashSet<string>(StringComparer.Ordinal);
        Originals(fixture.Query, rules, names, []);
        foreach (var name in names.Where(name => renaming.IsKept(name)))
        {
            Add(Kept, $"{fixture.Engine}\t{name.ToLowerInvariant()}", fixture.Name);
        }

        foreach (var row in File.ReadAllLines(renaming.Renamed.Folder + ".mapping.tsv").Skip(1))
        {
            var parts = row.Split('\t');
            var source = parts[3];
            if (
                parts[0] is not ("=" or "l")
                && !source.Contains(".answer.", StringComparison.Ordinal)
                && !source.Contains("request.relations", StringComparison.Ordinal)
                && !source.StartsWith("table.request", StringComparison.Ordinal)
            )
            {
                var where = source.StartsWith("plan.", StringComparison.Ordinal) ? source : source.Split(' ')[0];
                Add(NotFromCatalog, $"{fixture.Engine}\t{parts[0]}\t{parts[1]}\t{where}", fixture.Name);
            }
        }
    }

    private static void Add(SortedDictionary<string, SortedSet<string>> into, string key, string value)
    {
        if (!into.TryGetValue(key, out var set))
        {
            set = [with(StringComparer.Ordinal)];
            into[key] = set;
        }

        _ = set.Add(value);
    }

    private static string Grouped(string header, SortedDictionary<string, SortedSet<string>> rows)
    {
        var table = new StringBuilder(header);
        foreach (var (key, cases) in rows)
        {
            _ = table.Append(
                CultureInfo.InvariantCulture,
                $"{key}\t{cases.Count}\t{string.Join(",", cases.Take(3))}\n"
            );
        }

        return table.ToString();
    }

    private static List<string> LeaksOf(FixtureCase fixture, FixtureCase copy, Func<string, bool> isKept)
    {
        var rules = fixture.Engine == "postgres" ? SqlDialectRules.PostgreSql : SqlDialectRules.SqlServer;
        var names = new HashSet<string>(StringComparer.Ordinal);
        var literals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in new[] { File.ReadAllText(SchemaOf(fixture)), fixture.Query })
        {
            Originals(text, rules, names, literals);
        }

        _ = names.RemoveWhere(name => isKept(name));
        _ = literals.RemoveWhere(static literal => literal.Length == 0);

        var strings = CopyStrings(copy).ToList();
        var words = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (file, value) in strings)
        {
            foreach (var word in WordsOf(value))
            {
                _ = words.TryAdd(word.ToLowerInvariant(), file);
            }
        }

        var leaks = new List<string>();
        foreach (var name in names.Select(static n => n.ToLowerInvariant()).Distinct())
        {
            if (words.TryGetValue(name, out var file))
            {
                leaks.Add($"name {name} in {file}");
            }
        }

        foreach (var literal in literals)
        {
            var single = WordsOf(literal).ToList();
            string? found = null;
            if (single.Count == 1 && single[0] == literal)
            {
                found = words.GetValueOrDefault(literal.ToLowerInvariant());
            }
            else if (literal.Length >= 4)
            {
                found = strings.FirstOrDefault(s => s.Value.Contains(literal, StringComparison.Ordinal)).File;
            }

            if (found is not null && !isKept(literal))
            {
                leaks.Add($"literal '{literal}' in {found}");
            }
        }

        return leaks;
    }

    private static string SchemaOf(FixtureCase fixture)
    {
        if (!SpikeCases.IsRealistic(fixture))
        {
            return Path.Combine(FixtureCases.Root, fixture.Engine, "schema.sql");
        }

        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "SqlSource.slnx")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        return Path.Combine(
            root,
            "tests",
            "DatabaseTests",
            fixture.Engine == "postgres" ? "Postgres" : "SqlServer",
            "Realistic",
            "schema.sql"
        );
    }

    // The names and literals of a SQL text: words of plain text, quoted names, parameters and markers' values, and the
    // contents of literals.
    private static void Originals(string text, SqlDialectRules rules, HashSet<string> names, HashSet<string> literals)
    {
        var lexed = SqlLexer.Lex(text, rules);
        for (var index = 0; index < lexed.Lexemes.Count; index++)
        {
            var lexeme = lexed.Lexemes[index];
            var part = text[lexeme.Span.Start..lexeme.Span.End];
            switch (lexeme.Kind)
            {
                case SqlLexemeKind.Text:
                    var words = WordsOf(part).Where(static w => char.IsLetter(w[0]) || w[0] == '_').ToList();
                    var prefixed =
                        index + 1 < lexed.Lexemes.Count
                        && lexed.Lexemes[index + 1].Kind == SqlLexemeKind.Quoted
                        && words.Count > 0
                        && words[^1].Length == 1
                        && part.TrimEnd('&').EndsWith(words[^1], StringComparison.Ordinal);
                    names.UnionWith(prefixed ? words.Take(words.Count - 1) : words);
                    break;
                case SqlLexemeKind.Quoted when part[0] is '\'' or '$':
                    _ = literals.Add(
                        part[0] == '$' ? part.Trim('$') : part[1..^1].Replace("''", "'", StringComparison.Ordinal)
                    );
                    break;
                case SqlLexemeKind.Quoted:
                    _ = names.Add(part[1..^1]);
                    break;
                case SqlLexemeKind.Parameter:
                    _ = names.Add(part[1..]);
                    break;
                case SqlLexemeKind.LineComment when part.StartsWith("-- name:", StringComparison.Ordinal):
                    _ = names.Add(part["-- name:".Length..].Trim());
                    break;
                case SqlLexemeKind.LineComment when part.StartsWith("-- param:", StringComparison.Ordinal):
                    Originals(part["-- param:".Length..], rules, names, literals);
                    break;
                case SqlLexemeKind.LineComment:
                case SqlLexemeKind.BlockComment:
                case SqlLexemeKind.Hint:
                default:
                    break;
            }
        }
    }

    private static IEnumerable<string> WordsOf(string text)
    {
        var at = 0;
        while (at < text.Length)
        {
            if (char.IsLetterOrDigit(text[at]) || text[at] == '_')
            {
                var start = at;
                while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] is '_' or '$'))
                {
                    at++;
                }

                yield return text[start..at];
            }
            else
            {
                at++;
            }
        }
    }

    // Every string of the copy that holds the user's text: query.sql whole, the capture's strings but for the fixed
    // values of a plan's fixed keys and the members renamed as kept, and the entry's names.
    private static IEnumerable<(string File, string Value)> CopyStrings(FixtureCase copy)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var literals = new HashSet<string>(StringComparer.Ordinal);
        Originals(
            copy.Query,
            copy.Engine == "postgres" ? SqlDialectRules.PostgreSql : SqlDialectRules.SqlServer,
            names,
            literals
        );
        foreach (var value in names.Concat(literals))
        {
            yield return (FixtureCase.QueryFile, value);
        }

        var capture = JsonNode.Parse(copy.Capture)!;
        foreach (var call in ((JsonArray)capture["calls"]!).OfType<JsonObject>())
        {
            var method = call["method"]!.GetValue<string>();
            foreach (var (path, value) in Strings(call, method))
            {
                if (path == method + ".answer.plan")
                {
                    foreach (var (key, inner) in Strings(JsonNode.Parse(value), "plan"))
                    {
                        if (
                            !key.EndsWith("Node Type", StringComparison.Ordinal)
                            && !key.EndsWith("Join Type", StringComparison.Ordinal)
                            && !key.EndsWith("Parent Relationship", StringComparison.Ordinal)
                            && !key.EndsWith("Strategy", StringComparison.Ordinal)
                            && !key.EndsWith("Command", StringComparison.Ordinal)
                        )
                        {
                            yield return (
                                $"{FixtureCase.CaptureFile} {method} plan {key[(key.LastIndexOf('.') + 1)..]}",
                                inner
                            );
                        }
                    }
                }
                else if (!CrudeRenamer.IsKeptField(copy.Engine, path))
                {
                    yield return ($"{FixtureCase.CaptureFile} {path}", value);
                }
            }
        }

        var expected = JsonNode.Parse(copy.Expected)!;
        foreach (var (name, _) in (JsonObject)expected["queries"]!)
        {
            yield return ($"{FixtureCase.ExpectedFile} query name", name);
        }

        foreach (var (path, value) in Strings(expected["queries"], "entry"))
        {
            if (!ClosedMembers.Contains(path[(path.LastIndexOf('.') + 1)..]))
            {
                yield return ($"{FixtureCase.ExpectedFile} {path}", value);
            }
        }
    }

    private static IEnumerable<(string Path, string Value)> Strings(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    var child = path + "." + name;
                    foreach (var found in Strings(value, child))
                    {
                        yield return found;
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var found in Strings(item, path + "[]"))
                    {
                        yield return found;
                    }
                }

                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                yield return (path, value.GetValue<string>());
                break;
            default:
                break;
        }
    }

    // ---- The tables.

    private static string CaseName(FixtureCase fixture) =>
        (SpikeCases.IsRealistic(fixture) ? "realistic/" : "") + fixture.Name;

    private static string Clean(string text) =>
        text.Replace("\t", " ", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Table(List<Result> results, string run)
    {
        var table = new StringBuilder("engine\tcase\toutcome\tprovenance\trequestDiffs\tnotes\n");
        foreach (var result in results.Where(result => result.Run == run))
        {
            var diffs =
                result.Diffs.Count == 0
                    ? "0"
                    : $"{result.Diffs.Count}: {string.Join(",", result.Diffs.Select(static d => d.Method))}";
            var notes = string.Join("; ", result.Notes.Concat(result.Leaks.Select(static l => "leak " + l)));
            if (result.Operands.Length > 0)
            {
                notes = result.Operands + (notes.Length > 0 ? "; " + notes : "");
            }

            _ = table.Append(
                CultureInfo.InvariantCulture,
                $"{result.Case.Engine}\t{CaseName(result.Case)}\t{Clean(result.Outcome)}\t{Clean(result.Provenance)}"
                    + $"\t{diffs}\t{Clean(notes)}\n"
            );
        }

        return table.ToString();
    }

    private static string DiffTable(List<Result> results)
    {
        var table = new StringBuilder("run\tengine\tcase\tmethod\tasked\trecorded\n");
        foreach (var result in results)
        {
            foreach (var diff in result.Diffs)
            {
                _ = table.Append(
                    CultureInfo.InvariantCulture,
                    $"{result.Run}\t{result.Case.Engine}\t{CaseName(result.Case)}\t{diff.Method}"
                        + $"\t{Clean(Cut(diff.Asked, diff.Recorded))}\t{Clean(Cut(diff.Recorded, diff.Asked))}\n"
                );
            }
        }

        return table.ToString();
    }

    private static string LeakTable(List<Result> results)
    {
        var table = new StringBuilder("run\tengine\tcase\tleak\n");
        foreach (var result in results)
        {
            foreach (var leak in result.Leaks)
            {
                _ = table.Append(
                    CultureInfo.InvariantCulture,
                    $"{result.Run}\t{result.Case.Engine}\t{CaseName(result.Case)}\t{Clean(leak)}\n"
                );
            }
        }

        return table.ToString();
    }

    // Each case that a run without one rule does not replay to the same entry and provenance.
    private static string RuleEffects(List<Result> results)
    {
        var table = new StringBuilder("run\tengine\tcase\tfixture\toutcome\tprovenance\n");
        foreach (
            var result in results.Where(static r =>
                r.Run.StartsWith("all-but-", StringComparison.Ordinal) || r.Run == "design"
            )
        )
        {
            if (result.Outcome != "entry" || result.Provenance != "same")
            {
                _ = table.Append(
                    CultureInfo.InvariantCulture,
                    $"{result.Run}\t{result.Case.Engine}\t{CaseName(result.Case)}"
                        + $"\t{!SpikeCases.IsRealistic(result.Case)}"
                        + $"\t{Clean(result.Outcome)}\t{Clean(result.Provenance)}\n"
                );
            }
        }

        return table.ToString();
    }

    private static string NoteTable(List<Result> results)
    {
        var table = new StringBuilder("run\tnote\tcases\n");
        foreach (
            var group in results
                .SelectMany(static r => r.Notes.Select(n => (r.Run, Note: n)))
                .GroupBy(static p => (p.Run, p.Note))
                .OrderBy(static g => g.Key.Run, StringComparer.Ordinal)
                .ThenBy(static g => g.Key.Note, StringComparer.Ordinal)
        )
        {
            _ = table.Append(
                CultureInfo.InvariantCulture,
                $"{group.Key.Run}\t{Clean(group.Key.Note)}\t{group.Count()}\n"
            );
        }

        return table.ToString();
    }

    private static string Counts(List<Result> results)
    {
        var table = new StringBuilder(
            "run\tengine\tscope\tcases\tentry\tsameProvenance\tentryDiffers\tmiss\tfailure\tother"
                + "\twithRequestDiffs\twithLeaks\tselfTestLeaks\n"
        );
        foreach (var (run, _) in Runs)
        {
            foreach (var engine in new[] { "postgres", "mssql", "both" })
            {
                foreach (var scope in new[] { "fixtures", "all" })
                {
                    var chosen = results
                        .Where(r => r.Run == run)
                        .Where(r => engine == "both" || r.Case.Engine == engine)
                        .Where(r => scope == "all" || !SpikeCases.IsRealistic(r.Case))
                        .ToList();
                    var entryDiffers = chosen.Count(static r =>
                        r.Outcome.StartsWith("entry-differs", StringComparison.Ordinal)
                    );
                    var other = chosen.Count(static r =>
                        !r.Outcome.StartsWith("entry", StringComparison.Ordinal)
                        && !r.Outcome.StartsWith("miss", StringComparison.Ordinal)
                        && !r.Outcome.StartsWith("failure", StringComparison.Ordinal)
                    );
                    _ = table.Append(
                        CultureInfo.InvariantCulture,
                        $"{run}\t{engine}\t{scope}\t{chosen.Count}"
                            + $"\t{chosen.Count(static r => r.Outcome == "entry")}"
                            + $"\t{chosen.Count(static r => r.Provenance == "same")}"
                            + $"\t{entryDiffers}"
                            + $"\t{chosen.Count(static r => r.Outcome.StartsWith("miss", StringComparison.Ordinal))}"
                            + $"\t{chosen.Count(static r => r.Outcome.StartsWith("failure", StringComparison.Ordinal))}"
                            + $"\t{other}"
                            + $"\t{chosen.Count(static r => r.Diffs.Count > 0)}"
                            + $"\t{chosen.Count(static r => r.Leaks.Count > 0)}"
                            + $"\t{chosen.Sum(static r => r.SelfTestLeaks)}\n"
                    );
                }
            }
        }

        return table.ToString();
    }
}
