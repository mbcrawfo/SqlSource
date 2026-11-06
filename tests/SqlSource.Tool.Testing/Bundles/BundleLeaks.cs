using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Capture;

namespace SqlSource.Tool.Testing.Bundles;

// What of a schema's and a query's text a renamed bundle still holds, found without asking the renaming, by one of two
// searches.  Of is PostgreSQL's: it lexes the sources as PostgreSQL and seeks what it finds in every string of every
// file, so it suits an engine whose bundle keeps none of the user's text in a member the renaming leaves.  Find asks
// the engine's part for its dialect, word rule, fold and rules, seeks only the strings the renaming renames, keeps the
// names of the engine's own rows, and seeks a literal of any length: it is SQL Server's, whose rows of sys keep names
// a source may spell and whose Fixed and Closed members hold the engine's text.
internal static class BundleLeaks
{
    // A shorter literal is too common a substring to be sought.
    private const int ShortestLiteral = 4;

    // The markers that Of reads a name from.
    private const string NameMarker = "-- name:";
    private const string ParamMarker = "-- param:";

    // The capture's list of calls and the member of a call that names it.
    private const string CallsMember = "calls";
    private const string MethodMember = "method";

    // How Find's place of a string of the capture starts, as CaptureRenamer names one.
    private const string Request = "request";
    private const string Answer = "answer";

    // The marker of the query's name, whose value may end in an arrow and the result's shape.
    private const string NameMarkerWord = "name";
    private const string Arrow = "->";

    // The markers whose value starts with a name of the user's: the query's and a declared parameter's.
    private static readonly string[] NamingMarkers = [NameMarkerWord + ":", "param:"];

    // The leaks found, each as "name <name> in <file>" or "literal '<literal>' in <file>"; empty when none.
    // isEngineWord is asked of a name with whether the source quotes it, and of a literal as unquoted.  A name is
    // sought as a whole word, an unquoted one case-insensitively and a quoted one as written, so that "Order" is sought
    // though its fold is a keyword and ORDER BY is not it, and a literal of four characters or more as a substring, or
    // as a whole word when it is one, since a word that holds it, such as the setting standard_conforming_strings, is
    // another word.  After the leak search of the phase 10 spike,
    // docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike.md.
    public static IReadOnlyList<string> Of(
        Bundle renamed,
        IEnumerable<string> sources,
        Func<string, bool, bool> isEngineWord
    )
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var quotedNames = new HashSet<string>(StringComparer.Ordinal);
        var literals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            Originals(source, names, quotedNames, literals);
        }

        _ = names.RemoveWhere(name => isEngineWord(name, false));
        _ = quotedNames.RemoveWhere(name => isEngineWord(name, true));
        _ = literals.RemoveWhere(literal => literal.Length < ShortestLiteral || isEngineWord(literal, false));

        var texts = TextsOf(renamed).ToList();
        var words = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var exactWords = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (file, text) in texts)
        {
            foreach (var word in WordsOf(text))
            {
                _ = words.TryAdd(word, file);
                _ = exactWords.TryAdd(word, file);
            }
        }

        var leaks = new List<string>();
        var sought = names
            .Select(static name => (Name: name, Comparison: StringComparison.OrdinalIgnoreCase))
            .Concat(quotedNames.Select(static name => (Name: name, Comparison: StringComparison.Ordinal)))
            .DistinctBy(static name => name.Name, StringComparer.Ordinal)
            .OrderBy(static name => name.Name, StringComparer.Ordinal);
        foreach (var (name, comparison) in sought)
        {
            var wordsIn = comparison == StringComparison.Ordinal ? exactWords : words;
            var found = IsOneWord(name)
                ? wordsIn.GetValueOrDefault(name)
                : texts.FirstOrDefault(text => text.Text.Contains(name, comparison)).File;
            if (found is not null)
            {
                leaks.Add($"name {name} in {found}");
            }
        }

        foreach (var literal in literals.Order(StringComparer.Ordinal))
        {
            var found = IsOneWord(literal)
                ? exactWords.GetValueOrDefault(literal)
                : texts.FirstOrDefault(text => text.Text.Contains(literal, StringComparison.Ordinal)).File;
            if (found is not null)
            {
                leaks.Add($"literal '{literal}' in {found}");
            }
        }

        return leaks;
    }

    // The names and literals of a text: the words of plain text but a string's prefix, quoted names without the
    // nullability mark that the renaming keeps, parameters, the value of -- name: and what -- param: declares, and the
    // contents of string literals.  Comments hold nothing that the renaming keeps, since it empties them.
    private static void Originals(
        string text,
        HashSet<string> names,
        HashSet<string> quotedNames,
        HashSet<string> literals
    )
    {
        var lexed = SqlLexer.Lex(text, SqlDialectRules.PostgreSql);
        if (lexed.Error is { } error)
        {
            throw new ArgumentException($"A source does not lex: {error.Kind} at {error.Span}.", nameof(text));
        }

        var lexemes = lexed.Lexemes;
        for (var index = 0; index < lexemes.Count; index++)
        {
            var span = lexemes[index].Span;
            var part = text[span.Start..span.End];
            switch (lexemes[index].Kind)
            {
                case SqlLexemeKind.Text:
                    var words = WordsOf(part).Where(static word => char.IsLetter(word[0]) || word[0] == '_').ToList();
                    var prefixed =
                        index + 1 < lexemes.Count
                        && lexemes[index + 1].Kind == SqlLexemeKind.Quoted
                        && words.Count > 0
                        && words[^1].Length == 1
                        && part.TrimEnd('&').EndsWith(words[^1], StringComparison.Ordinal);
                    names.UnionWith(prefixed ? words.Take(words.Count - 1) : words);
                    break;
                case SqlLexemeKind.Quoted when part[0] == '"':
                    var quoted = part[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
                    _ = quotedNames.Add(quoted.Length > 1 && quoted[^1] is '!' or '?' ? quoted[..^1] : quoted);
                    break;
                case SqlLexemeKind.Quoted when part[0] == '$':
                    var tag = part[..(part.IndexOf('$', 1) + 1)];
                    _ = literals.Add(part[tag.Length..^tag.Length]);
                    break;
                case SqlLexemeKind.Quoted:
                    _ = literals.Add(part[1..^1].Replace("''", "'", StringComparison.Ordinal));
                    break;
                case SqlLexemeKind.Parameter:
                    _ = names.Add(part[1..]);
                    break;
                case SqlLexemeKind.LineComment when part.StartsWith(NameMarker, StringComparison.Ordinal):
                    _ = names.Add(part[NameMarker.Length..].Trim());
                    break;
                case SqlLexemeKind.LineComment when part.StartsWith(ParamMarker, StringComparison.Ordinal):
                    Originals(part[ParamMarker.Length..], names, quotedNames, literals);
                    break;
                case SqlLexemeKind.LineComment:
                case SqlLexemeKind.BlockComment:
                case SqlLexemeKind.Hint:
                default:
                    break;
            }
        }
    }

    // Where a name could be left, by file: query.sql and explain-settings.txt whole, and every string of the JSON
    // files, a string that is JSON itself, a plan, read the same way, with the names of the sidecar's entries.  Not
    // sought: the members' names, which are the formats'; the method of each call of the capture, such as open, which
    // is the format's name of the call; the sidecar's header, which is the tool's text; and a string of one character
    // outside a plan, a closed code such as a relkind that a one-letter alias would match.
    private static IEnumerable<(string File, string Text)> TextsOf(Bundle bundle)
    {
        yield return (BundleFiles.Query, bundle.Query);
        yield return (BundleFiles.ExplainSettings, string.Join('\n', bundle.ExplainSettings));
        if (bundle.Info is { } info)
        {
            foreach (var text in StringsOf(JsonNode.Parse(BundleJson.Write(info)), embedded: false))
            {
                yield return (BundleFiles.Info, text);
            }
        }

        foreach (var text in StringsOf(WithoutMethods(JsonNode.Parse(bundle.Capture)), embedded: false))
        {
            yield return (BundleFiles.Capture, text);
        }

        if (bundle.Expected is { } expected && JsonNode.Parse(expected) is JsonObject sidecar)
        {
            foreach (var (member, value) in sidecar)
            {
                if (member is SidecarKeys.Warning or SidecarKeys.SchemaUrl)
                {
                    continue;
                }

                if (member == SidecarKeys.Queries && value is JsonObject entries)
                {
                    foreach (var (name, _) in entries)
                    {
                        yield return (BundleFiles.Expected, name);
                    }
                }

                foreach (var text in StringsOf(value, embedded: false))
                {
                    yield return (BundleFiles.Expected, text);
                }
            }
        }
    }

    private static IEnumerable<string> StringsOf(JsonNode? node, bool embedded)
    {
        switch (node)
        {
            case JsonObject members:
                return members.SelectMany(member => StringsOf(member.Value, embedded));
            case JsonArray items:
                return items.SelectMany(item => StringsOf(item, embedded));
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                var text = value.GetValue<string>();
                if (Embedded(text) is { } document)
                {
                    return StringsOf(document, embedded: true);
                }

                return text.Length == 1 && !embedded ? [] : [text];
            default:
                return [];
        }
    }

    // The capture with no member "method" in its calls.
    private static JsonNode? WithoutMethods(JsonNode? capture)
    {
        if (capture?[CallsMember] is JsonArray calls)
        {
            foreach (var call in calls.OfType<JsonObject>())
            {
                _ = call.Remove(MethodMember);
            }
        }

        return capture;
    }

    // A string that is a JSON array or object, as explain's plan is.
    private static JsonNode? Embedded(string text)
    {
        if (text.Length == 0 || text.TrimStart() is not ['[' or '{', ..])
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsOneWord(string name) => WordsOf(name).FirstOrDefault() == name;

    // The runs of letters, digits, _ and $ that start with a letter, a digit or _.
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

    // Each name and literal of the user's that the renamed bundle holds, as "<word> in <place>".  A word, a quoted
    // name, a parameter or a literal's content of the sources is the user's unless it is the engine's word, a number,
    // or a name kept from a row of the engine's.  It is found, ignoring case, where a string holds it as a whole token
    // by the engine's word rule, or anywhere in a string when it is no single word, as a literal of two words is: a
    // literal of one letter would otherwise be found in every word that holds the letter.  The strings searched are
    // those whose rule is neither Fixed nor Closed, the entry's key among them, and query.sql.
    public static IReadOnlyList<string> Find(
        Bundle renamed,
        IEngineRenaming engine,
        IReadOnlySet<string> keptFromEngineRows,
        params string[] sources
    )
    {
        var kept = keptFromEngineRows.Select(name => engine.Fold(name, quoted: true)).ToHashSet(StringComparer.Ordinal);
        var words = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var phrases = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var (text, kind) in UsersOf(source, engine))
            {
                var quoted = kind == Kind.QuotedName;

                // A text with no token, as the literal ', ', is punctuation and white space alone: it holds nothing of
                // the user's, and as a phrase it would be found in every list of the SQL's own.
                if (
                    !TokensOf(text, engine).Any()
                    || SqlRenamer.IsNumber(text)
                    || engine.IsEngineWord(text, quoted)
                    || kept.Contains(engine.Fold(text, quoted))
                )
                {
                    continue;
                }

                var whole = kind == Kind.Word || TokensOf(text, engine).SequenceEqual([text]);
                _ = (whole ? words : phrases).Add(text);
            }
        }

        var found = new List<string>();
        foreach (var (place, text) in PlacesOf(renamed, engine))
        {
            var tokens = TokensOf(text, engine).ToHashSet(StringComparer.OrdinalIgnoreCase);
            found.AddRange(words.Where(tokens.Contains).Select(word => $"{word} in {place}"));
            found.AddRange(
                phrases
                    .Where(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    .Select(phrase => $"'{phrase}' in {place}")
            );
        }

        return found;
    }

    // Every Name member of a row of the raw capture that the engine's part says is the engine's: the names the
    // renaming keeps, though a source may spell them.
    public static IReadOnlySet<string> EngineRowNames(Bundle raw, IEngineRenaming engine)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in CallsOf(raw))
        {
            foreach (var (part, node) in PartsOf(call))
            {
                JsonStrings.Visit(
                    node,
                    part,
                    found =>
                    {
                        if (
                            engine.RuleOf(call.Method, found.Path) is { Kind: FieldRuleKind.Name }
                            && found.Row is { } row
                            && engine.IsEngineRow(call.Method, found.RowPath, row)
                        )
                        {
                            _ = names.Add(found.Value);
                        }

                        return null;
                    }
                );
            }
        }

        return names;
    }

    // The texts of a source that may be the user's, each with its kind.  Comments are left out, but for the name that
    // starts the value of a marker of a query's name or of a declared parameter.
    private static IEnumerable<(string Text, Kind Kind)> UsersOf(string source, IEngineRenaming engine)
    {
        var lexed = SqlLexer.Lex(source, SqlDialectRules.For(engine.Dialect));
        if (lexed.Error is { } error)
        {
            throw new InvalidOperationException($"A source of the search does not lex: {error.Kind} at {error.Span}.");
        }

        var lexemes = lexed.Lexemes;
        for (var index = 0; index < lexemes.Count; index++)
        {
            var lexeme = lexemes[index];
            var (start, end) = (lexeme.Span.Start, lexeme.Span.End);
            switch (lexeme.Kind)
            {
                case SqlLexemeKind.Text:
                case SqlLexemeKind.Hint:
                    var quoteFollows = index + 1 < lexemes.Count && lexemes[index + 1].Kind == SqlLexemeKind.Quoted;
                    foreach (var word in EngineWordsOf(source, start, end, quoteFollows, engine))
                    {
                        yield return (word, Kind.Word);
                    }

                    break;
                case SqlLexemeKind.Parameter:
                    var name = lexeme.ParameterName;
                    yield return (source.Substring(name.Start, name.Length), Kind.Word);
                    break;
                case SqlLexemeKind.Quoted:
                    if (QuotedOf(source[start..end]) is { } quoted)
                    {
                        yield return quoted;
                    }

                    break;
                case SqlLexemeKind.LineComment:
                    if (MarkedName(source[start..end], engine) is { } marked)
                    {
                        yield return (marked, Kind.Word);
                    }

                    break;
                case SqlLexemeKind.BlockComment:
                    break;
                default:
                    throw new InvalidOperationException($"A lexeme of the kind {lexeme.Kind} is not searched.");
            }
        }
    }

    // The words of plain text by the engine's word rule, a leading @ or # trimmed: a number, an @@ function and a
    // literal's one-letter prefix are none.
    private static IEnumerable<string> EngineWordsOf(
        string text,
        int start,
        int end,
        bool quoteFollows,
        IEngineRenaming engine
    )
    {
        var at = start;
        while (at < end)
        {
            var character = text[at];
            if (char.IsAsciiDigit(character))
            {
                at = SqlRenamer.NumberRunEnd(engine, text, at, end);
            }
            else if (engine.IsWordStart(character))
            {
                var wordStart = at;
                at++;
                while (at < end && engine.IsWordPart(text[at]))
                {
                    at++;
                }

                var word = text[wordStart..at];
                var isPrefix =
                    quoteFollows
                    && word.Length == 1
                    && SqlRenamer.LiteralPrefixes.Contains(word[0], StringComparison.Ordinal)
                    && (at == end || (at + 1 == end && text[at] == '&'));
                if (!isPrefix && !word.StartsWith("@@", StringComparison.Ordinal))
                {
                    yield return word.TrimStart('@', '#');
                }
            }
            else
            {
                at++;
            }
        }
    }

    // A literal's content, or a quoted name's with a nullability mark after its stem left out.  Null for a quoted
    // region of another kind.
    private static (string Text, Kind Kind)? QuotedOf(string quoted)
    {
        var closer = quoted[0] switch
        {
            '\'' => "'",
            '"' => "\"",
            '[' => "]",
            _ => null,
        };
        if (closer is null)
        {
            return null;
        }

        var content = quoted[1..^1].Replace(closer + closer, closer, StringComparison.Ordinal);
        if (quoted[0] == '\'')
        {
            return (content, Kind.Literal);
        }

        return (content.Length > 1 && content[^1] is '!' or '?' ? content[..^1] : content, Kind.QuotedName);
    }

    // The name that starts the value of a marker of the query's name or of a declared parameter, or null.
    private static string? MarkedName(string comment, IEngineRenaming engine)
    {
        var body = comment.TrimStart('-').TrimStart();
        foreach (var marker in NamingMarkers)
        {
            if (body.StartsWith(marker, StringComparison.Ordinal))
            {
                return TokensOf(body[marker.Length..], engine).FirstOrDefault();
            }
        }

        return null;
    }

    // The tokens of a text by the engine's word rule, a leading @ or # trimmed.
    private static IEnumerable<string> TokensOf(string text, IEngineRenaming engine)
    {
        var at = 0;
        while (at < text.Length)
        {
            if (!IsTokenPart(text[at], engine))
            {
                at++;
                continue;
            }

            var tokenStart = at;
            while (at < text.Length && IsTokenPart(text[at], engine))
            {
                at++;
            }

            var token = text[tokenStart..at].TrimStart('@', '#');
            if (token.Length > 0)
            {
                yield return token;
            }
        }
    }

    private static bool IsTokenPart(char character, IEngineRenaming engine) =>
        char.IsAsciiDigit(character) || engine.IsWordStart(character) || engine.IsWordPart(character);

    // Every string of the renamed bundle that the renaming renames, with its place as a refusal names it: query.sql,
    // the capture's strings whose rule is neither Fixed nor Closed, and the entry's, its key among them.
    private static IEnumerable<(string Place, string Text)> PlacesOf(Bundle renamed, IEngineRenaming engine)
    {
        yield return (BundleFiles.Query, WithoutMarkerWords(renamed.Query));
        var strings = new List<(string Place, string Text)>();
        foreach (var (call, index) in CallsOf(renamed).Select(static (call, index) => (call, index)))
        {
            var place = $"{BundleFiles.Capture} call {index.ToString(CultureInfo.InvariantCulture)} '{call.Method}'";
            foreach (var (part, node) in PartsOf(call))
            {
                JsonStrings.Visit(
                    node,
                    part,
                    found =>
                    {
                        if (IsRenamed(engine.RuleOf(call.Method, found.Path)))
                        {
                            strings.Add((place + " " + found.Path, found.Value));
                        }

                        return null;
                    }
                );
            }
        }

        if (renamed.Expected is { } expected)
        {
            var (key, entry) = EntryOf(expected);
            strings.Add((BundleFiles.Expected + " key", key));
            JsonStrings.Visit(
                entry,
                string.Empty,
                found =>
                {
                    if (IsRenamed(EntryRenamer.RuleOf(engine, found.Path)))
                    {
                        strings.Add((BundleFiles.Expected + " " + found.Path, found.Value));
                    }

                    return null;
                }
            );
        }

        foreach (var found in strings)
        {
            yield return found;
        }
    }

    // query.sql with what is the tool's in each marker's line blanked: the marker's own word, as "name" of
    // "-- name: q_1", and the result shape after the arrow of a query's name.
    private static string WithoutMarkerWords(string query)
    {
        var lines = query.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var at = line.Length - line.TrimStart().Length;
            if (string.CompareOrdinal(line, at, "--", 0, 2) != 0)
            {
                continue;
            }

            at += 2;
            while (at < line.Length && char.IsWhiteSpace(line[at]))
            {
                at++;
            }

            var wordEnd = at;
            while (wordEnd < line.Length && char.IsAsciiLetter(line[wordEnd]))
            {
                wordEnd++;
            }

            if (wordEnd == at || wordEnd >= line.Length || line[wordEnd] != ':')
            {
                continue;
            }

            var arrow =
                line[at..wordEnd] == NameMarkerWord ? line.IndexOf(Arrow, wordEnd, StringComparison.Ordinal) : -1;
            var blanked = line[..at] + new string(' ', wordEnd - at) + line[wordEnd..];
            lines[index] = arrow < 0 ? blanked : blanked[..arrow];
        }

        return string.Join('\n', lines);
    }

    // A string with no rule is searched too: a renamed bundle never holds one, so it is a member the search must see.
    private static bool IsRenamed(FieldRule? rule) =>
        rule is not { Kind: FieldRuleKind.Fixed or FieldRuleKind.Closed or FieldRuleKind.Drop };

    private static IReadOnlyList<CaptureCall> CallsOf(Bundle bundle)
    {
        var read = CaptureJson.Read(bundle.Capture);
        return read.Value is { } capture
            ? capture.Calls
            : throw new InvalidOperationException($"The capture of the bundle cannot be read.  {read.Failure}");
    }

    private static IEnumerable<(string Part, JsonNode? Node)> PartsOf(CaptureCall call)
    {
        yield return (Request, JsonNode.Parse(call.Request.GetRawText()));
        yield return (Answer, JsonNode.Parse(call.Answer.GetRawText()));
    }

    // The key and the object of the one entry of an expected.json.
    private static (string Key, JsonObject Entry) EntryOf(string expected)
    {
        var queries = JsonNode.Parse(expected)?[SidecarKeys.Queries] as JsonObject;
        if (queries is not { Count: 1 })
        {
            throw new InvalidOperationException("The expected.json is no sidecar of one entry.");
        }

        var (key, entry) = queries.Single();
        return (key, entry as JsonObject ?? throw new InvalidOperationException("The entry is no JSON object."));
    }

    // What a text of a source is: a word or a parameter's name, a quoted name or a literal's content.
    private enum Kind
    {
        Word,
        QuotedName,
        Literal,
    }
}
