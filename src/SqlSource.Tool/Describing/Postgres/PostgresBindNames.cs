using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The name that <c>check</c> binds a parameter's type by in Npgsql, and that the sidecar records as
/// <c>bindName</c> once Npgsql has taken it.
/// </summary>
/// <remarks>
/// The name is a candidate: it is computed from the catalog rows alone, and only a name that Npgsql takes in a run is
/// recorded.  It never depends on the session's <c>search_path</c> and never holds a modifier, since a parameter's
/// type has none.  The rules, in order:
/// <list type="number">
/// <item>An array is its element's name and <c>[]</c>.</item>
/// <item>
/// A domain over a domain is the name of the first base, going down, that is no domain: Npgsql sends a domain as its
/// base type and refuses only the nesting.
/// </item>
/// <item>
/// Plain <c>bit</c> is <c>bit</c>.  <c>format_type(oid, -1)</c> gives <c>"bit"</c> with the quotes, since plain
/// <c>bit</c> means <c>bit(1)</c>, and Npgsql refuses a quote.
/// </item>
/// <item>
/// The single-byte <c>"char"</c> is the server's spelling for now, quotes and all, which Npgsql may refuse.
/// </item>
/// <item>
/// Any other type of <c>pg_catalog</c> is its <c>format_type(oid, -1)</c> without a leading <c>pg_catalog.</c>.  The
/// server qualifies the name when a user type of the same name stands before <c>pg_catalog</c> on the
/// <c>search_path</c> (TD-0050), and the name that Npgsql binds must not depend on the session.
/// </item>
/// <item>
/// A type of any other schema is the schema, a <c>.</c> and the name, neither quoted, as Npgsql takes it.  A <c>.</c>
/// in either part makes the text ambiguous, since it could name another type that exists: there is no name.
/// </item>
/// </list>
/// </remarks>
internal static class PostgresBindNames
{
    private const string Catalog = "pg_catalog";
    private const string CatalogPrefix = Catalog + ".";

    /// <summary>
    /// The text that <c>SQLSRC232</c>'s server line gives for a type that has no candidate name.
    /// </summary>
    public const string NoName =
        "the type has no name that Npgsql could be given: a schema or a type name that holds a '.' cannot be named to "
        + "Npgsql";

    /// <summary>
    /// The name that Npgsql binds the type by, or null when no name can say the type: a schema or a type name that
    /// holds a <c>.</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The rows hold no row for a type that is reached.</exception>
    public static string? Of(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows)
    {
        var row = PostgresTypes.Row(oid, rows);

        if (PostgresTypes.IsArray(row))
        {
            return Of(row.Typelem, rows) is { } element ? PostgresTypeNames.ArrayOf(element) : null;
        }

        if (row.Typtype == "d" && PostgresTypes.Row(row.Typbasetype, rows).Typtype == "d")
        {
            return Of(InnermostBaseOf(row, rows).Oid, rows);
        }

        return row switch
        {
            { Nspname: Catalog, Typname: "bit" } => "bit",
            { Nspname: Catalog, Typname: "char" } => row.FormatType,
            { Nspname: Catalog } => WithoutCatalog(row.FormatType),
            _ when row.Nspname.Contains('.', StringComparison.Ordinal)
                    || row.Typname.Contains('.', StringComparison.Ordinal) => null,
            _ => $"{row.Nspname}.{row.Typname}",
        };
    }

    private static PostgresTypeRow InnermostBaseOf(
        PostgresTypeRow domain,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        var current = domain;
        while (current.Typtype == "d")
        {
            current = PostgresTypes.Row(current.Typbasetype, rows);
        }

        return current;
    }

    private static string WithoutCatalog(string formatType) =>
        formatType.StartsWith(CatalogPrefix, StringComparison.Ordinal)
            ? formatType[CatalogPrefix.Length..]
            : formatType;
}
