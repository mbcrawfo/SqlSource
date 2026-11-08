using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

// The words walk: the words of a renamed plan, for the token check.
internal static partial class PostgresPlanRenamer
{
    /// <summary>
    /// The words of a renamed plan, for the token check, by each string's key: nothing of a fixed key or a count;
    /// a name as <c>"&lt;name&gt;"</c>; a CTE's <c>Subplan Name</c> as its name so; and an expression's tokens, a
    /// word, a quoted name and a string as written and every other token a character at a time.  A word or a name
    /// <c>&lt;stem&gt;_&lt;n&gt;</c> whose stem is shaped as a replacement gives its stem.  Every string of a key it
    /// does not read, the key too, and a plan or a string that does not read, are given whole.
    /// </summary>
    /// <param name="field">The plan, whose <see cref="RenamingField.Value" /> is the renamed plan.</param>
    public static IEnumerable<string> WordsOf(RenamingField field)
    {
        using var document = Parse(field.Value);
        if (document is null)
        {
            return [field.Value];
        }

        var words = new List<string>();
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            AllStrings(root, words);
            return words;
        }

        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                AllStrings(element, words);
                continue;
            }

            foreach (var member in element.EnumerateObject())
            {
                if (member.NameEquals(PlanKeys.Plan))
                {
                    NodeWords(member.Value, words);
                }
                else
                {
                    words.Add(member.Name);
                    AllStrings(member.Value, words);
                }
            }
        }

        return words;
    }

    // The stem of <stem>_<n> when the stem is shaped as a replacement, whose _<n> is the planner's; else null.
    private static string? StemOfChild(string spelling) =>
        ChildOf(spelling) is var (stem, _) && NameMapping.IsShapedAsReplacement(stem) ? stem : null;

    // A spelling as the token check reads it: a child of a replacement by its stem, else the spelling whole.
    private static string CheckedSpelling(string spelling) => StemOfChild(spelling) ?? spelling;

    private static void NodeWords(JsonElement node, List<string> words)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            AllStrings(node, words);
            return;
        }

        foreach (var member in node.EnumerateObject())
        {
            var value = member.Value;
            if (!Kinds.TryGetValue(member.Name, out var kind))
            {
                words.Add(member.Name);
                AllStrings(value, words);
                continue;
            }

            switch (kind)
            {
                case KeyKind.Structure when value.ValueKind == JsonValueKind.Array:
                    foreach (var child in value.EnumerateArray())
                    {
                        NodeWords(child, words);
                    }

                    break;
                case KeyKind.Structure:
                    NodeWords(value, words);
                    break;
                case KeyKind.Fixed or KeyKind.Count when value.ValueKind == JsonValueKind.String:
                    break;
                case KeyKind.Fixed or KeyKind.Count or KeyKind.Presence:
                    AllStrings(value, words);
                    break;
                case KeyKind.Name:
                    TextWords(
                        value,
                        words,
                        static (name, into) => into.Add(PostgresIdentifiers.Quote(CheckedSpelling(name)))
                    );
                    break;
                case KeyKind.Subplan:
                    TextWords(value, words, SubplanWords);
                    break;
                case KeyKind.Expression when value.ValueKind == JsonValueKind.Array:
                    foreach (var item in value.EnumerateArray())
                    {
                        TextWords(item, words, ExpressionWords);
                    }

                    break;
                case KeyKind.Expression:
                    TextWords(value, words, ExpressionWords);
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(kind);
            }
        }
    }

    // A string by the rule given; any other value's strings whole.
    private static void TextWords(JsonElement value, List<string> words, Action<string, List<string>> rule)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            rule(value.GetString()!, words);
        }
        else
        {
            AllStrings(value, words);
        }
    }

    private static void SubplanWords(string text, List<string> words)
    {
        if (text.Length > CtePrefix.Length && text.StartsWith(CtePrefix, StringComparison.Ordinal))
        {
            words.Add(PostgresIdentifiers.Quote(CheckedSpelling(text[CtePrefix.Length..])));
        }
        else
        {
            ExpressionWords(text, words);
        }
    }

    private static void ExpressionWords(string text, List<string> words)
    {
        if (PlanTokens.Read(text) is not { } tokens)
        {
            words.Add(text);
            return;
        }

        foreach (var token in tokens)
        {
            var written = text[token.Start..token.End];
            switch (token.Kind)
            {
                case PlanTokens.Kind.Word:
                    words.Add(CheckedSpelling(written));
                    break;
                case PlanTokens.Kind.QuotedName:
                    words.Add(
                        StemOfChild(PostgresIdentifiers.Unquote(written)) is { } stem
                            ? PostgresIdentifiers.Quote(stem)
                            : written
                    );
                    break;
                case PlanTokens.Kind.String:
                    words.Add(written);
                    break;
                case PlanTokens.Kind.Number
                or PlanTokens.Kind.Open
                or PlanTokens.Kind.Close
                or PlanTokens.Kind.OpenBracket
                or PlanTokens.Kind.CloseBracket
                or PlanTokens.Kind.Comma
                or PlanTokens.Kind.Dot
                or PlanTokens.Kind.Cast
                or PlanTokens.Kind.Other:
                    words.AddRange(written.Select(static character => character.ToString()));
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(token.Kind);
            }
        }
    }

    // Every string of a value, and every key of an object in it, whole.
    private static void AllStrings(JsonElement value, List<string> words)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                words.Add(value.GetString()!);
                break;
            case JsonValueKind.Object:
                foreach (var member in value.EnumerateObject())
                {
                    words.Add(member.Name);
                    AllStrings(member.Value, words);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    AllStrings(item, words);
                }

                break;
            case JsonValueKind.Undefined
            or JsonValueKind.Number
            or JsonValueKind.True
            or JsonValueKind.False
            or JsonValueKind.Null:
                break;
            default:
                // JsonValueKind is the runtime's, which a newer runtime may extend; today's has no other kind, so this
                // cannot be reached. A kind it adds is added as a word whole, which the token check refuses.
                words.Add(value.GetRawText());
                break;
        }
    }
}
