using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SqlSource.Parsing;
using SqlSource.Parsing.Quoting;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing.Fixtures;

#pragma warning disable CA1308 // PostgreSQL folds a name to lower case, and the lists are lower case.

namespace SqlSource.Tool.Tests.Spike10;

// The rules the crude renamer of finding 4 adds to the design's steps.  Each is a rule the replay showed the renaming
// has to know; None is the design's steps as written.
[Flags]
internal enum CrudeRules
{
    None = 0,

    // A copy of a reused parameter (SQL Server's @name_1) is renamed through its parameter: <renamed>_<n>.
    CopiesThroughTheirParameter = 1,

    // A quoted name spelled as digits alone ([1]) is the tool's own alias of an operand or a declared parameter, and
    // kept.
    DigitNamesKept = 2,

    // A request whose list the pipeline orders by name (relations, partitions: schema, then name, by ordinal) is put
    // in that order again after its names are renamed.
    RelationsInNameOrder = 4,

    // The planner's Subplan Name "CTE <name>" keeps the word CTE and renames the name as one raw name.
    CteSubplanName = 8,

    // A plan's alias <alias>_<n>, which the planner makes for a child of a partitioned table or an inheritance parent,
    // is renamed through its alias: <renamed>_<n>.
    ChildAliasesThroughTheirAlias = 16,

    // A name ending in the generator's nullability mark, ! or ?, keeps the mark: <renamed stem><mark>.
    NullabilityMarksKept = 32,

    All =
        CopiesThroughTheirParameter
        | DigitNamesKept
        | RelationsInNameOrder
        | CteSubplanName
        | ChildAliasesThroughTheirAlias
        | NullabilityMarksKept,
}

// What one renaming gave: the copy, the mapping (folded original -> replacement; a literal's key is 'content'), and
// what the renamer noticed on the way (fields it had no rule for, strings it could not read).
internal sealed record CrudeRenaming(
    FixtureCase Renamed,
    IReadOnlyDictionary<string, string> Mapping,
    IReadOnlyList<string> Notes,
    Func<string, bool> IsKept
);

// One name of the mapping: its replacement, the class it was given, and where it was first found.
internal sealed record CrudeName(string Replacement, char Class, string Original, string Source);

// The mapping of one renaming: one spelling, one name (step 4), a keyed hash of the class and the folded spelling
// (step 3), with a key of 32 zero bytes so that runs can be compared.  A name kept as the engine's maps to itself.
internal sealed class CrudeMapping
{
    private static readonly byte[] Key = new byte[32];

    private readonly Dictionary<string, CrudeName> _names = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, CrudeName> _literals = [with(StringComparer.Ordinal)];
    private readonly HashSet<string> _replacements = [with(StringComparer.Ordinal)];

    public IEnumerable<KeyValuePair<string, CrudeName>> Names => _names;

    public IEnumerable<KeyValuePair<string, CrudeName>> Literals => _literals;

    public bool TryGet(string key, out CrudeName name) => _names.TryGetValue(key, out name!);

    public string Name(string key, char cls, string original, string source)
    {
        if (_names.TryGetValue(key, out var found))
        {
            return found.Replacement;
        }

        var replacement = Make(cls, key);
        _names[key] = new CrudeName(replacement, cls, original, source);
        return replacement;
    }

    // A name the catalog row says is the engine's: it maps to itself, unless it was already renamed.
    public void Keep(string key, string original, string source) =>
        _ = _names.TryAdd(key, new CrudeName(original, '=', original, source));

    public string Literal(string content)
    {
        if (_literals.TryGetValue(content, out var found))
        {
            return found.Replacement;
        }

        var replacement = Make('l', content);
        _literals[content] = new CrudeName(replacement, 'l', content, "literal");
        return replacement;
    }

    private string Make(char cls, string key)
    {
        var hash = Convert.ToHexStringLower(
            HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(cls.ToString() + ":" + key))
        );
        for (var length = 8; length <= hash.Length; length += 2)
        {
            var candidate = cls.ToString() + "_" + hash[..length];
            if (_replacements.Add(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("The hash of a name collides at every length.");
    }
}

// The lists the renamer reads from earlier findings under SPIKE10_OUT: task 4's vocabulary and task 2's members that
// the pipeline does not read.
internal sealed class CrudeLists
{
    private static readonly Lazy<CrudeLists> Loaded = new(Load);

    // The non-reserved SQL Server words task 4 found in no list (finding 3), kept as the engine's.
    private static readonly string[] SqlServerExtraWords =
    [
        "apply",
        "deleted",
        "inserted",
        "matched",
        "offset",
        "preceding",
        "unbounded",
        "ties",
        "recompile",
        "system_time",
        "absent",
        "openjson",
    ];

    // PostgreSQL's system columns, which task 4 recommends keeping with the vocabulary.
    private static readonly string[] PostgresSystemColumns = ["ctid", "xmin", "xmax", "cmin", "cmax", "tableoid"];

    public static CrudeLists Instance => Loaded.Value;

    public HashSet<string> PostgresWords { get; } = [with(StringComparer.Ordinal)];

    public HashSet<string> SqlServerWords { get; } = [with(StringComparer.Ordinal)];

    // method + "." + path of a member that task 2 found not read, by engine.
    public HashSet<string> NotRead { get; } = [with(StringComparer.Ordinal)];

    public HashSet<string> PlanKeysNotRead { get; } = [with(StringComparer.Ordinal)];

    private static CrudeLists Load()
    {
        var lists = new CrudeLists();
        var results = Path.GetDirectoryName(SpikeCases.Out("t5-probe"))!;
        var sources = Path.Combine(Path.GetDirectoryName(results)!, "sources");
        foreach (var file in new[] { "t4-pg-14-keywords.tsv", "t4-pg-18-keywords.tsv" })
        {
            foreach (var row in Rows(Path.Combine(results, file)))
            {
                _ = lists.PostgresWords.Add(row[0].ToLowerInvariant());
            }
        }

        foreach (var file in new[] { "t4-pg-14-catalog.tsv", "t4-pg-18-catalog.tsv" })
        {
            foreach (var row in Rows(Path.Combine(results, file)).Where(static row => row[0] != "operator"))
            {
                _ = lists.PostgresWords.Add(row[1].ToLowerInvariant());
            }
        }

        lists.PostgresWords.UnionWith(PostgresSystemColumns);

        foreach (var row in Rows(Path.Combine(sources, "mssql-keywords.tsv")))
        {
            _ = lists.SqlServerWords.Add(row[0].ToLowerInvariant());
        }

        foreach (var file in new[] { "t4-mssql-2017-catalog.tsv", "t4-mssql-default-catalog.tsv" })
        {
            foreach (var row in Rows(Path.Combine(results, file)))
            {
                _ = lists.SqlServerWords.Add(row[1].ToLowerInvariant());
            }
        }

        lists.SqlServerWords.UnionWith(SqlServerExtraWords);

        foreach (var engine in new[] { "postgres", "mssql" })
        {
            foreach (var row in Rows(Path.Combine(results, $"t2-members-{engine}.tsv")))
            {
                if (row[^1] == "not read")
                {
                    _ = lists.NotRead.Add($"{engine}:{row[0]}.{row[2]}");
                }
            }
        }

        foreach (var row in Rows(Path.Combine(results, "t2-plan-keys.tsv")))
        {
            if (row[^1] == "not read" && !row[0].StartsWith('('))
            {
                _ = lists.PlanKeysNotRead.Add(row[0]);
            }
        }

        return lists;
    }

    private static IEnumerable<string[]> Rows(string path) =>
        File.ReadAllLines(path).Skip(1).Where(static line => line.Length > 0).Select(static line => line.Split('\t'));
}

// Finding 4 of the phase 10.0 spike: a crude renaming of a case, by the design's steps 1 to 11, so that the copy can
// be replayed.  Throwaway code: it learns where a renamed request differs from one made from the renamed query.
internal sealed partial class CrudeRenamer
{
    // How a rule of the field tables renames a string.
    internal enum Rule
    {
        Keep,
        Sql,
        TypeSpelling,
        Label,
        Parameter,
        Plan,
        Setting,
        Schema,
        Table,
        Column,
        TypeName,
        Function,
        Database,
    }

    private static readonly Regex Marker = MarkerPattern();

    private static readonly Regex Copy = CopyPattern();

    private readonly string _engine;
    private readonly CrudeRules _rules;
    private readonly CrudeMapping _mapping = new();
    private readonly List<string> _notes = [];
    private readonly HashSet<string> _queryParameters = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly HashSet<string> _words;

    private CrudeRenamer(string engine, CrudeRules rules)
    {
        _engine = engine;
        _rules = rules;
        _words = IsPostgres ? CrudeLists.Instance.PostgresWords : CrudeLists.Instance.SqlServerWords;
    }

    private bool IsPostgres =>
        _engine switch
        {
            "postgres" => true,
            "mssql" => false,
            _ => throw new InvalidOperationException($"No renamer for the engine {_engine}."),
        };

    private SqlDialectRules Rules => IsPostgres ? SqlDialectRules.PostgreSql : SqlDialectRules.SqlServer;

    // The interface of the brief: the case renamed with every rule the spike found, and its mapping.
    public static (FixtureCase Renamed, IReadOnlyDictionary<string, string> Mapping) Rename(FixtureCase fixture)
    {
        var renamed = Rename(fixture, CrudeRules.All, "t5-" + fixture.Name);
        return (renamed.Renamed, renamed.Mapping);
    }

    public static CrudeRenaming Rename(FixtureCase fixture, CrudeRules rules, string folderName)
    {
        var renamer = new CrudeRenamer(fixture.Engine, rules);
        var capture = (JsonObject)JsonNode.Parse(fixture.Capture)!;
        var calls = ((JsonArray)capture["calls"]!).OfType<JsonObject>().ToList();

        // Step 1: drop what the pipeline does not read.
        foreach (var call in calls)
        {
            renamer.Drop(call);
        }

        // Step 2, in order: the catalog fields of the answers, the plans' names, then the words of the query.
        foreach (var call in calls)
        {
            renamer.Walk(call["answer"], Method(call), "answer", register: true, engineRow: false);
        }

        foreach (var call in calls)
        {
            renamer.Walk(call["request"], Method(call), "request", register: true, engineRow: false);
        }

        if (renamer.IsPostgres)
        {
            foreach (var call in calls.Where(static call => Method(call) == "explain"))
            {
                renamer.RegisterPlanNames(call);
            }
        }

        renamer.FindQueryParameters(fixture.Query);
        var query = renamer.Sql(fixture.Query, 'a', queryFile: true, "query.sql");

        foreach (var call in calls)
        {
            renamer.Walk(call["request"], Method(call), "request", register: false, engineRow: false);
            renamer.Walk(call["answer"], Method(call), "answer", register: false, engineRow: false);
        }

        if ((rules & CrudeRules.RelationsInNameOrder) != 0)
        {
            foreach (var call in calls.Where(static call => Method(call) is "relations" or "partitions"))
            {
                SortRelations(call);
            }
        }

        var renamedCapture = new DescribeCapture(fixture.Engine);
        foreach (var call in calls)
        {
            renamedCapture.Add(
                new CaptureCall(
                    Method(call),
                    JsonSerializer.SerializeToElement(call["request"]),
                    JsonSerializer.SerializeToElement(call["answer"])
                )
            );
        }

        var expected = renamer.Entry(fixture.Expected);
        var copy = SpikeCases.Copy(fixture, folderName, query, CaptureJson.Write(renamedCapture));
        foreach (var file in Directory.GetFiles(copy.Folder))
        {
            var name = Path.GetFileName(file);
            if (
                name
                is not (
                    FixtureCase.QueryFile
                    or FixtureCase.CaptureFile
                    or FixtureCase.ExpectedFile
                    or FixtureCase.ExplainSettingsFile
                )
            )
            {
                File.Delete(file);
            }
        }

        File.WriteAllText(Path.Combine(copy.Folder, FixtureCase.ExpectedFile), expected);
        renamer.WriteMapping(copy.Folder + ".mapping.tsv");
        return new CrudeRenaming(copy, renamer.MappingDictionary(), renamer._notes, renamer.IsKeptOriginal);
    }

    // Whether a member of a call holds the tool's or the engine's fixed text, which no renaming touches.
    public static bool IsKeptField(string engine, string path) =>
        path.EndsWith(".method", StringComparison.Ordinal)
        || (
            (engine == "postgres" ? PostgresFields : SqlServerFields).TryGetValue(path, out var rule)
            && rule == Rule.Keep
        );

    private static void SortRelations(JsonObject call)
    {
        if (call["request"]?["relations"] is not JsonArray relations)
        {
            return;
        }

        var sorted = relations
            .OfType<JsonObject>()
            .OrderBy(static relation => relation["schema"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(static relation => relation["name"]!.GetValue<string>(), StringComparer.Ordinal)
            .Select(static relation => relation.DeepClone())
            .ToList();
        relations.Clear();
        foreach (var relation in sorted)
        {
            relations.Add(relation);
        }
    }

    private static string Method(JsonObject call) => call["method"]!.GetValue<string>();

    // Whether an original name or literal content is left as it is by this renaming: a word of the engine, a name the
    // planner makes, or a name a catalog row says is the engine's.
    private bool IsKeptOriginal(string spelling) =>
        Keeps(spelling)
        || (_mapping.TryGet(Fold(spelling, quoted: true), out var name) && name.Class == '=')
        || (_mapping.TryGet(Fold(spelling, quoted: false), out var folded) && folded.Class == '=');

    private Dictionary<string, string> MappingDictionary()
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, name) in _mapping.Names.Where(static pair => pair.Value.Class != '='))
        {
            mapping[key] = name.Replacement;
        }

        foreach (var (key, name) in _mapping.Literals)
        {
            mapping["'" + key + "'"] = name.Replacement;
        }

        return mapping;
    }

    private void WriteMapping(string path)
    {
        var text = new StringBuilder("class\toriginal\treplacement\tsource\n");
        foreach (var (_, name) in _mapping.Names.Concat(_mapping.Literals))
        {
            _ = text.Append(
                CultureInfo.InvariantCulture,
                $"{name.Class}\t{name.Original}\t{name.Replacement}\t{name.Source}\n"
            );
        }

        File.WriteAllText(path, text.ToString());
    }

    private void Note(string note)
    {
        if (!_notes.Contains(note))
        {
            _notes.Add(note);
        }
    }

    // ---- Names: what is kept, how a spelling is folded, and the one replacement of a spelling.

    // Step 2: a keyword, a word of the engine's lists, the engine's own schemas, a name the planner makes.  A user's
    // name spelled as one of them stays verbatim (owner's decision).
    private bool Keeps(string spelling)
    {
        if (spelling.Length == 0)
        {
            return true;
        }

        var lower = spelling.ToLowerInvariant();
        if (_words.Contains(lower) || EngineNames.Contains(lower))
        {
            return true;
        }

        if ((_rules & CrudeRules.DigitNamesKept) != 0 && spelling.All(char.IsAsciiDigit))
        {
            return true;
        }

        return IsPostgres && PlannerName(spelling);
    }

    private HashSet<string> EngineNames => IsPostgres ? PostgresEngineNames : SqlServerEngineNames;

    // Step 5: PostgreSQL folds an unquoted name to lower case; SQL Server compares names ignoring case.
    private string Fold(string spelling, bool quoted) => IsPostgres && quoted ? spelling : spelling.ToLowerInvariant();

    private string NameOf(string spelling, bool quoted, char cls, string source)
    {
        if (Keeps(spelling))
        {
            return spelling;
        }

        if ((_rules & CrudeRules.NullabilityMarksKept) != 0 && spelling.Length > 1 && spelling[^1] is '!' or '?')
        {
            Note("a name's nullability mark was kept");
            return NameOf(spelling[..^1], quoted, cls, source) + spelling[^1];
        }

        var key = Fold(spelling, quoted);
        return _mapping.Name(key, cls, spelling, source);
    }

    private void KeepName(string spelling, bool quoted, string source)
    {
        if (!Keeps(spelling))
        {
            _mapping.Keep(Fold(spelling, quoted), spelling, source);
        }
    }

    // A parameter's name, and with CopiesThroughTheirParameter a copy's name through its parameter.
    private string ParameterName(string name, string source)
    {
        if (
            (_rules & CrudeRules.CopiesThroughTheirParameter) != 0
            && !_queryParameters.Contains(name)
            && Copy.Match(name) is { Success: true } copy
            && _queryParameters.Contains(copy.Groups["name"].Value)
        )
        {
            return NameOf(copy.Groups["name"].Value, quoted: false, 'p', source)
                + copy.Groups["separator"].Value
                + copy.Groups["number"].Value;
        }

        return NameOf(name, quoted: false, 'p', source);
    }

    private void FindQueryParameters(string query)
    {
        var lexed = SqlLexer.Lex(query, Rules);
        foreach (var lexeme in lexed.Lexemes.Where(static lexeme => lexeme.Kind == SqlLexemeKind.Parameter))
        {
            _ = _queryParameters.Add(query.Substring(lexeme.ParameterName.Start, lexeme.ParameterName.Length));
        }
    }

    // ---- SQL: the generator's lexer, the engine's word rule (step 8), literals (step 6), comments (step 7).

    private bool IsWordStart(char character) =>
        IsPostgres
            ? PostgresIdentifiers.IsWordStart(character)
            : QuoteReader.IsNameCharacterOfSqlServer(character) && !char.IsDigit(character);

    private bool IsWordPart(char character) =>
        IsPostgres ? PostgresIdentifiers.IsWordPart(character) : QuoteReader.IsNameCharacterOfSqlServer(character);

    // The SQL renamed lexeme by lexeme.  A new word is of the class given, or a function before "(".
    private string Sql(string sql, char newClass, bool queryFile, string source)
    {
        var lexed = SqlLexer.Lex(sql, Rules);
        if (lexed.Error is not null)
        {
            Note($"refused: {source} does not lex");
            return sql;
        }

        var lexemes = lexed.Lexemes;
        var into = new StringBuilder(sql.Length);
        for (var index = 0; index < lexemes.Count; index++)
        {
            var lexeme = lexemes[index];
            var start = lexeme.Span.Start;
            var end = lexeme.Span.End;
            switch (lexeme.Kind)
            {
                case SqlLexemeKind.Text:
                    var quoteFollows = index + 1 < lexemes.Count && lexemes[index + 1].Kind == SqlLexemeKind.Quoted;
                    Text(sql, start, end, quoteFollows, newClass, into, source);
                    break;
                case SqlLexemeKind.Quoted:
                    _ = into.Append(Quoted(sql[start..end], source));
                    break;
                case SqlLexemeKind.Parameter:
                    var name = lexeme.ParameterName;
                    _ = into.Append(sql, start, name.Start - start)
                        .Append(ParameterName(sql.Substring(name.Start, name.Length), source));
                    break;
                case SqlLexemeKind.LineComment:
                    _ = into.Append(queryFile ? MarkerOrEmpty(sql[start..end]) : "--");
                    break;
                case SqlLexemeKind.BlockComment:
                    _ = into.Append("/*").Append('\n', sql[start..end].Count(static c => c == '\n')).Append("*/");
                    break;
                case SqlLexemeKind.Hint:
                    _ = into.Append(sql, start, 3);
                    Text(sql, start + 3, end - 2, quoteFollows: false, newClass, into, source);
                    _ = into.Append("*/");
                    break;
                default:
                    throw new InvalidOperationException($"No rule for the lexeme {lexeme.Kind}.");
            }
        }

        return into.ToString();
    }

    private void Text(
        string sql,
        int start,
        int end,
        bool quoteFollows,
        char newClass,
        StringBuilder into,
        string source
    )
    {
        var at = start;
        while (at < end)
        {
            var character = sql[at];
            if (IsWordStart(character))
            {
                var wordStart = at;
                while (at < end && IsWordPart(sql[at]))
                {
                    at++;
                }

                var word = sql[wordStart..at];
                var rest = sql.AsSpan(at, end - at);
                if (
                    quoteFollows
                    && word.Length == 1
                    && "EeNnBbXxUu".Contains(word[0], StringComparison.Ordinal)
                    && (rest.IsEmpty || rest is "&")
                )
                {
                    _ = into.Append(word);
                    continue;
                }

                var after = at;
                while (after < end && char.IsWhiteSpace(sql[after]))
                {
                    after++;
                }

                var cls = after < end && sql[after] == '(' ? 'f' : newClass;
                _ = into.Append(Word(word, cls, source));
            }
            else if (char.IsAsciiDigit(character))
            {
                var numberStart = at;
                while (at < end && (IsWordPart(sql[at]) || sql[at] == '.'))
                {
                    at++;
                }

                _ = into.Append(sql, numberStart, at - numberStart);
            }
            else
            {
                _ = into.Append(character);
                at++;
            }
        }
    }

    // A word of plain text.  SQL Server's @@ function and a #temporary name keep their prefix.
    private string Word(string word, char cls, string source)
    {
        if (word.StartsWith("@@", StringComparison.Ordinal))
        {
            return word;
        }

        var prefix = 0;
        while (prefix < word.Length && word[prefix] is '@' or '#')
        {
            prefix++;
        }

        return prefix == word.Length
            ? word
            : string.Concat(word.AsSpan(0, prefix), NameOf(word[prefix..], quoted: false, cls, source));
    }

    // A quoted region: a literal keeps its form with 'l_...' inside, a quoted name its delimiters with the name inside.
    private string Quoted(string quoted, string source)
    {
        var opener = quoted[0];
        if (opener == '\'')
        {
            return "'" + _mapping.Literal(quoted[1..^1].Replace("''", "'", StringComparison.Ordinal)) + "'";
        }

        if (IsPostgres && opener == '$')
        {
            var tag = quoted[..(quoted.IndexOf('$', 1) + 1)];
            return tag + _mapping.Literal(quoted[tag.Length..^tag.Length]) + tag;
        }

        var (inner, close) = opener switch
        {
            '"' => (quoted[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal), "\""),
            '[' => (quoted[1..^1].Replace("]]", "]", StringComparison.Ordinal), "]"),
            _ => throw new InvalidOperationException($"No rule for a quoted region opened by {opener}."),
        };
        return Keeps(inner) ? quoted : opener + NameOf(inner, quoted: true, 'a', source) + close;
    }

    // A marker of query.sql keeps its kind, with its names renamed; any other comment is emptied.
    private string MarkerOrEmpty(string comment)
    {
        var marker = Marker.Match(comment);
        if (!marker.Success)
        {
            return "--";
        }

        var kind = marker.Groups["kind"].Value;
        var value = marker.Groups["value"].Value;
        return kind switch
        {
            "name" => $"-- name: {NameOf(value.Trim(), quoted: false, 'q', "-- name:")}",
            "param" => "-- param:" + Sql(value, 'y', queryFile: false, "-- param:"),
            _ => KeptMarker(comment, kind),
        };
    }

    private string KeptMarker(string comment, string kind)
    {
        Note($"marker kept as it is: {kind}");
        return comment;
    }

    // ---- JSON: each string by the rule of its field.

    private void Drop(JsonObject call)
    {
        foreach (var side in new[] { "request", "answer" })
        {
            Drop(call[side], $"{_engine}:{Method(call)}.{side}");
        }
    }

    private static void Drop(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToArray())
                {
                    var child = path + "." + name;
                    if (CrudeLists.Instance.NotRead.Contains(child))
                    {
                        _ = obj.Remove(name);
                    }
                    else
                    {
                        Drop(value, child);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Drop(item, path + "[]");
                }

                break;
            default:
                break;
        }
    }

    // Walks a request or an answer.  Registering, only the catalog fields of names are read, in order; renaming, every
    // string is replaced by the rule of its field.  A row the engine says is its own keeps its names.
    private void Walk(JsonNode? node, string method, string path, bool register, bool engineRow)
    {
        switch (node)
        {
            case JsonObject obj:
                if (
                    path.EndsWith(".error", StringComparison.Ordinal)
                    || path.EndsWith(".driverError", StringComparison.Ordinal)
                    || path.EndsWith(".errors[]", StringComparison.Ordinal)
                )
                {
                    if (!register)
                    {
                        Error(obj, path);
                    }

                    return;
                }

                var engine = engineRow || IsEngineRow(obj);
                foreach (var (name, value) in obj.ToArray())
                {
                    if (value is JsonValue leaf && leaf.GetValueKind() == JsonValueKind.String)
                    {
                        var renamed = Field(method, path + "." + name, leaf.GetValue<string>(), obj, register, engine);
                        if (!register)
                        {
                            obj[name] = renamed;
                        }
                    }
                    else
                    {
                        Walk(value, method, path + "." + name, register, engine);
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue leaf && leaf.GetValueKind() == JsonValueKind.String)
                    {
                        var renamed = Field(method, path + "[]", leaf.GetValue<string>(), null, register, engineRow);
                        if (!register)
                        {
                            array[index] = renamed;
                        }
                    }
                    else
                    {
                        Walk(array[index], method, path + "[]", register, engineRow);
                    }
                }

                break;
            default:
                break;
        }
    }

    private string Field(string method, string path, string value, JsonObject? row, bool register, bool engineRow)
    {
        var where = method + "." + path;
        if (!FieldRules.TryGetValue(where, out var rule))
        {
            if (!register)
            {
                Note($"no rule for {where}: words renamed");
            }

            return register ? value : Sql(value, 'a', queryFile: false, where);
        }

        var cls = rule switch
        {
            Rule.Schema => 's',
            Rule.Table => 't',
            Rule.Column => 'c',
            Rule.TypeName => 'y',
            Rule.Function => 'f',
            Rule.Database => 'd',
            Rule.Keep or Rule.Sql or Rule.TypeSpelling or Rule.Label or Rule.Parameter or Rule.Plan or Rule.Setting =>
                '\0',
            _ => '\0',
        };
        if (register)
        {
            if (cls != '\0')
            {
                if (engineRow)
                {
                    KeepName(value, quoted: true, where);
                }
                else
                {
                    _ = NameOf(value, quoted: true, cls, where);
                }
            }

            return value;
        }

        return rule switch
        {
            Rule.Keep => value,
            Rule.Sql => Sql(value, 'a', queryFile: false, where),
            Rule.TypeSpelling => Sql(value, 'y', queryFile: false, where),
            Rule.Label => _mapping.Literal(value),
            Rule.Parameter => value.StartsWith('@')
                ? "@" + ParameterName(value[1..], where)
                : ParameterName(value, where),
            Rule.Plan => Plan(value),
            Rule.Setting => Setting(row, value),
            Rule.Schema or Rule.Table or Rule.Column or Rule.TypeName or Rule.Function or Rule.Database => engineRow
            && !_mapping.TryGet(Fold(value, quoted: true), out _)
                ? value
                : NameOf(value, quoted: true, cls, where),
            _ => engineRow && !_mapping.TryGet(Fold(value, quoted: true), out _)
                ? value
                : NameOf(value, quoted: true, cls, where),
        };
    }

    private Dictionary<string, Rule> FieldRules => IsPostgres ? PostgresFields : SqlServerFields;

    private bool IsEngineRow(JsonObject row) => IsPostgres ? IsPostgresEngineRow(row) : IsSqlServerEngineRow(row);

    private void Error(JsonObject error, string path)
    {
        if (IsPostgres)
        {
            PostgresError(error, path);
        }
        else
        {
            SqlServerError(error);
        }
    }

    // ---- The entry (step 11): renamed by the same mapping, by field; its hash is left as it is.

    private string Entry(string expected)
    {
        var root = (JsonObject)JsonNode.Parse(expected)!;
        var queries = (JsonObject)root["queries"]!;
        var renamed = new JsonObject();
        foreach (var (name, value) in queries.ToArray())
        {
            _ = queries.Remove(name);
            var entry = (JsonObject)value!;
            EntryNode(entry, "");
            renamed[NameOf(name, quoted: false, 'q', "expected.json")] = entry;
        }

        root["queries"] = renamed;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private void EntryNode(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToArray())
                {
                    var child = path + "." + name;
                    if (value is JsonValue leaf && leaf.GetValueKind() == JsonValueKind.String)
                    {
                        obj[name] = EntryString(child, leaf.GetValue<string>());
                    }
                    else
                    {
                        EntryNode(value, child);
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue leaf && leaf.GetValueKind() == JsonValueKind.String)
                    {
                        array[index] = EntryString(path + "[]", leaf.GetValue<string>());
                    }
                    else
                    {
                        EntryNode(array[index], path + "[]");
                    }
                }

                break;
            default:
                break;
        }
    }

    private string EntryString(string path, string value)
    {
        const string Source = "expected.json";
        var last = path[(path.LastIndexOf('.') + 1)..];
        var inType = path.Contains(".type", StringComparison.Ordinal);
        var inUserType = path.Contains(".userType.", StringComparison.Ordinal);
        if (path is ".columns[].name" or ".columns[].origin.column")
        {
            return EntryName(value, 'c', Source);
        }

        if (path == ".parameters[].name")
        {
            return ParameterName(value, Source);
        }

        if (path is ".columns[].origin.table" or ".matchesTable.table")
        {
            return EntryName(value, 't', Source);
        }

        if (path is ".columns[].origin.schema" or ".matchesTable.schema")
        {
            return EntryName(value, 's', Source);
        }

        if (!inType)
        {
            return value;
        }

        return last switch
        {
            "labels[]" => _mapping.Literal(value),
            "assemblyQualifiedName" => value,
            "schema" => EntryName(value, 's', Source),
            "internalName" => EntryName(value, 'y', Source),
            "name" when inUserType => EntryName(value, 'y', Source),
            "name" or "bindName" => Sql(value, 'y', queryFile: false, Source),
            _ => value,
        };
    }

    private string EntryName(string value, char cls, string source)
    {
        var stem =
            (_rules & CrudeRules.NullabilityMarksKept) != 0 && value.Length > 1 && value[^1] is '!' or '?'
                ? value[..^1]
                : value;
        if (!Keeps(value) && !Keeps(stem) && !_mapping.TryGet(Fold(stem, quoted: true), out _))
        {
            Note($"a name found only in the entry: {cls}");
        }

        return _mapping.TryGet(Fold(value, quoted: true), out var kept) && kept.Class == '='
            ? value
            : NameOf(value, quoted: true, cls, source);
    }

    [GeneratedRegex(@"^--\s*(?<kind>[a-z-]+):(?<value>.*)$", RegexOptions.Singleline)]
    private static partial Regex MarkerPattern();

    [GeneratedRegex(@"^(?<name>.+?)(?<separator>_+)(?<number>\d+)$")]
    private static partial Regex CopyPattern();
}
