using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;

namespace SqlSource.Tool.Testing.Bundles;

// What of a schema's and a query's text a renamed bundle of PostgreSQL still holds, found without asking the renaming:
// Of lexes the sources as PostgreSQL and seeks what it finds in every string of every file, so it suits an engine
// whose bundle keeps none of the user's text in a member the renaming leaves.  SQL Server's search is
// SqlServerBundleLeaks.Find.
internal static class PostgresBundleLeaks
{
    // A shorter literal is too common a substring to be sought.
    private const int ShortestLiteral = 4;

    // The markers that Of reads a name from.
    private const string NameMarker = "-- name:";
    private const string ParamMarker = "-- param:";

    // The capture's list of calls and the member of a call that names it.
    private const string CallsMember = "calls";
    private const string MethodMember = "method";

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
}
