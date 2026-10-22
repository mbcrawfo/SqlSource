using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres.Types;

/// <summary>
/// The sidecar's record of a type, from the rows that <c>types</c> answered.
/// </summary>
/// <remarks>
/// The rows are the closure of the types asked for: every type reached through the element of an array, the base of
/// a domain, the subtype of a range and the range of a multirange.  A row that is missing for a type that is
/// reached is a bug of the query, and throws.  A type is built with a modifier, which the server spells through
/// <c>spellings</c> and <see cref="TypeModifiers" /> decodes into facets; the type of a column of a capture recorded
/// before the modifier was read is built with the facets of Npgsql's three numbers instead, and spelled as it was.
/// A parameter's own type never carries a modifier: only the base of a domain does, and what lies under it.
/// </remarks>
internal static class PostgresTypes
{
    /// <summary>
    /// Whether the type is a pseudo-type, <c>record</c>, <c>cstring</c>, <c>anyelement</c>, or an array of one,
    /// <c>cstring[]</c>, or a domain over either: one that no value has and that cannot be described.  The server
    /// accepts one as a parameter's type, declared or inferred.
    /// </summary>
    public static bool IsPseudo(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows)
    {
        var row = Row(oid, rows);
        return row.Typtype == "p"
            || (IsArray(row) && IsPseudo(row.Typelem, rows))
            || (row.Typtype == "d" && IsPseudo(row.Typbasetype, rows));
    }

    /// <summary>
    /// The record of a type with a modifier: a column's, as the row description gives it, or <c>-1</c> for a
    /// parameter's, which the protocol gives none.
    /// </summary>
    /// <param name="oid">The type.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <param name="modifier">
    /// The modifier, <c>-1</c> for none.  A base type is spelled with it and carries its facets, and an array gives it
    /// to its element.  A domain gives its base its own <c>typtypmod</c>, or this one when it fixes none.  A subtype
    /// has none.
    /// </param>
    /// <param name="spellings">The server's spelling of every pair that <see cref="PairsOf" /> gives.</param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack a type that is reached, the spellings lack a pair, or the type is a pseudo-type, or of a kind
    /// this version does not know.
    /// </exception>
    public static PostgresType Build(
        long oid,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        int modifier,
        TypeSpellings spellings
    ) => Walk(oid, rows, modifier, new Builder(spellings));

    /// <summary>The record of the type of a column whose capture holds no modifier.</summary>
    /// <param name="oid">The type.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <param name="facets">
    /// What Npgsql's three numbers say the column's modifier sets.  They land on a base type, and on the base of a
    /// domain, since the protocol applies a domain's modifier to its base.  The element of an array and a subtype
    /// have none: Npgsql gives an array the numbers of its element, which are dropped.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack a type that is reached, or the type is a pseudo-type, or of a kind this version does not know.
    /// </exception>
    /// <remarks>
    /// It keeps a switch of its own and does not share <see cref="Walk{T}" />: an array gives its element no facets,
    /// where the walk gives the element the array's modifier, and a domain passes its facets down unchanged, where the
    /// walk gives its base <see cref="BaseModifierOf" />.
    /// </remarks>
    public static PostgresType Build(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, PostgresFacets facets)
    {
        var row = Row(oid, rows);
        return row.Typtype switch
        {
            "b" when IsArray(row) => ArrayOf(row, Build(row.Typelem, rows, PostgresFacets.None)),
            "b" => Plain(row, SidecarValues.Kind.Base, (PostgresTypeNames.Spell(row, facets), facets)),
            "d" => Domain(row, Build(row.Typbasetype, rows, facets)),
            "e" => Enum(row),
            "r" => Range(row, Build(RangeSubtypeOf(row), rows, PostgresFacets.None)),
            "m" => Multirange(row, Build(MultirangeSubtypeOf(row), rows, PostgresFacets.None)),
            "c" => Composite(row),
            _ => throw Unknown(row),
        };
    }

    /// <summary>
    /// The pairs of a type and a modifier other than <c>-1</c> that the build with a modifier looks up when it is
    /// given the same arguments, and no others.  A pseudo-type and a kind this version does not know give none: the
    /// session refuses the one, and the build throws for the other.
    /// </summary>
    /// <exception cref="InvalidOperationException">The rows lack a type that is reached.</exception>
    public static IEnumerable<PostgresTypmodPair> PairsOf(
        long oid,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        int modifier
    ) => Walk(oid, rows, modifier, Pairs.Instance);

    // The one walk over typtype of the build with a modifier and of PairsOf, so that the pairs asked for are the ones
    // the build looks up: an array gives its modifier to its element, a domain gives its base BaseModifierOf, and a
    // subtype has none.  The node below is walked before the node above is visited.
    private static T Walk<T>(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, int modifier, ITypeWalk<T> walk)
    {
        var row = Row(oid, rows);
        return row.Typtype switch
        {
            "b" when IsArray(row) => walk.AtArray(row, Walk(row.Typelem, rows, modifier, walk)),
            "b" => walk.AtBase(row, modifier),
            "d" => walk.AtDomain(row, Walk(row.Typbasetype, rows, BaseModifierOf(row, modifier), walk)),
            "e" => walk.AtEnum(row),
            "r" => walk.AtRange(row, Walk(RangeSubtypeOf(row), rows, TypeModifiers.NoModifier, walk)),
            "m" => walk.AtMultirange(row, Walk(MultirangeSubtypeOf(row), rows, TypeModifiers.NoModifier, walk)),
            "c" => walk.AtComposite(row),
            _ => walk.AtOther(row),
        };
    }

    // What a walk makes at each kind of type, from what it made of the type below.  AtOther is at a pseudo-type, or at
    // a kind this version does not know.
    private interface ITypeWalk<T>
    {
        T AtArray(PostgresTypeRow row, T element);

        T AtBase(PostgresTypeRow row, int modifier);

        T AtDomain(PostgresTypeRow row, T baseType);

        T AtEnum(PostgresTypeRow row);

        T AtRange(PostgresTypeRow row, T subtype);

        T AtMultirange(PostgresTypeRow row, T subtype);

        T AtComposite(PostgresTypeRow row);

        T AtOther(PostgresTypeRow row);
    }

    // The build with a modifier: it throws where the type cannot be described.
    private sealed class Builder(TypeSpellings spellings) : ITypeWalk<PostgresType>
    {
        public PostgresType AtArray(PostgresTypeRow row, PostgresType element) => ArrayOf(row, element);

        public PostgresType AtBase(PostgresTypeRow row, int modifier) =>
            Plain(row, SidecarValues.Kind.Base, spellings.Of(row, modifier));

        public PostgresType AtDomain(PostgresTypeRow row, PostgresType baseType) => Domain(row, baseType);

        public PostgresType AtEnum(PostgresTypeRow row) => Enum(row);

        public PostgresType AtRange(PostgresTypeRow row, PostgresType subtype) => Range(row, subtype);

        public PostgresType AtMultirange(PostgresTypeRow row, PostgresType subtype) => Multirange(row, subtype);

        public PostgresType AtComposite(PostgresTypeRow row) => Composite(row);

        public PostgresType AtOther(PostgresTypeRow row) => throw Unknown(row);
    }

    // The pairs: one at a base type with a modifier, and none where the build throws, since the session refuses a
    // pseudo-type and the labels of an enum are no pair.
    private sealed class Pairs : ITypeWalk<IEnumerable<PostgresTypmodPair>>
    {
        public static Pairs Instance { get; } = new();

        public IEnumerable<PostgresTypmodPair> AtArray(PostgresTypeRow row, IEnumerable<PostgresTypmodPair> element) =>
            element;

        public IEnumerable<PostgresTypmodPair> AtBase(PostgresTypeRow row, int modifier) =>
            modifier == TypeModifiers.NoModifier ? [] : [new PostgresTypmodPair(row.Oid, modifier)];

        public IEnumerable<PostgresTypmodPair> AtDomain(
            PostgresTypeRow row,
            IEnumerable<PostgresTypmodPair> baseType
        ) => baseType;

        public IEnumerable<PostgresTypmodPair> AtEnum(PostgresTypeRow row) => [];

        public IEnumerable<PostgresTypmodPair> AtRange(PostgresTypeRow row, IEnumerable<PostgresTypmodPair> subtype) =>
            subtype;

        public IEnumerable<PostgresTypmodPair> AtMultirange(
            PostgresTypeRow row,
            IEnumerable<PostgresTypmodPair> subtype
        ) => subtype;

        public IEnumerable<PostgresTypmodPair> AtComposite(PostgresTypeRow row) => [];

        public IEnumerable<PostgresTypmodPair> AtOther(PostgresTypeRow row) => [];
    }

    // A domain has the category of its base, so an array is a base type of the category A.  int2vector and oidvector
    // have the category A and an element, as an array has, but they are no arrays: the protocol has each as a base
    // type of its own, and Npgsql binds it by its name.
    internal static bool IsArray(PostgresTypeRow row) =>
        row is { Typtype: "b", Typcategory: "A" } && row.Oid is not (BuiltInTypes.Int2vector or BuiltInTypes.Oidvector);

    // The modifier a domain gives its base: its own typtypmod, which a null of an old capture reads as -1, or else
    // the one it was built with.  Only a domain over a type that is no domain can fix one, and the row description
    // gives a column of any domain over it that one, so for a column the two agree; a parameter, built with -1,
    // takes the domain's.
    private static int BaseModifierOf(PostgresTypeRow domain, int modifier) =>
        domain.Typtypmod is { } own and not TypeModifiers.NoModifier ? own : modifier;

    private static long RangeSubtypeOf(PostgresTypeRow row) => row.Rngsubtype ?? throw Lacks(row, "subtype");

    private static long MultirangeSubtypeOf(PostgresTypeRow row) =>
        row.MultirangeRngsubtype ?? throw Lacks(row, "subtype");

    private static PostgresType ArrayOf(PostgresTypeRow row, PostgresType element) =>
        new(
            PostgresTypeNames.ArrayOf(element.Name),
            SidecarValues.Kind.Array,
            row.Nspname,
            row.Typname,
            null,
            null,
            null,
            element,
            null,
            null,
            null
        );

    private static PostgresType Domain(PostgresTypeRow row, PostgresType baseType) =>
        Unmodified(row, SidecarValues.Kind.Domain) with
        {
            Base = baseType,
        };

    private static PostgresType Enum(PostgresTypeRow row) =>
        Unmodified(row, SidecarValues.Kind.Enum) with
        {
            Labels = new EquatableArray<string>([.. row.EnumLabels ?? throw Lacks(row, "labels")]),
        };

    private static PostgresType Range(PostgresTypeRow row, PostgresType subtype) =>
        Unmodified(row, SidecarValues.Kind.Range) with
        {
            Subtype = subtype,
        };

    private static PostgresType Multirange(PostgresTypeRow row, PostgresType subtype) =>
        Unmodified(row, SidecarValues.Kind.Multirange) with
        {
            Subtype = subtype,
        };

    private static PostgresType Composite(PostgresTypeRow row) => Unmodified(row, SidecarValues.Kind.Composite);

    private static PostgresType Unmodified(PostgresTypeRow row, string kind) =>
        Plain(row, kind, (PostgresTypeNames.Spell(row, PostgresFacets.None), PostgresFacets.None));

    private static PostgresType Plain(PostgresTypeRow row, string kind, (string Name, PostgresFacets Facets) spelled) =>
        new(
            spelled.Name,
            kind,
            row.Nspname,
            row.Typname,
            spelled.Facets.Length,
            spelled.Facets.Precision,
            spelled.Facets.Scale,
            null,
            null,
            null,
            null
        );

    private static InvalidOperationException Unknown(PostgresTypeRow row) =>
        new($"The type '{row.FormatType}' has the typtype '{row.Typtype}', which cannot be described.");

    /// <summary>The row of a type, which the rows must hold: one they lack is a bug of the describer.</summary>
    /// <param name="oid">The type.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <param name="owner">What has the type, for the message: <c>the column 'id'</c>.</param>
    /// <exception cref="InvalidOperationException">The rows lack the type.</exception>
    internal static PostgresTypeRow Row(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, string? owner = null)
    {
        if (rows.TryGetValue(oid, out var row))
        {
            return row;
        }

        var of = owner is null ? string.Empty : $" of {owner}";
        throw new InvalidOperationException($"The answer to 'types' holds no row for the type {oid}{of}.");
    }

    private static InvalidOperationException Lacks(PostgresTypeRow row, string what) =>
        new($"The row of the type '{row.FormatType}' holds no {what}.");
}
