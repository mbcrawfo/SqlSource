using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

// The rename walk: one plan renamed key by key.
internal static partial class PostgresPlanRenamer
{
    /// <summary>
    /// The plan renamed, written indented with <c>\n</c> line ends.  A plan that is no JSON array of objects with a
    /// <c>Plan</c>, a node that is no object, a value of the wrong shape for its key, a string of an expression key
    /// that <see cref="PlanTokens" /> cannot read and a token of no kind it renames are refused, with
    /// <see cref="RefusalStep.PlanString" /> at the field's place; the return is then empty.
    /// </summary>
    /// <param name="field">The plan, whose <see cref="RenamingField.Value" /> is the plan as captured.</param>
    /// <param name="scope">The renaming's scope, which every name and literal comes from.</param>
    public static string Rename(RenamingField field, IRenamingScope scope) =>
        new Renaming(field, scope).Run() ?? string.Empty;

    // One plan renamed, written as it is read, which stops at its first refusal.
    private sealed class Renaming(RenamingField field, IRenamingScope scope)
    {
        // The aliases of the members of the plan's Appends that the walk reads as one prefix (AddChildren).
        private readonly Dictionary<string, (string Stem, string Suffix)> _children = [with(StringComparer.Ordinal)];

        public string? Run()
        {
            using var document = Parse(field.Value);
            if (document is null || !IsPlanRoot(document.RootElement))
            {
                _ = RefusePlan();
                return null;
            }

            var refused = false;
            var text = JsonFile.Text(writer =>
            {
                writer.WriteStartArray();
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    _children.Clear();
                    AddChildren(element.GetProperty(PlanKeys.Plan), _children);
                    writer.WriteStartObject();
                    writer.WritePropertyName(PlanKeys.Plan);
                    if (!Node(writer, element.GetProperty(PlanKeys.Plan)))
                    {
                        refused = true;
                        return;
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            });
            return refused ? null : text;
        }

        private bool Node(Utf8JsonWriter writer, JsonElement node)
        {
            if (node.ValueKind != JsonValueKind.Object)
            {
                return RefusePlan();
            }

            writer.WriteStartObject();
            foreach (var member in node.EnumerateObject())
            {
                if (!Kinds.TryGetValue(member.Name, out var kind))
                {
                    continue;
                }

                writer.WritePropertyName(member.Name);
                if (!Member(writer, member, kind))
                {
                    return false;
                }
            }

            writer.WriteEndObject();
            return true;
        }

        private bool Member(Utf8JsonWriter writer, JsonProperty member, KeyKind kind)
        {
            var value = member.Value;
            switch (kind)
            {
                case KeyKind.Structure when member.NameEquals(PlanKeys.Plan):
                    return Node(writer, value);
                case KeyKind.Structure:
                    return Nodes(writer, value);
                case KeyKind.Fixed:
                    return Text(writer, value, static text => text);
                case KeyKind.Count:
                    return Text(writer, value, RefuseText);
                case KeyKind.Presence:
                    var count = value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
                    writer.WriteStartArray();
                    for (var index = 0; index < count; index++)
                    {
                        writer.WriteStartObject();
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    return true;
                case KeyKind.Name when member.NameEquals(PlanKeys.Alias):
                    return Text(
                        writer,
                        value,
                        name => MemberOf(name, quoted: true) ?? NameOf(name, quoted: true, NameClass.Alias)
                    );
                case KeyKind.Name:
                    var cls = NameKeyClasses[member.Name];
                    return Text(writer, value, name => NameOf(name, quoted: true, cls));
                case KeyKind.Subplan:
                    return Text(writer, value, Subplan);
                case KeyKind.Expression when value.ValueKind == JsonValueKind.Array:
                    writer.WriteStartArray();
                    if (!value.EnumerateArray().All(item => Text(writer, item, Expression)))
                    {
                        return false;
                    }

                    writer.WriteEndArray();
                    return true;
                case KeyKind.Expression:
                    return Text(writer, value, Expression);
                default:
                    throw UnexpectedEnumValueException.Of(kind);
            }
        }

        private bool Nodes(Utf8JsonWriter writer, JsonElement nodes)
        {
            if (nodes.ValueKind != JsonValueKind.Array)
            {
                return RefusePlan();
            }

            writer.WriteStartArray();
            if (!nodes.EnumerateArray().All(node => Node(writer, node)))
            {
                return false;
            }

            writer.WriteEndArray();
            return true;
        }

        // A string by the rule given, which gives null when it refused; a number, a boolean or a null kept, since
        // none holds anything of the user's; an array or an object refused.
        private bool Text(Utf8JsonWriter writer, JsonElement value, Func<string, string?> rule)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    if (rule(value.GetString()!) is not { } renamed)
                    {
                        return false;
                    }

                    writer.WriteStringValue(renamed);
                    return true;
                case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null:
                    value.WriteTo(writer);
                    return true;
                case JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined:
                    return Refuse(value.GetRawText());
                default:
                    // JsonValueKind is the runtime's, which a newer runtime may extend; today's has no other kind, so
                    // this cannot be reached. A kind it adds holds something unknown, so the plan is refused.
                    return Refuse(value.GetRawText());
            }
        }

        // A name, or a word, renamed: <alias>_<n> that the mapping does not hold, of an alias that it holds or that
        // is the engine's, through the alias; any other as its own name.
        private string NameOf(string spelling, bool quoted, NameClass cls)
        {
            if (
                !scope.IsMapped(spelling, quoted)
                && !PostgresWords.IsEngineWord(spelling, quoted)
                && ChildOf(spelling) is var (stem, suffix)
                && (scope.IsMapped(stem, quoted) || PostgresWords.IsEngineWord(stem, quoted))
            )
            {
                return scope.Name(stem, quoted, cls) + suffix;
            }

            return scope.Name(spelling, quoted, cls);
        }

        // The Alias of each member of an Append that PlanPartitions.ShapeOf reads as one prefix, at and under the
        // node through Plans, as PlanNode reads a plan, with its stem and suffix.  A key that holds no string is read
        // as absent, which gives no prefix.
        private static void AddChildren(JsonElement node, Dictionary<string, (string Stem, string Suffix)> children)
        {
            if (
                node.ValueKind != JsonValueKind.Object
                || !node.TryGetProperty(PlanKeys.Plans, out var plans)
                || plans.ValueKind != JsonValueKind.Array
            )
            {
                return;
            }

            var members = plans.EnumerateArray().Select(static member => ShallowOf(member, [])).ToArray();
            if (PlanPartitions.ShapeOf(ShallowOf(node, members)) is not null)
            {
                foreach (var alias in members.Select(static member => member.Alias!))
                {
                    children[alias] = ChildOf(alias)!.Value;
                }
            }

            foreach (var child in plans.EnumerateArray())
            {
                AddChildren(child, children);
            }
        }

        // A node with the keys that PlanPartitions.ShapeOf reads, and the children given.
        private static PlanNode ShallowOf(JsonElement node, IReadOnlyList<PlanNode> plans) =>
            new(
                StringOf(node, PlanKeys.NodeType) ?? string.Empty,
                JoinType: null,
                StringOf(node, PlanKeys.ParentRelationship),
                Output: null,
                StringOf(node, PlanKeys.Alias),
                StringOf(node, PlanKeys.RelationName),
                StringOf(node, PlanKeys.Schema),
                SubplanName: null,
                CteName: null,
                Command: null,
                HasGroupingSets: false,
                Strategy: null,
                HasGroupKey: false,
                HasFilter: false,
                SubplansRemoved: 0,
                plans
            );

        private static string? StringOf(JsonElement node, string key) =>
            node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        // The alias of a member that the walk reads in one prefix, renamed through its stem whatever the mapping holds
        // of its spelling, when the stem is mapped or the engine's; else null.  The renamed members then share the
        // stem's replacement as their prefix, as the original's members share the stem.
        private string? MemberOf(string spelling, bool quoted) =>
            _children.TryGetValue(spelling, out var child)
            && (scope.IsMapped(child.Stem, quoted) || PostgresWords.IsEngineWord(child.Stem, quoted))
                ? scope.Name(child.Stem, quoted, NameClass.Alias) + child.Suffix
                : null;

        private string? Subplan(string text) =>
            text.Length > CtePrefix.Length && text.StartsWith(CtePrefix, StringComparison.Ordinal)
                ? CtePrefix + NameOf(text[CtePrefix.Length..], quoted: true, NameClass.Alias)
                : Expression(text);

        // An expression through PlanTokens: a word or a quoted name by the mapping, a function's where "(" follows,
        // a string as the mapping's literal, a number and punctuation kept, and any other token kept only when it
        // holds no letter, digit or "_".
        private string? Expression(string text)
        {
            if (PlanTokens.Read(text) is not { } tokens)
            {
                return RefuseText(text);
            }

            var renamed = new StringBuilder(text.Length);
            var at = 0;
            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                var written = text[token.Start..token.End];
                var cls =
                    index + 1 < tokens.Count && tokens[index + 1].Kind == PlanTokens.Kind.Open
                        ? NameClass.Function
                        : NameClass.Alias;
                var qualifier = Qualifies(tokens, index);
                var replacement = token.Kind switch
                {
                    PlanTokens.Kind.Word => (qualifier ? MemberOf(written, quoted: false) : null)
                        ?? NameOf(written, quoted: false, cls),
                    PlanTokens.Kind.QuotedName => PostgresIdentifiers.Quote(
                        (qualifier ? MemberOf(PostgresIdentifiers.Unquote(written), quoted: true) : null)
                            ?? NameOf(PostgresIdentifiers.Unquote(written), quoted: true, cls)
                    ),
                    PlanTokens.Kind.String => "'"
                        + scope
                            .Literal(SqlRenamer.ContentOf(written, 0, written.Length))
                            .Replace("'", "''", StringComparison.Ordinal)
                        + "'",
                    PlanTokens.Kind.Other when HasNameCharacter(written) => null,
                    PlanTokens.Kind.Number
                    or PlanTokens.Kind.Open
                    or PlanTokens.Kind.Close
                    or PlanTokens.Kind.OpenBracket
                    or PlanTokens.Kind.CloseBracket
                    or PlanTokens.Kind.Comma
                    or PlanTokens.Kind.Dot
                    or PlanTokens.Kind.Cast
                    or PlanTokens.Kind.Other => written,
                    _ => throw UnexpectedEnumValueException.Of(token.Kind),
                };
                if (replacement is null)
                {
                    return RefuseText(written);
                }

                _ = renamed.Append(text, at, token.Start - at).Append(replacement);
                at = token.End;
            }

            return renamed.Append(text, at, text.Length - at).ToString();
        }

        // Whether the token qualifies a column, as PlanPartitions.WithoutAlias reads one: a "." directly follows it and
        // none directly precedes it.
        private static bool Qualifies(List<PlanTokens.Token> tokens, int index) =>
            index + 1 < tokens.Count
            && IsDotAt(tokens[index + 1], tokens[index].End)
            && (index == 0 || !IsDotAt(tokens[index - 1], tokens[index].Start - 1));

        private static bool IsDotAt(PlanTokens.Token token, int at) =>
            token.Kind == PlanTokens.Kind.Dot && token.Start == at;

        private static bool HasNameCharacter(string token) =>
            token.Any(static character => char.IsLetterOrDigit(character) || character == '_');

        private bool RefusePlan() => Refuse(field.Value);

        private string? RefuseText(string token)
        {
            _ = Refuse(token);
            return null;
        }

        // Refuses at the plan's place, the token the text given; false, for the caller to stop.
        private bool Refuse(string token)
        {
            scope.Refuse(new RenamingRefusal(RefusalStep.PlanString, CaptureRenamer.PlaceOf(field), token));
            return false;
        }
    }
}
