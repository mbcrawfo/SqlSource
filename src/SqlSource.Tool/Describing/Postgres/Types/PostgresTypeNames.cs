using System;
using System.Globalization;

namespace SqlSource.Tool.Describing.Postgres.Types;

/// <summary>
/// Spells a type's <c>name</c> as the sidecar wants it, and as <c>format_type</c> spells a type with its modifier.
/// </summary>
/// <remarks>
/// A name does not depend on the session's <c>search_path</c>.  A type of <c>pg_catalog</c> is its SQL spelling,
/// <c>character varying(100)</c>, without the <c>pg_catalog.</c> that <c>format_type</c> writes before it when a type
/// of the same name stands before <c>pg_catalog</c> on the <c>search_path</c> (<see cref="WithoutCatalog" />).  A type
/// of any other schema is the schema and the name, whatever the <c>search_path</c>.  An identifier is quoted unless it
/// is lower-case letters, digits and <c>_</c>.  A reserved word is not quoted, as in <c>user.status</c>: the
/// <c>name</c> has been spelled so since phase 3, and the container test
/// <c>DescribeAsync_TypeOfASchemaNamedByAReservedWord_IsSpelledBare</c> pins it.  The name that Npgsql binds is the
/// <c>bindName</c> (<see cref="PostgresBindNames" />), not this one.
/// </remarks>
internal static class PostgresTypeNames
{
    private const string CatalogPrefix = SystemSchemas.Postgres + ".";

    /// <summary>The name of a type that is no array.  Facets that the type does not carry are left out.</summary>
    public static string Spell(PostgresTypeRow row, PostgresFacets facets) =>
        row.Nspname == SystemSchemas.Postgres
            ? InCatalog(row, facets)
            : $"{Identifier(row.Nspname)}.{Identifier(row.Typname)}";

    /// <summary>
    /// The name of a type that is no array, from the text that <c>format_type(oid, modifier)</c> gave.  A type of
    /// <c>pg_catalog</c> is the server's text without its schema.  A type of any other schema is the tool's
    /// <c>schema.name</c> and the tail, what the server's text holds after <c>format_type(oid, -1)</c>, which is the
    /// row's <see cref="PostgresTypeRow.FormatType" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The server's text does not start with the row's <see cref="PostgresTypeRow.FormatType" />.  The rule of the
    /// tail was verified for PostGIS and for a type made over <c>varcharin</c>; a type that breaks it is a bug to
    /// see, not a name to guess.
    /// </exception>
    public static string WithModifier(PostgresTypeRow row, string formatted)
    {
        if (row.Nspname == SystemSchemas.Postgres)
        {
            return WithoutCatalog(formatted);
        }

        return formatted.StartsWith(row.FormatType, StringComparison.Ordinal)
            ? Spell(row, PostgresFacets.None) + formatted[row.FormatType.Length..]
            : throw new InvalidOperationException(
                $"format_type for type {row.Nspname}.{row.Typname} gave '{formatted}', which does not start with "
                    + $"'{row.FormatType}', the text without a modifier: the tool cannot find the modifier's text."
            );
    }

    /// <summary>
    /// The text that <c>format_type</c> gave for a type of <c>pg_catalog</c>, without a leading <c>pg_catalog.</c>.
    /// The server writes the schema when a type of the same name stands before <c>pg_catalog</c> on the session's
    /// <c>search_path</c>, and only then, as <c>pg_catalog.</c> and the text it gives otherwise:
    /// <c>pg_catalog.text</c>, <c>pg_catalog."char"</c>.  A text that does not depend on the session never starts so,
    /// since a type named <c>pg_catalog.x</c> is quoted.
    /// </summary>
    internal static string WithoutCatalog(string formatted) =>
        formatted.StartsWith(CatalogPrefix, StringComparison.Ordinal) ? formatted[CatalogPrefix.Length..] : formatted;

    /// <summary>The name of an array: its element's and <c>[]</c>.</summary>
    public static string ArrayOf(string element) => element + "[]";

    /// <summary>
    /// A name as an SQL identifier: bare when it is lower-case letters, digits and <c>_</c> and does not start with
    /// a digit, else in double quotes with a quote doubled.
    /// </summary>
    public static string Identifier(string name) =>
        PostgresIdentifiers.IsBareName(name) ? name : $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    // format_type(oid, -1) of char is "bpchar": only a char with a length is "character".  Of bit it is "bit" with
    // the quotes, since plain bit means bit(1): the tool writes the length form itself and keeps the server's
    // spelling without one, as it does for bpchar.  Internal for BuiltInTypesTests, which checks that it spells every
    // type of BuiltInTypes.WithModifier.
    internal static string InCatalog(PostgresTypeRow row, PostgresFacets facets)
    {
        var text = WithoutCatalog(row.FormatType);
        return row.Typname switch
        {
            "bpchar" when facets.Length is { } length => string.Create(
                CultureInfo.InvariantCulture,
                $"character({length})"
            ),
            "varchar" or "varbit" when facets.Length is { } length => string.Create(
                CultureInfo.InvariantCulture,
                $"{text}({length})"
            ),
            "bit" when facets.Length is { } length => string.Create(CultureInfo.InvariantCulture, $"bit({length})"),
            "numeric" when facets is { Precision: { } precision, Scale: { } scale } => string.Create(
                CultureInfo.InvariantCulture,
                $"numeric({precision},{scale})"
            ),
            "numeric" when facets.Precision is { } precision => string.Create(
                CultureInfo.InvariantCulture,
                $"numeric({precision})"
            ),
            "time" or "timetz" or "timestamp" or "timestamptz" when facets.Precision is { } precision => AfterFirstWord(
                text,
                precision
            ),
            "interval" when facets.Precision is { } precision => string.Create(
                CultureInfo.InvariantCulture,
                $"interval({precision})"
            ),
            _ => text,
        };
    }

    // "timestamp(3) without time zone": the precision stands after the first word.
    private static string AfterFirstWord(string name, int precision)
    {
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        return space < 0
            ? string.Create(CultureInfo.InvariantCulture, $"{name}({precision})")
            : string.Create(CultureInfo.InvariantCulture, $"{name[..space]}({precision}){name[space..]}");
    }
}
