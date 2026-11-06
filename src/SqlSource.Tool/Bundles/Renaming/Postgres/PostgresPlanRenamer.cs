using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// The plan of an <c>explain</c> call, the text of <c>EXPLAIN (VERBOSE, FORMAT JSON)</c>, renamed key by key: each key
/// of <see cref="PlanKeys" /> by its kind, and every other key dropped, at any depth.
/// </summary>
/// <remarks>
/// <para>
/// A name key, <c>Alias</c>, <c>Relation Name</c>, <c>Schema</c> or <c>CTE Name</c>, holds one raw name, renamed as a
/// catalog holds it, quoted, so that the same spelling quoted in <c>Output</c> has the same replacement.  An
/// expression key is renamed token by token through <see cref="PlanTokens" />, as the walk reads it.
/// </para>
/// <para>
/// The planner prints a partition's scan, or a child of an inherited table, under its parent's alias and a number,
/// <c>orders_1</c> (<see cref="PlanPartitions" />), which the query does not hold.  Such a name is renamed through
/// the alias, <c>t_1234abcd_1</c>, so that the members of an <c>Append</c> keep one prefix, and the token check reads
/// it by its stem.  The members of an <c>Append</c> that <see cref="PlanPartitions.ShapeOf" /> reads as one prefix are
/// renamed so whatever the mapping holds of their spelling, since a partition may be named as its scan, and so is
/// the same spelling where it qualifies a column; a <c>Relation Name</c> keeps its table's own replacement.
/// </para>
/// </remarks>
internal static class PostgresPlanRenamer
{
    // The member of explain's answer that holds the plan.
    private const string PlanMember = "plan";

    // What starts the Subplan Name of a CTE's plan, before the CTE's name.
    private const string CtePrefix = "CTE ";

    private static readonly JsonDocumentOptions ReadOptions = new() { MaxDepth = PlanNode.MaxDepth };

    private static readonly JsonWriterOptions WriteOptions = new()
    {
        Indented = true,
        Encoder = CaptureJson.Options.Encoder,
    };

    // The kind of each key the walk reads.
    private static readonly FrozenDictionary<string, KeyKind> Kinds = new Dictionary<string, KeyKind>(
        StringComparer.Ordinal
    )
    {
        [PlanKeys.Plan] = KeyKind.Structure,
        [PlanKeys.Plans] = KeyKind.Structure,
        [PlanKeys.NodeType] = KeyKind.Fixed,
        [PlanKeys.JoinType] = KeyKind.Fixed,
        [PlanKeys.ParentRelationship] = KeyKind.Fixed,
        [PlanKeys.Strategy] = KeyKind.Fixed,
        [PlanKeys.Command] = KeyKind.Fixed,
        [PlanKeys.SubplansRemoved] = KeyKind.Count,
        [PlanKeys.GroupingSets] = KeyKind.Presence,
        [PlanKeys.Alias] = KeyKind.Name,
        [PlanKeys.RelationName] = KeyKind.Name,
        [PlanKeys.Schema] = KeyKind.Name,
        [PlanKeys.CteName] = KeyKind.Name,
        [PlanKeys.SubplanName] = KeyKind.Subplan,
        [PlanKeys.Output] = KeyKind.Expression,
        [PlanKeys.Filter] = KeyKind.Expression,
        [PlanKeys.GroupKey] = KeyKind.Expression,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // The class of a new name of each name key.
    private static readonly FrozenDictionary<string, NameClass> NameKeyClasses = new Dictionary<string, NameClass>(
        StringComparer.Ordinal
    )
    {
        [PlanKeys.Alias] = NameClass.Alias,
        [PlanKeys.RelationName] = NameClass.Table,
        [PlanKeys.Schema] = NameClass.Schema,
        [PlanKeys.CteName] = NameClass.Alias,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private enum KeyKind
    {
        // A node, or the nodes under one.
        Structure,

        // A word of the planner's closed set, kept.
        Fixed,

        // A number, kept.
        Count,

        // Read only for being there: kept, with nothing in it.
        Presence,

        // One raw name.
        Name,

        // CTE and a raw name, or an expression.
        Subplan,

        // A string, or strings, of PlanTokens.
        Expression,
    }

    /// <summary>The keys this gives a kind, which are every key of <see cref="PlanKeys.All" />.</summary>
    public static IReadOnlyCollection<string> KeysRead => Kinds.Keys;

    /// <summary>
    /// Registers the names of the plan's name keys, each one raw name in its key's class, but an <c>Alias</c> of the
    /// form <c>&lt;alias&gt;_&lt;n&gt;</c>, which is renamed through its alias.  Does nothing when the call's
    /// <c>answer.plan</c> is no string, or no plan: <see cref="Rename" /> refuses one that does not read.
    /// </summary>
    public static void Register(RenamingCall call, IRenamingScope scope)
    {
        if (
            call.Renamed[CaptureRenamer.Answer] is not JsonObject answer
            || answer[PlanMember] is not JsonValue value
            || !value.TryGetValue<string>(out var plan)
        )
        {
            return;
        }

        using var document = Parse(plan);
        if (document is null || !IsPlanRoot(document.RootElement))
        {
            return;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            RegisterNode(element.GetProperty(PlanKeys.Plan), scope);
        }
    }

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

    private static JsonDocument? Parse(string text)
    {
        try
        {
            return JsonDocument.Parse(text, ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A JSON array whose every element is an object with a Plan.
    private static bool IsPlanRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(PlanKeys.Plan, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static void RegisterNode(JsonElement node, IRenamingScope scope)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var member in node.EnumerateObject())
        {
            if (!Kinds.TryGetValue(member.Name, out var kind))
            {
                continue;
            }

            var value = member.Value;
            if (kind == KeyKind.Structure)
            {
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in value.EnumerateArray())
                    {
                        RegisterNode(child, scope);
                    }
                }
                else
                {
                    RegisterNode(value, scope);
                }
            }
            else if (
                kind == KeyKind.Name
                && value.ValueKind == JsonValueKind.String
                && value.GetString() is { } name
                && !(member.NameEquals(PlanKeys.Alias) && ChildOf(name) is not null)
            )
            {
                _ = scope.Name(name, quoted: true, NameKeyClasses[member.Name]);
            }
        }
    }

    // <stem>_<n>, n one or more ASCII digits, as the stem and "_<n>"; null for any other spelling.
    private static (string Stem, string Suffix)? ChildOf(string spelling)
    {
        var underscore = spelling.LastIndexOf('_');
        if (underscore <= 0 || underscore == spelling.Length - 1)
        {
            return null;
        }

        foreach (var character in spelling.AsSpan(underscore + 1))
        {
            if (!char.IsAsciiDigit(character))
            {
                return null;
            }
        }

        return (spelling[..underscore], spelling[underscore..]);
    }

    // A lower-case letter, "_" and eight or more lower-case hex digits, as NameMapping makes a replacement.
    private static bool IsReplacementShaped(string stem)
    {
        if (stem.Length < 10 || !char.IsAsciiLetterLower(stem[0]) || stem[1] != '_')
        {
            return false;
        }

        foreach (var character in stem.AsSpan(2))
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }

        return true;
    }

    // The stem of <stem>_<n> when the stem is shaped as a replacement, whose _<n> is the planner's; else null.
    private static string? StemOfChild(string spelling) =>
        ChildOf(spelling) is var (stem, _) && IsReplacementShaped(stem) ? stem : null;

    // A spelling as the token check reads it: a child of a replacement by its stem, else the spelling whole.
    private static string CheckedSpelling(string spelling) => StemOfChild(spelling) ?? spelling;

    private static string Quoted(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

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
                    TextWords(value, words, static (name, into) => into.Add(Quoted(CheckedSpelling(name))));
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
            words.Add(Quoted(CheckedSpelling(text[CtePrefix.Length..])));
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
                    words.Add(StemOfChild(PostgresIdentifiers.Unquote(written)) is { } stem ? Quoted(stem) : written);
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

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, WriteOptions))
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
                        return null;
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            // The writer ends a line as the platform does, and a string's own line break is always escaped.
            return Encoding.UTF8.GetString(buffer.WrittenSpan).Replace("\r\n", "\n", StringComparison.Ordinal);
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
                    PlanTokens.Kind.QuotedName => Quoted(
                        (qualifier ? MemberOf(PostgresIdentifiers.Unquote(written), quoted: true) : null)
                            ?? NameOf(PostgresIdentifiers.Unquote(written), quoted: true, cls)
                    ),
                    PlanTokens.Kind.String => "'"
                        + scope
                            .Literal(written[1..^1].Replace("''", "'", StringComparison.Ordinal))
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
