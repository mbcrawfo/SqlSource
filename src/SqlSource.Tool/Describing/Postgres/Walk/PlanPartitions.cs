using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// The <c>Append</c>s of a plan that may scan a partitioned table: what <c>partitions</c> is asked, and what
/// <see cref="PlanInstances" /> reads of the members.
/// </summary>
/// <remarks>
/// <para>
/// A partitioned table scans as an <c>Append</c> or a <c>Merge Append</c> whose <c>Member</c>s scan its partitions,
/// each under the table's alias and a number, <c>e_1</c> and <c>e_2</c> for a table aliased <c>e</c>, which is the
/// alias the nodes above print.  A <c>UNION ALL</c> plans as an <c>Append</c> too, and its root prints the first
/// branch, so the shape alone proves nothing: the members must also be partitions of one table, which
/// <c>partitions</c> answers, and must print the same <c>Output</c> but for the alias, which a set operation of
/// different branches does not.
/// </para>
/// <para>
/// Every name a plan prints is unique within it, so two scans cannot share an alias, and the alias of each member
/// is its own.
/// </para>
/// </remarks>
internal static class PlanPartitions
{
    /// <summary>
    /// The relations the members scan of every <c>Append</c> of the shape, anywhere in the plan, in schema and name
    /// order, each once: empty when there is none, and <c>partitions</c> is not asked.
    /// </summary>
    public static IReadOnlyList<PostgresRelationName> MembersOf(PlanNode root) =>
        PlanRelations.InOrder(
            root.SelfAndDescendants()
                .Where(static node => ShapeOf(node) is not null)
                .SelectMany(static node => node.Plans)
                .Select(static member => new PostgresRelationName(member.Schema!, member.RelationName!))
        );

    /// <summary>
    /// The alias the nodes above an <c>Append</c> print for its members, when the node is an <c>Append</c> or a
    /// <c>Merge Append</c> whose every child is a <c>Member</c> scan of a relation, with a schema, a relation and an
    /// alias that is one prefix and a number; or null.
    /// </summary>
    public static string? ShapeOf(PlanNode node)
    {
        if (!PlanNodeTypes.Appends.Contains(node.NodeType) || node.Plans.Count == 0)
        {
            return null;
        }

        string? prefix = null;
        foreach (var member in node.Plans)
        {
            if (
                member.ParentRelationship != PlanRelationships.Member
                || !PlanInstances.ScansARelation(member)
                || member.Schema is null
                || member.RelationName is null
                || member.Alias is not { } alias
                || PrefixOf(alias) is not { } own
                || (prefix is not null && !string.Equals(prefix, own, StringComparison.Ordinal))
            )
            {
                return null;
            }

            prefix = own;
        }

        return prefix;
    }

    /// <summary>
    /// The tables every member of the <c>Append</c> is a partition of, by OID, nearest first, when its members print
    /// the same <c>Output</c> but for their aliases: empty when they are not, or when a member's relation has no
    /// answer.
    /// </summary>
    /// <param name="node">An <c>Append</c> that <see cref="ShapeOf" /> gave a prefix.</param>
    /// <param name="partitions">The answer of <c>partitions</c>, by relation.</param>
    public static IReadOnlyList<long> ParentsOf(
        PlanNode node,
        IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>> partitions
    )
    {
        IReadOnlyList<long>? common = null;
        IReadOnlyList<string>? output = null;
        foreach (var member in node.Plans)
        {
            if (!partitions.TryGetValue(new PostgresRelationName(member.Schema!, member.RelationName!), out var mine))
            {
                return [];
            }

            common = common is null ? mine : [.. common.Where(mine.Contains)];
            IReadOnlyList<string> own = member.Output is { } entries
                ? [.. entries.Select(entry => WithoutAlias(entry, member.Alias!))]
                : [];
            if (output is not null && !output.SequenceEqual(own, StringComparer.Ordinal))
            {
                return [];
            }

            output = own;
        }

        return common ?? [];
    }

    // The alias without its last "_" and number, or null when it has none.
    private static string? PrefixOf(string alias)
    {
        var cut = alias.LastIndexOf('_');
        return cut > 0 && cut < alias.Length - 1 && alias[(cut + 1)..].All(char.IsAsciiDigit) ? alias[..cut] : null;
    }

    /// <summary>
    /// The entry with each name of the alias that qualifies a column, bare or quoted, replaced by a mark no name
    /// holds: so that two members' entries compare the same when they differ by the alias alone.  A name qualifies
    /// when a <c>.</c> directly follows it and none directly precedes it.  A string constant is copied as it is, and
    /// an entry whose quotes or parentheses do not balance is returned unchanged.
    /// </summary>
    public static string WithoutAlias(string entry, string alias)
    {
        if (PlanExpression.Tokens.Read(entry) is not { } tokens)
        {
            return entry;
        }

        var text = new StringBuilder(entry.Length);
        var copied = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            string? name = null;
            if (token.Kind == PlanExpression.Kind.Word)
            {
                name = entry[token.Start..token.End];
            }
            else if (token.Kind == PlanExpression.Kind.QuotedName)
            {
                name = PostgresIdentifiers.Unquote(entry.AsSpan(token.Start, token.End - token.Start));
            }

            var qualifies =
                index + 1 < tokens.Count
                && IsDotAt(tokens[index + 1], token.End)
                && (index == 0 || !IsDotAt(tokens[index - 1], token.Start - 1));
            if (qualifies && string.Equals(name, alias, StringComparison.Ordinal))
            {
                _ = text.Append(entry, copied, token.Start - copied).Append('\u0001');
                copied = token.End;
            }
        }

        return text.Append(entry, copied, entry.Length - copied).ToString();
    }

    // Whether the token is a "." that stands at the index.
    private static bool IsDotAt(PlanExpression.Token token, int at) =>
        token.Kind == PlanExpression.Kind.Dot && token.Start == at;
}
