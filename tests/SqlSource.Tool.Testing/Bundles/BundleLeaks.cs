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

// What a renamed bundle may still hold of the user's, searched for with nothing of the renaming's own: the names and
// literals of the SQL the user wrote, the schema and the query, against every string the renamed bundle holds that the
// renaming renames, and its query.sql.  An engine's part is asked only for its word rule, its words, its fold and its
// rules, so the search is the same for every engine.
internal static class BundleLeaks
{
    // How the renaming's place of a string of the capture starts, as CaptureRenamer names one.
    private const string Request = "request";
    private const string Answer = "answer";

    // The marker of the query's name, whose value may end in an arrow and the result's shape.
    private const string NameMarker = "name";
    private const string Arrow = "->";

    // The markers whose value starts with a name of the user's: the query's and a declared parameter's.
    private static readonly string[] NamingMarkers = [NameMarker + ":", "param:"];

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
                if (
                    text.Length == 0
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

    // The provenance of the one entry of an expected.json, which a renaming keeps: its plan and its table match, each
    // parameter's typeSource and each column's nullableSource, each with its path.  A null is "null".
    public static IReadOnlyList<(string Path, string Value)> ProvenanceOf(string expected)
    {
        var entry = EntryOf(expected).Entry;
        var provenance = new List<(string Path, string Value)>
        {
            (SidecarKeys.Plan, ValueOf(entry[SidecarKeys.Plan])),
            (SidecarKeys.TableMatch, ValueOf(entry[SidecarKeys.TableMatch])),
        };
        provenance.AddRange(OfElements(entry, SidecarKeys.Parameters, SidecarKeys.TypeSource));
        provenance.AddRange(OfElements(entry, SidecarKeys.Columns, SidecarKeys.NullableSource));
        return provenance;
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
                    foreach (var word in WordsOf(source, start, end, quoteFollows, engine))
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
    private static IEnumerable<string> WordsOf(
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

            var arrow = line[at..wordEnd] == NameMarker ? line.IndexOf(Arrow, wordEnd, StringComparison.Ordinal) : -1;
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

    private static IEnumerable<(string Path, string Value)> OfElements(JsonObject entry, string array, string member)
    {
        if (entry[array] is not JsonArray elements)
        {
            yield break;
        }

        for (var index = 0; index < elements.Count; index++)
        {
            var path = $"{array}[{index.ToString(CultureInfo.InvariantCulture)}].{member}";
            yield return (path, ValueOf(elements[index]?[member]));
        }
    }

    // What a text of a source is: a word or a parameter's name, a quoted name or a literal's content.
    private enum Kind
    {
        Word,
        QuotedName,
        Literal,
    }

    private static string ValueOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : node?.ToJsonString() ?? "null";
}
