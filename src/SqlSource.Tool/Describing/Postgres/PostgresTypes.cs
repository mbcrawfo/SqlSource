using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The sidecar's record of a type, from the rows that <c>types</c> answered.
/// </summary>
/// <remarks>
/// The rows are the closure of the types asked for: every type reached through the element of an array, the base of
/// a domain, the subtype of a range and the range of a multirange.  A row that is missing for a type that is
/// reached is a bug of the query, and throws.
/// </remarks>
internal static class PostgresTypes
{
    /// <summary>
    /// Whether the type is a pseudo-type, <c>record</c>, <c>cstring</c>, <c>anyelement</c>, or an array of one,
    /// <c>cstring[]</c>: one that no value has and that cannot be described.  The server accepts one as a
    /// parameter's type, declared or inferred.
    /// </summary>
    public static bool IsPseudo(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows)
    {
        var row = Row(oid, rows);
        return row.Typtype == "p" || (IsArray(row) && IsPseudo(row.Typelem, rows));
    }

    /// <summary>The record of a type.</summary>
    /// <param name="oid">The type.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <param name="facets">
    /// What the type's modifier sets.  They land on a base type, and on the base of a domain, since the protocol
    /// applies a domain's modifier to its base.  The element of an array and a subtype have none.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack a type that is reached, or the type is a pseudo-type, or of a kind this version does not know.
    /// </exception>
    public static PostgresType Build(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows, PostgresFacets facets)
    {
        var row = Row(oid, rows);
        return row.Typtype switch
        {
            "b" when IsArray(row) => Array(row, rows),
            "b" => Plain(row, SidecarValues.Kind.Base, facets),
            "d" => Plain(row, SidecarValues.Kind.Domain, PostgresFacets.None) with
            {
                Base = Build(row.Typbasetype, rows, facets),
            },
            "e" => Plain(row, SidecarValues.Kind.Enum, PostgresFacets.None) with
            {
                Labels = new EquatableArray<string>([.. row.EnumLabels ?? throw Lacks(row, "labels")]),
            },
            "r" => Plain(row, SidecarValues.Kind.Range, PostgresFacets.None) with
            {
                Subtype = Build(row.Rngsubtype ?? throw Lacks(row, "subtype"), rows, PostgresFacets.None),
            },
            "m" => Plain(row, SidecarValues.Kind.Multirange, PostgresFacets.None) with
            {
                Subtype = Build(row.MultirangeRngsubtype ?? throw Lacks(row, "subtype"), rows, PostgresFacets.None),
            },
            "c" => Plain(row, SidecarValues.Kind.Composite, PostgresFacets.None),
            _ => throw new InvalidOperationException(
                $"The type '{row.FormatType}' has the typtype '{row.Typtype}', which cannot be described."
            ),
        };
    }

    // A domain has the category of its base, so an array is a base type of the category A.
    private static bool IsArray(PostgresTypeRow row) => row is { Typtype: "b", Typcategory: "A" };

    private static PostgresType Array(PostgresTypeRow row, IReadOnlyDictionary<long, PostgresTypeRow> rows)
    {
        var element = Build(row.Typelem, rows, PostgresFacets.None);
        return new PostgresType(
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
    }

    private static PostgresType Plain(PostgresTypeRow row, string kind, PostgresFacets facets) =>
        new(
            PostgresTypeNames.Spell(row, facets),
            kind,
            row.Nspname,
            row.Typname,
            facets.Length,
            facets.Precision,
            facets.Scale,
            null,
            null,
            null,
            null
        );

    private static PostgresTypeRow Row(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(oid, out var row)
            ? row
            : throw new InvalidOperationException($"The answer to 'types' holds no row for the type {oid}.");

    private static InvalidOperationException Lacks(PostgresTypeRow row, string what) =>
        new($"The row of the type '{row.FormatType}' holds no {what}.");
}
