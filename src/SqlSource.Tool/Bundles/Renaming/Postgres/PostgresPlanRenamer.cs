using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;

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
internal static partial class PostgresPlanRenamer
{
    // The member of explain's answer that holds the plan.
    private const string PlanMember = "plan";

    // What starts the Subplan Name of a CTE's plan, before the CTE's name.
    private const string CtePrefix = "CTE ";

    private static readonly JsonDocumentOptions ReadOptions = new() { MaxDepth = PlanNode.MaxDepth };

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
}
