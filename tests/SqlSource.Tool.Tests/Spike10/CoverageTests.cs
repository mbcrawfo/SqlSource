using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Spike10;

// Finding 3 of the phase 10.0 spike: where each word of the corpora comes from, so that a renamer that keeps the
// engine's vocabulary verbatim knows what it can keep and what it must rename.  It reads the lists the container
// projects' VocabularyTests wrote (t4-pg-<14|18>-*, t4-mssql-<2017|default>-catalog, sources/mssql-keywords.tsv).  Run
// with SPIKE10_OUT=<folder> dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class
// "SqlSource.Tool.Tests.Spike10.CoverageTests" --explicit only.
public sealed partial class CoverageTests
{
    // The keys of a plan whose strings are SQL expressions, and the keys whose strings are names.
    private static readonly HashSet<string> ExpressionKeys =
    [
        with(StringComparer.Ordinal),
        "Output",
        "Filter",
        "Function Call",
        "Group Key",
        "Group Keys",
        "Hash Cond",
        "Hash Keys",
        "Index Cond",
        "Join Filter",
        "Merge Cond",
        "One-Time Filter",
        "Recheck Cond",
        "Run Condition",
        "Sort Key",
        "Window",
    ];

    private static readonly HashSet<string> NameKeys =
    [
        with(StringComparer.Ordinal),
        "Alias",
        "CTE Name",
        "Relation Name",
        "Schema",
        "Subplan Name",
        "Function Name",
        "Index Name",
        "Conflict Arbiter Indexes",
    ];

    // The keys of an answer whose strings are names the user chose.
    private static readonly HashSet<string> UserKeys =
    [
        with(StringComparer.Ordinal),
        "name",
        "schema",
        "nspname",
        "typname",
        "enumLabels",
        "sourceDatabase",
        "sourceSchema",
        "sourceTable",
        "sourceColumn",
        "userTypeSchema",
        "userTypeName",
        "database",
        "table",
    ];

    private static readonly string[] SystemSchemas = ["pg_catalog", "information_schema", "sys"];

    // The names the design lists as made by the planner, matched by form (on the lower-cased word or name).
    private static readonly (string Form, Regex Pattern)[] PlannerForms =
    [
        ("*SELECT* <k>", StarSelect()),
        ("*VALUES*", StarValues()),
        ("column<n>", ColumnN()),
        ("col<n>", ColN()),
        ("InitPlan <k>", Plan("initplan")),
        ("SubPlan <k>", Plan("subplan")),
        ("unnamed_subquery", Exact("unnamed_subquery")),
        ("ANY_subquery", Exact("any_subquery")),
        ("excluded", Exact("excluded")),
        ("worktable", Exact("worktable")),
    ];

    private static IEnumerable<string> FormsOf(string text) =>
        PlannerForms.Where(f => f.Pattern.IsMatch(text)).Select(static f => f.Form);

    [GeneratedRegex(@"^\*select\* \d+$")]
    private static partial Regex StarSelect();

    [GeneratedRegex(@"^\*values\*$")]
    private static partial Regex StarValues();

    [GeneratedRegex(@"^column\d+$")]
    private static partial Regex ColumnN();

    [GeneratedRegex(@"^col\d+$")]
    private static partial Regex ColN();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    private static Regex Plan(string word) => new($@"^{word}( \d+)?$", RegexOptions.CultureInvariant);

    private static Regex Exact(string word) => new($"^{word}$", RegexOptions.CultureInvariant);

    // One word of a case: where it was read, and whether a parenthesis follows it.
    private readonly record struct Occurrence(string Word, string Source, bool Call);

    private sealed class Row
    {
        public readonly HashSet<string> Cases = [with(StringComparer.Ordinal)];
        public readonly HashSet<string> Sources = [with(StringComparer.Ordinal)];
        public int User;
        public int PlanName;
        public int Asked;
        public int NotAlone;
        public int None;
        public string Example = "";
    }

    private sealed class Lists
    {
        // word -> kind -> images; the words of a keyword list, as tags.
        public readonly Dictionary<string, SortedDictionary<string, SortedSet<string>>> Catalog = [];
        public readonly Dictionary<string, SortedSet<string>> Keywords = [];
        public readonly HashSet<string> Operators = [with(StringComparer.Ordinal)];
    }

    [Fact(Explicit = true)]
    public void Words_OfEveryCase_ByWhereTheyCome()
    {
        var pgLists = LoadPostgres();
        var msLists = LoadSqlServer();
        var rows = new Dictionary<(string Engine, string Word), Row>();
        var calls =
            new Dictionary<(string Engine, string Word), (HashSet<string> Cases, int Uses, HashSet<string> Asked)>();
        var operators = new Dictionary<string, (HashSet<string> Cases, int Uses, HashSet<string> Asked)>(
            StringComparer.Ordinal
        );
        var userEngine = new Dictionary<(string Engine, string Word), HashSet<string>>();
        var formsSeen = new Dictionary<string, (HashSet<string> Sql, HashSet<string> Plan, string Example)>(
            StringComparer.Ordinal
        );
        var otherPlannerNames = new SortedDictionary<string, (HashSet<string> Cases, string Example)>(
            StringComparer.Ordinal
        );
        var cases = SpikeCases.All();

        foreach (var fixture in cases)
        {
            var lists = fixture.Engine == "postgres" ? pgLists : msLists;
            var root = (JsonObject)JsonNode.Parse(fixture.Capture)!;
            var calls0 = ((JsonArray)root["calls"]!).OfType<JsonObject>().ToList();
            var key = fixture.ToString();
            var user = UserNames(calls0, fixture.Engine);
            var sqlOccurrences = SqlWords(fixture).ToList();
            var occurrences = new List<Occurrence>(sqlOccurrences);
            var planNames = new HashSet<string>(StringComparer.Ordinal);
            var askedFunctions = new HashSet<string>(StringComparer.Ordinal);
            var askedOperators = new HashSet<string>(StringComparer.Ordinal);
            var notAlone = new HashSet<string>(StringComparer.Ordinal);
            foreach (var call in calls0.OrderBy(static c => c["method"]?.GetValue<string>() == "overloads" ? 0 : 1))
            {
                switch (call["method"]?.GetValue<string>())
                {
                    case "overloads":
                        askedFunctions.UnionWith(Strings(call["request"]?["functions"]).Select(Fold));
                        askedOperators.UnionWith(Strings(call["request"]?["operators"]));
                        notAlone.UnionWith(Strings(call["answer"]?["functions"]).Select(Fold));
                        notAlone.UnionWith(Strings(call["answer"]?["operators"]));
                        break;
                    case "explain":
                        if (PlanOf(call) is { } plan)
                        {
                            PlanOccurrences(plan, occurrences, planNames, operators, key, askedOperators, formsSeen);
                        }

                        break;
                    default:
                        break;
                }
            }

            var sqlWords = new HashSet<string>(sqlOccurrences.Select(static o => o.Word), StringComparer.Ordinal);
            foreach (var word in sqlWords)
            {
                foreach (var form in FormsOf(word))
                {
                    Note(formsSeen, form, key, plan: false, word);
                }
            }

            foreach (var name in planNames.Where(n => !sqlWords.Contains(n) && !user.Contains(n)))
            {
                if (!FormsOf(name).Any())
                {
                    var pattern = Digits().Replace(name, "<n>");
                    var (set, example) = otherPlannerNames.GetValueOrDefault(
                        pattern,
                        ([with(StringComparer.Ordinal)], name)
                    );
                    _ = set.Add(key);
                    otherPlannerNames[pattern] = (set, example);
                }
            }

            var seenInCase = new HashSet<string>(StringComparer.Ordinal);
            foreach (var occurrence in occurrences)
            {
                var word = occurrence.Word;
                var row = rows.GetValueOrDefault((fixture.Engine, word)) ?? new Row { Example = key };
                rows[(fixture.Engine, word)] = row;
                _ = row.Cases.Add(key);
                _ = row.Sources.Add(occurrence.Source);
                if (!seenInCase.Add(word))
                {
                    continue;
                }

                var isUser = user.Contains(word);
                var isPlanName = planNames.Contains(word);
                var isAsked = askedFunctions.Contains(word);
                var keyword = lists.Keywords.ContainsKey(word);
                var listed = lists.Catalog.ContainsKey(word);
                var planner = FormsOf(word).Any();
                row.User += isUser ? 1 : 0;
                row.PlanName += isPlanName ? 1 : 0;
                row.Asked += isAsked ? 1 : 0;
                row.NotAlone += notAlone.Contains(word) ? 1 : 0;
                row.None += isUser || isPlanName || isAsked || keyword || listed || planner ? 0 : 1;
                if (isUser && (keyword || listed))
                {
                    var set = userEngine.GetValueOrDefault((fixture.Engine, word)) ?? [with(StringComparer.Ordinal)];
                    _ = set.Add(key);
                    userEngine[(fixture.Engine, word)] = set;
                }
            }

            foreach (var occurrence in occurrences.Where(o => o.Call))
            {
                var word = occurrence.Word;
                var listed = lists.Catalog.TryGetValue(word, out var kinds) && kinds.ContainsKey("function");
                if (user.Contains(word) || (!listed && lists.Keywords.ContainsKey(word)))
                {
                    continue;
                }

                var entry = calls.GetValueOrDefault(
                    (fixture.Engine, word),
                    ([with(StringComparer.Ordinal)], 0, [with(StringComparer.Ordinal)])
                );
                _ = entry.Cases.Add(key);
                if (askedFunctions.Contains(word))
                {
                    _ = entry.Asked.Add(key);
                }

                calls[(fixture.Engine, word)] = (entry.Cases, entry.Uses + 1, entry.Asked);
            }
        }

        Write(rows, calls, operators, userEngine, formsSeen, otherPlannerNames, pgLists, msLists, cases.Count);
    }

    private static void Write(
        Dictionary<(string Engine, string Word), Row> rows,
        Dictionary<(string Engine, string Word), (HashSet<string> Cases, int Uses, HashSet<string> Asked)> calls,
        Dictionary<string, (HashSet<string> Cases, int Uses, HashSet<string> Asked)> operators,
        Dictionary<(string Engine, string Word), HashSet<string>> userEngine,
        Dictionary<string, (HashSet<string> Sql, HashSet<string> Plan, string Example)> formsSeen,
        SortedDictionary<string, (HashSet<string> Cases, string Example)> otherPlannerNames,
        Lists pg,
        Lists ms,
        int caseCount
    )
    {
        Lists ListsOf(string engine) => engine == "postgres" ? pg : ms;
        var coverage = new StringBuilder(
            "engine\tword\tcases\tsources\tkeyword\tlist\tplanner\tuserCases\tplanNameCases\taskedCases"
                + "\tnotAloneCases\tnoClassCases\texampleCase\n"
        );
        foreach (
            var ((engine, word), row) in rows.OrderBy(static p => p.Key.Engine, StringComparer.Ordinal)
                .ThenBy(static p => p.Key.Word, StringComparer.Ordinal)
        )
        {
            var lists = ListsOf(engine);
            var keyword = lists.Keywords.TryGetValue(word, out var tags) ? string.Join('/', tags) : "";
            var listed = lists.Catalog.TryGetValue(word, out var kinds)
                ? string.Join('|', kinds.Select(static k => k.Key + "[" + string.Join(',', k.Value) + "]"))
                : "";
            var formNames = string.Join('|', FormsOf(word));
            _ = coverage.Append(
                CultureInfo.InvariantCulture,
                $"{engine}\t{word}\t{row.Cases.Count}\t{string.Join(',', row.Sources.Order())}\t{keyword}\t{listed}"
                    + $"\t{formNames}\t{row.User}\t{row.PlanName}\t{row.Asked}\t{row.NotAlone}"
                    + $"\t{row.None}\t{row.Example}\n"
            );
        }

        File.WriteAllText(SpikeCases.Out("t4-coverage.tsv"), coverage.ToString());

        var functions = new StringBuilder("engine\tname\tuses\tcases\tlist\tkeyword\taskedInCases\n");
        foreach (
            var ((engine, word), (set, uses, asked)) in calls
                .OrderBy(static p => p.Key.Engine, StringComparer.Ordinal)
                .ThenBy(static p => p.Key.Word, StringComparer.Ordinal)
        )
        {
            var lists = ListsOf(engine);
            var listed =
                lists.Catalog.TryGetValue(word, out var kinds) && kinds.TryGetValue("function", out var images)
                    ? string.Join(',', images)
                    : "";
            var keyword = lists.Keywords.TryGetValue(word, out var tags) ? string.Join('/', tags) : "";
            _ = functions.Append(
                CultureInfo.InvariantCulture,
                $"{engine}\t{word}\t{uses}\t{set.Count}\t{listed}\t{keyword}\t{asked.Count}\n"
            );
        }

        File.WriteAllText(SpikeCases.Out("t4-functions.tsv"), functions.ToString());

        var ops = new StringBuilder("operator\tuses\tcases\tinCatalog\taskedInCases\n");
        foreach (var (name, (set, uses, asked)) in operators.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            _ = ops.Append(
                CultureInfo.InvariantCulture,
                $"{name}\t{uses}\t{set.Count}\t{(pg.Operators.Contains(name) ? "yes" : "no")}\t{asked.Count}\n"
            );
        }

        File.WriteAllText(SpikeCases.Out("t4-operators.tsv"), ops.ToString());

        var overlap = new StringBuilder("engine\tname\tcases\tkeyword\tlist\n");
        foreach (
            var ((engine, word), set) in userEngine
                .OrderBy(static p => p.Key.Engine, StringComparer.Ordinal)
                .ThenBy(static p => p.Key.Word, StringComparer.Ordinal)
        )
        {
            var lists = ListsOf(engine);
            var keyword = lists.Keywords.TryGetValue(word, out var tags) ? string.Join('/', tags) : "";
            var listed = lists.Catalog.TryGetValue(word, out var kinds) ? string.Join('|', kinds.Keys) : "";
            _ = overlap.Append(CultureInfo.InvariantCulture, $"{engine}\t{word}\t{set.Count}\t{keyword}\t{listed}\n");
        }

        File.WriteAllText(SpikeCases.Out("t4-user-engine-words.tsv"), overlap.ToString());

        var planner = new StringBuilder("form\tsqlCases\tplanCases\texample\n");
        foreach (var (form, _) in PlannerForms)
        {
            var (sql, plan, example) = formsSeen.GetValueOrDefault(form, ([], [], ""));
            _ = planner.Append(CultureInfo.InvariantCulture, $"{form}\t{sql.Count}\t{plan.Count}\t{example}\n");
        }

        foreach (var (pattern, (set, example)) in otherPlannerNames)
        {
            _ = planner.Append(CultureInfo.InvariantCulture, $"[further] {pattern}\t0\t{set.Count}\t{example}\n");
        }

        File.WriteAllText(SpikeCases.Out("t4-planner-names.tsv"), planner.ToString());

        var sizes = new StringBuilder("list\tbytes\tlines\tgzipBytes\n");
        foreach (
            var file in new[]
            {
                "t4-pg-14-keywords.tsv",
                "t4-pg-14-catalog.tsv",
                "t4-pg-18-keywords.tsv",
                "t4-pg-18-catalog.tsv",
                "t4-mssql-2017-catalog.tsv",
                "t4-mssql-default-catalog.tsv",
            }
        )
        {
            Size(sizes, SpikeCases.Out(file), file);
        }

        Size(
            sizes,
            Path.Combine(Path.GetDirectoryName(SpikeCases.Out("x"))!, "..", "sources", "mssql-keywords.tsv"),
            "mssql-keywords.tsv"
        );
        File.WriteAllText(SpikeCases.Out("t4-list-sizes.tsv"), sizes.ToString());
        File.WriteAllText(SpikeCases.Out("t4-case-count.txt"), $"{caseCount}\n");
    }

    private static void Size(StringBuilder into, string path, string name)
    {
        var bytes = File.ReadAllBytes(path);
        using var memory = new MemoryStream();
        using (
            var zip = new System.IO.Compression.GZipStream(
                memory,
                System.IO.Compression.CompressionLevel.SmallestSize,
                leaveOpen: true
            )
        )
        {
            zip.Write(bytes);
        }

        var lines = bytes.Count(static b => b == (byte)'\n');
        _ = into.Append(CultureInfo.InvariantCulture, $"{name}\t{bytes.Length}\t{lines}\t{memory.Length}\n");
    }

    private static JsonNode? PlanOf(JsonObject call) =>
        call["answer"]?["plan"] is JsonValue text && text.TryGetValue<string>(out var json)
            ? JsonNode.Parse(json)
            : null;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "PostgreSQL folds to lower case."
    )]
    private static string Fold(string text) => text.ToLowerInvariant();

    private static void Note(
        Dictionary<string, (HashSet<string> Sql, HashSet<string> Plan, string Example)> seen,
        string form,
        string key,
        bool plan,
        string example
    )
    {
        var (sql, planCases, existing) = seen.GetValueOrDefault(
            form,
            ([with(StringComparer.Ordinal)], [with(StringComparer.Ordinal)], example)
        );
        _ = (plan ? planCases : sql).Add(key);
        seen[form] = (sql, planCases, existing);
    }

    private static IEnumerable<string> Strings(JsonNode? node) =>
        node is JsonArray array
            ? array.Select(static e => e?.GetValue<string>()).Where(static s => s is not null).Select(static s => s!)
            : [];

    // The words of the case's query.sql that the lexer leaves as text, folded: not a string, a quoted name, a
    // comment or a parameter.
    private static IEnumerable<Occurrence> SqlWords(FixtureCase fixture)
    {
        var text = fixture.Query;
        var postgres = fixture.Engine == "postgres";
        var lexed = SqlLexer.Lex(text, postgres ? SqlDialectRules.PostgreSql : SqlDialectRules.SqlServer);
        for (var i = 0; i < lexed.Lexemes.Count; i++)
        {
            var lexeme = lexed.Lexemes[i];
            if (lexeme.Kind != SqlLexemeKind.Text)
            {
                continue;
            }

            var end = lexeme.Span.End;
            var at = lexeme.Span.Start;
            while (at < end)
            {
                var c = text[at];
                if (IsStart(c, postgres))
                {
                    var start = at;
                    while (at < end && IsPart(text[at], postgres))
                    {
                        at++;
                    }

                    // A prefix of a string (N'x', E'x', B'x', U&'x').
                    var nextQuoted =
                        at == end && i + 1 < lexed.Lexemes.Count && lexed.Lexemes[i + 1].Kind == SqlLexemeKind.Quoted;
                    if (nextQuoted && at - start <= 2)
                    {
                        continue;
                    }

                    var after = at;
                    while (after < end && char.IsWhiteSpace(text[after]))
                    {
                        after++;
                    }

                    var word = Fold(text[start..at]);
                    var isVariable = !postgres && word.StartsWith('@');
                    if (!isVariable)
                    {
                        yield return new Occurrence(word, "sql", after < end && text[after] == '(');
                    }
                }
                else if (char.IsAsciiDigit(c))
                {
                    while (at < end && (IsPart(text[at], postgres) || text[at] == '.'))
                    {
                        at++;
                    }
                }
                else
                {
                    at++;
                }
            }
        }
    }

    private static bool IsStart(char c, bool postgres) =>
        postgres ? PostgresIdentifiers.IsWordStart(c) : char.IsLetter(c) || c is '_' or '#' or '@';

    private static bool IsPart(char c, bool postgres) =>
        postgres ? PostgresIdentifiers.IsWordPart(c) : Parsing.Quoting.QuoteReader.IsNameCharacterOfSqlServer(c);

    private static void PlanOccurrences(
        JsonNode plan,
        List<Occurrence> occurrences,
        HashSet<string> planNames,
        Dictionary<string, (HashSet<string> Cases, int Uses, HashSet<string> Asked)> operators,
        string key,
        HashSet<string> askedOperators,
        Dictionary<string, (HashSet<string> Sql, HashSet<string> Plan, string Example)> formsSeen
    )
    {
        foreach (var (name, value) in Leaves(plan, null))
        {
            if (NameKeys.Contains(name))
            {
                var folded = Fold(value);
                foreach (var form in FormsOf(folded))
                {
                    Note(formsSeen, form, key, plan: true, value);
                }

                if (name == "Subplan Name")
                {
                    foreach (var word in WordsOf(value))
                    {
                        occurrences.Add(new Occurrence(word, "plan", false));
                    }

                    // "InitPlan 1 (returns $0)" is the planner's name as a whole.
                    _ = planNames.Add(Fold(value.Split(' ')[0]));
                    continue;
                }

                _ = planNames.Add(folded);
                foreach (var word in WordsOf(value))
                {
                    _ = planNames.Add(word);
                    occurrences.Add(new Occurrence(word, "plan", false));
                }

                continue;
            }

            if (!ExpressionKeys.Contains(name))
            {
                continue;
            }

            var tokens = PlanTokens.Read(value);
            if (tokens is null)
            {
                continue;
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var text = value[token.Start..token.End];
                if (token.Kind == PlanTokens.Kind.Word)
                {
                    foreach (var form in FormsOf(Fold(text)))
                    {
                        Note(formsSeen, form, key, plan: true, text);
                    }

                    occurrences.Add(
                        new Occurrence(
                            Fold(text),
                            "plan",
                            i + 1 < tokens.Count && tokens[i + 1].Kind == PlanTokens.Kind.Open
                        )
                    );
                }
                else if (token.Kind == PlanTokens.Kind.QuotedName)
                {
                    var inner = Fold(text[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal));
                    foreach (var form in FormsOf(inner))
                    {
                        Note(formsSeen, form, key, plan: true, text);
                    }
                }
                else if (token.Kind == PlanTokens.Kind.Other && !text.StartsWith('$'))
                {
                    var entry = operators.GetValueOrDefault(
                        text,
                        ([with(StringComparer.Ordinal)], 0, [with(StringComparer.Ordinal)])
                    );
                    _ = entry.Cases.Add(key);
                    if (askedOperators.Contains(text))
                    {
                        _ = entry.Asked.Add(key);
                    }

                    operators[text] = (entry.Cases, entry.Uses + 1, entry.Asked);
                }
            }
        }
    }

    private static IEnumerable<string> WordsOf(string value)
    {
        var at = 0;
        while (at < value.Length)
        {
            if (PostgresIdentifiers.IsWordStart(value[at]))
            {
                var start = at;
                while (at < value.Length && PostgresIdentifiers.IsWordPart(value[at]))
                {
                    at++;
                }

                yield return Fold(value[start..at]);
            }
            else
            {
                at++;
            }
        }
    }

    private static IEnumerable<(string Key, string Value)> Leaves(JsonNode? node, string? key)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    foreach (var found in Leaves(value, name))
                    {
                        yield return found;
                    }
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    foreach (var found in Leaves(element, key))
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

    // The folded words of the user's names in the case's catalog answers (and, for SQL Server, the request's database).
    private static HashSet<string> UserNames(List<JsonObject> calls, string engine)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in calls)
        {
            var method = call["method"]?.GetValue<string>();
            if (method is "explain" or "overloads" or "open" or "prepare" or "check" or "spellings")
            {
                continue;
            }

            Collect(call["answer"], names, engine);
            Collect(call["request"], names, engine);
        }

        return names;
    }

    private static void Collect(JsonNode? node, HashSet<string> names, string engine)
    {
        switch (node)
        {
            case JsonObject obj:
                var system = SystemSchemas.Contains(
                    obj["nspname"]?.GetValue<string>() ?? obj["userTypeSchema"]?.GetValue<string>() ?? ""
                );
                foreach (var (name, value) in obj)
                {
                    if (system && name is "typname" or "userTypeName")
                    {
                        continue;
                    }

                    if (UserKeys.Contains(name) && !(system && name is "nspname" or "userTypeSchema"))
                    {
                        foreach (var text in LeafStrings(value))
                        {
                            names.UnionWith(WordsOfEngine(text, engine));
                        }
                    }
                    else
                    {
                        Collect(value, names, engine);
                    }
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    Collect(element, names, engine);
                }

                break;
            default:
                break;
        }
    }

    private static IEnumerable<string> LeafStrings(JsonNode? value)
    {
        if (value is JsonArray)
        {
            return Strings(value);
        }

        return value is JsonValue leaf && leaf.TryGetValue<string>(out var text) ? [text] : [];
    }

    private static IEnumerable<string> WordsOfEngine(string text, string engine) =>
        engine == "postgres" ? WordsOf(text) : WordsOf(text).Select(static w => w.TrimStart('@'));

    private static Lists LoadPostgres()
    {
        var lists = new Lists();
        foreach (var image in new[] { "14", "18" })
        {
            foreach (var line in File.ReadLines(SpikeCases.Out($"t4-pg-{image}-keywords.tsv")).Skip(1))
            {
                var columns = line.Split('\t');
                var word = Fold(columns[0]);
                if (!lists.Keywords.TryGetValue(word, out var tags))
                {
                    tags = [];
                    lists.Keywords[word] = tags;
                }

                _ = tags.Add(columns[1]);
            }

            foreach (var line in File.ReadLines(SpikeCases.Out($"t4-pg-{image}-catalog.tsv")).Skip(1))
            {
                var columns = line.Split('\t');
                if (columns[0] == "operator")
                {
                    _ = lists.Operators.Add(columns[1]);
                    continue;
                }

                AddCatalog(lists, Fold(columns[1]), columns[0], image);
            }
        }

        return lists;
    }

    private static Lists LoadSqlServer()
    {
        var lists = new Lists();
        foreach (var image in new[] { "2017", "default" })
        {
            foreach (var line in File.ReadLines(SpikeCases.Out($"t4-mssql-{image}-catalog.tsv")).Skip(1))
            {
                var columns = line.Split('\t');
                AddCatalog(lists, Fold(columns[1]), columns[0], image);
            }
        }

        var sources = Path.Combine(Path.GetDirectoryName(SpikeCases.Out("x"))!, "..", "sources", "mssql-keywords.tsv");
        foreach (var line in File.ReadLines(sources).Skip(1))
        {
            var columns = line.Split('\t');
            var word = Fold(columns[0]);
            if (!lists.Keywords.TryGetValue(word, out var tags))
            {
                tags = [];
                lists.Keywords[word] = tags;
            }

            _ = tags.Add(columns[1]);
        }

        return lists;
    }

    private static void AddCatalog(Lists lists, string word, string kind, string image)
    {
        if (!lists.Catalog.TryGetValue(word, out var kinds))
        {
            kinds = [];
            lists.Catalog[word] = kinds;
        }

        if (!kinds.TryGetValue(kind, out var images))
        {
            images = [];
            kinds[kind] = images;
        }

        _ = images.Add(image);
    }
}
