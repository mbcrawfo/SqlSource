using System;
using System.Globalization;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Spells a type's <c>name</c> as the sidecar wants it, and as <c>format_type</c> spells a type with its modifier.
/// </summary>
/// <remarks>
/// A type of <c>pg_catalog</c> is its SQL spelling, <c>character varying(100)</c>.  A type of any other schema is the
/// schema and the name, whatever the session's <c>search_path</c>, so that the value does not depend on the
/// session.  An identifier is quoted unless it is lower-case letters, digits and <c>_</c>: a reserved word is not
/// quoted, which <c>docs/tech-debt/TD-0043</c> records.
/// </remarks>
internal static class PostgresTypeNames
{
    private const string Catalog = "pg_catalog";

    /// <summary>The name of a type that is no array.  Facets that the type does not carry are left out.</summary>
    public static string Spell(PostgresTypeRow row, PostgresFacets facets) =>
        row.Nspname == Catalog ? InCatalog(row, facets) : $"{Identifier(row.Nspname)}.{Identifier(row.Typname)}";

    /// <summary>The name of an array: its element's and <c>[]</c>.</summary>
    public static string ArrayOf(string element) => element + "[]";

    /// <summary>
    /// A name as an SQL identifier: bare when it is lower-case letters, digits and <c>_</c> and does not start with
    /// a digit, else in double quotes with a quote doubled.
    /// </summary>
    public static string Identifier(string name) =>
        IsBare(name) ? name : $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    // format_type(oid, -1) of char is "bpchar": only a char with a length is "character".  Of bit it is "bit" with
    // the quotes, since bit is a keyword, so bit is spelled here and not taken from the row.
    private static string InCatalog(PostgresTypeRow row, PostgresFacets facets) =>
        row.Typname switch
        {
            "bpchar" when facets.Length is { } length => Invariant($"character({length})"),
            "varchar" or "varbit" when facets.Length is { } length => Invariant($"{row.FormatType}({length})"),
            "bit" => facets.Length is { } length ? Invariant($"bit({length})") : "bit",
            "numeric" when facets is { Precision: { } precision, Scale: { } scale } => Invariant(
                $"numeric({precision},{scale})"
            ),
            "numeric" when facets.Precision is { } precision => Invariant($"numeric({precision})"),
            "time" or "timetz" or "timestamp" or "timestamptz" when facets.Precision is { } precision => AfterFirstWord(
                row.FormatType,
                precision
            ),
            "interval" when facets.Precision is { } precision => Invariant($"interval({precision})"),
            _ => row.FormatType,
        };

    // "timestamp(3) without time zone": the precision stands after the first word.
    private static string AfterFirstWord(string name, int precision)
    {
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        return space < 0
            ? Invariant($"{name}({precision})")
            : Invariant($"{name[..space]}({precision}){name[space..]}");
    }

    private static bool IsBare(string name) =>
        name.Length > 0
        && !char.IsAsciiDigit(name[0])
        && name.All(static value => char.IsAsciiLetterLower(value) || char.IsAsciiDigit(value) || value == '_');

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
