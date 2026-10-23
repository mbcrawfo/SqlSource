using System;
using System.Collections.Generic;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// The keys a type override may name: the names of the types that each engine's base map knows, each engine's with
/// its one-word aliases, and a schema-qualified name of any type.  Each engine reads a key by its own rules,
/// PostgreSQL's by <see cref="TryNormalizePostgres" /> and SQL Server's by <see cref="TryNormalizeSqlServer" />, and
/// the settings check a key here.  The names and the aliases are <see cref="TypeKeyNames" />'s, and the grammar of a
/// key's parts is <see cref="TypeKeyText" />'s.  A written key is read as an identifier is: the system schema,
/// <c>pg_catalog</c> or <c>sys</c>, is bare in any case or quoted exactly.  A schema that a server reported is not a
/// written key, and is compared with <see cref="SystemSchemas.IsSqlServer" />.
/// </summary>
internal static class TypeKeys
{
    /// <summary>
    /// Reads a key, <c>[part "."] part ["[]"]</c>, where a part is plain, letters, digits, <c>_</c> and <c>$</c>, or
    /// quoted, in double quotes with <c>""</c> for a quote, which can hold any character and keeps its case.  A
    /// quoted part may be written in PostgreSQL's Unicode-escape form, <c>U&amp;"a\000Ab"</c>, where <c>\\</c> is a
    /// backslash, <c>\XXXX</c> and <c>\+XXXXXX</c> are the character with that hex code, and a character outside the
    /// BMP is either one escape or the two of its surrogates; it has no <c>UESCAPE</c> clause.  It holds the same text
    /// as the part in plain quotes, so it is a key by the same rules, and an escape that is not valid makes the key not
    /// valid.  Returns false for anything else, with an empty <paramref name="normalized" />.
    /// <para>
    /// An unqualified part must be a name the base map knows, an alias of one, or <c>_</c> and a name it knows, and
    /// is given as the name in lower case: <c>integer[]</c> gives <c>int4[]</c>.  A quoted one must hold the name
    /// exactly, in lower case, and has no alias: <c>"char"</c> gives <c>char</c>, and <c>"Int4"</c> and
    /// <c>"integer"</c> are not keys.  <c>_</c> and a name it knows is the catalog's name of an array, and is given as
    /// the array's key, so <c>_int4</c> and <c>"_int4"</c> give <c>int4[]</c>, and <c>_int4[]</c> stays as written.
    /// </para>
    /// <para>
    /// <c>pg_catalog.</c>, plain in any case or quoted in lower case, is taken off and what is left is not checked,
    /// but that every name of <c>pg_catalog</c> that starts with <c>_</c> is an array's, so <c>pg_catalog.newtype</c>
    /// gives <c>newtype</c>, <c>pg_catalog._int4</c> gives <c>int4[]</c> and <c>pg_catalog._pg_class</c> gives
    /// <c>pg_class[]</c>.  A plain name there that the map knows, or with <c>[]</c> <c>_</c> and one, is given in
    /// lower case, as it is without the schema, so <c>pg_catalog.INT4</c> gives <c>int4</c>; any other plain name
    /// stays as written, and has no alias: <c>pg_catalog.Integer</c> gives <c>Integer</c>.  A quoted name there that
    /// holds a name the map knows is that name, and one that holds <c>_</c> and a name is that name's key with
    /// <c>[]</c>, with a name the map does not know written as <see cref="TypeKeyText.WritePart" /> writes it:
    /// <c>pg_catalog."_odd"</c> gives <c>odd[]</c> and <c>pg_catalog."_Odd"</c> gives <c>"Odd"[]</c>, and with
    /// <c>[]</c> a name that holds <c>_</c> and a name the map knows is <c>_</c>, that name and <c>[]</c>, as a plain
    /// one is: <c>pg_catalog."_int4"[]</c> gives <c>_int4[]</c>.  Any other quoted name stays as written.
    /// </para>
    /// <para>
    /// Any other qualified key is given as written, unchecked: <c>public._tag</c> is not <c>public.tag[]</c>, since a
    /// type of another schema may be named with a <c>_</c>, and the lookup finds an array outside <c>pg_catalog</c>
    /// by its element's key alone.
    /// </para>
    /// <para>
    /// An escaped part is read as the quoted part of its text, and where a quoted part is kept as written it is given
    /// in the quoted form <see cref="TypeKeyText.WritePart" /> writes for its text, so
    /// <c>public.U&amp;"st\0061tus"</c> gives <c>public."status"</c> and <c>public.U&amp;"a\000ab"</c> gives
    /// <c>public.U&amp;"a\000Ab"</c>.
    /// </para>
    /// </summary>
    public static bool TryNormalizePostgres(ReadOnlySpan<char> key, out string normalized)
    {
        normalized = string.Empty;
        return TypeKeyText.TrySplit(key, out var schema, out var name, out var isArray)
            && TryNormalizePostgresParts(key, schema, name, isArray, out normalized);
    }

    /// <summary>
    /// Reads a key by SQL Server's rules, by the grammar of <see cref="TryNormalizePostgres" />.  An array is not a
    /// key: SQL Server has none.  An unqualified plain part must be a name SQL Server's map knows or one of its
    /// aliases, <c>integer</c>, <c>character</c>, <c>dec</c> and <c>rowversion</c>, ignoring case, and is given as the
    /// name in lower case: <c>rowversion</c> gives <c>timestamp</c>.  A quoted one must hold the name exactly, in lower
    /// case, and has no alias.
    /// <para>
    /// <c>sys.</c>, plain in any case or quoted in lower case, is SQL Server's system schema, and is taken off as
    /// <see cref="TryNormalizePostgres" /> takes off <c>pg_catalog.</c>: what is left is not checked, a plain name the
    /// map knows is given in lower case and any other as written, with no alias, and a quoted name that holds a name
    /// the map knows is that name and any other is kept as written.  <c>pg_catalog.</c> is not a key, and any other
    /// qualified key is what <see cref="TryNormalizePostgres" /> gives.  Returns false with an empty
    /// <paramref name="normalized" /> for anything else.
    /// </para>
    /// </summary>
    public static bool TryNormalizeSqlServer(ReadOnlySpan<char> key, out string normalized)
    {
        normalized = string.Empty;
        return TypeKeyText.TrySplit(key, out var schema, out var name, out var isArray)
            && TryNormalizeSqlServerParts(key, schema, name, isArray, out normalized);
    }

    /// <summary>
    /// Reads a key by each engine's rules, as <see cref="TryNormalizePostgres" /> and
    /// <see cref="TryNormalizeSqlServer" /> do, with null for an engine that does not read it.  The key is split once,
    /// and a key of a user's schema, which both engines read alike, is read once.  Returns whether either engine reads
    /// it.
    /// </summary>
    public static bool TryNormalizeEach(ReadOnlySpan<char> key, out string? postgreSql, out string? sqlServer)
    {
        postgreSql = null;
        sqlServer = null;
        if (!TypeKeyText.TrySplit(key, out var schema, out var name, out var isArray))
        {
            return false;
        }

        if (!schema.IsEmpty && !IsSchema(schema, SystemSchemas.Postgres) && !IsSchema(schema, SystemSchemas.SqlServer))
        {
            postgreSql = UsersKey(key, schema, name, isArray);
            sqlServer = isArray ? null : postgreSql;
            return true;
        }

        if (TryNormalizePostgresParts(key, schema, name, isArray, out var postgreSqlKey))
        {
            postgreSql = postgreSqlKey;
        }

        if (TryNormalizeSqlServerParts(key, schema, name, isArray, out var sqlServerKey))
        {
            sqlServer = sqlServerKey;
        }

        return postgreSql is not null || sqlServer is not null;
    }

    // PostgreSQL's reading of a key that TrySplit split.
    private static bool TryNormalizePostgresParts(
        ReadOnlySpan<char> key,
        ReadOnlySpan<char> schema,
        ReadOnlySpan<char> name,
        bool isArray,
        out string normalized
    )
    {
        normalized = string.Empty;
        if (schema.IsEmpty)
        {
            return TypeKeyText.IsQuoted(name)
                ? TryNormalizeQuoted(TypeKeyText.TextOf(name).ToString(), isArray, out normalized)
                : TryNormalizePlain(name.ToString(), isArray, out normalized);
        }

        if (!IsSchema(schema, SystemSchemas.Postgres))
        {
            normalized = UsersKey(key, schema, name, isArray);
        }
        else if (TypeKeyText.IsQuoted(name))
        {
            normalized = NormalizeQuotedCatalogName(name, isArray);
        }
        else if (!isArray && name.Length > 1 && name[0] == '_')
        {
            // Every type of pg_catalog whose name starts with _ is an array, of the type named by the rest.
            var ofElement = name.Slice(1).ToString();
            normalized =
                (TypeKeyNames.PostgresNames.TryGetValue(ofElement, out var knownElement) ? knownElement : ofElement)
                + "[]";
        }
        else
        {
            // A name the map knows is given in lower case, as it is without the schema; any other stays as written.
            var text = name.ToString();
            if (TypeKeyNames.PostgresNames.TryGetValue(text, out var known))
            {
                text = known;
            }
            else if (isArray && TryReadArrayName(text, out var element))
            {
                text = "_" + element;
            }

            normalized = isArray ? text + "[]" : text;
        }

        return true;
    }

    // SQL Server's reading of a key that TrySplit split.
    private static bool TryNormalizeSqlServerParts(
        ReadOnlySpan<char> key,
        ReadOnlySpan<char> schema,
        ReadOnlySpan<char> name,
        bool isArray,
        out string normalized
    )
    {
        normalized = string.Empty;
        if (isArray)
        {
            return false;
        }

        if (schema.IsEmpty)
        {
            var text = TypeKeyText.TextOf(name).ToString();
            if (TypeKeyText.IsQuoted(name))
            {
                if (!TryGetExactName(TypeKeyNames.SqlServerNames, text, out var exact))
                {
                    return false;
                }

                normalized = exact;
                return true;
            }

            if (
                !TypeKeyNames.SqlServerNames.TryGetValue(text, out var known)
                && !TypeKeyNames.SqlServerAliases.TryGetValue(text, out known)
            )
            {
                return false;
            }

            normalized = known;
            return true;
        }

        if (IsSchema(schema, SystemSchemas.SqlServer))
        {
            normalized = TypeKeyText.IsQuoted(name)
                ? NormalizeQuotedSqlServerName(name)
                : NormalizePlainSqlServerName(name);
            return true;
        }

        if (IsSchema(schema, SystemSchemas.Postgres))
        {
            return false;
        }

        normalized = UsersKey(key, schema, name, isArray: false);
        return true;
    }

    // The key of a type of a user's schema, which both engines read alike: as written, or rewritten when a part is
    // escaped.
    private static string UsersKey(
        ReadOnlySpan<char> key,
        ReadOnlySpan<char> schema,
        ReadOnlySpan<char> name,
        bool isArray
    ) => TypeKeyText.IsEscaped(schema) || TypeKeyText.IsEscaped(name) ? Rewrite(schema, name, isArray) : key.ToString();

    // A qualified key as written, except that an escaped part is in the quoted form of its text.
    private static string Rewrite(ReadOnlySpan<char> schema, ReadOnlySpan<char> name, bool isArray)
    {
        var key = AsWritten(schema) + "." + AsWritten(name);
        return isArray ? key + "[]" : key;
    }

    // A part as a key keeps it: as written, except that an escaped one is in the quoted form of its text.
    private static string AsWritten(ReadOnlySpan<char> part) =>
        TypeKeyText.IsEscaped(part) ? TypeKeyText.Quote(TypeKeyText.TextOf(part).ToString()) : part.ToString();

    private static bool TryNormalizePlain(string text, bool isArray, out string normalized)
    {
        normalized = string.Empty;
        if (
            TypeKeyNames.PostgresNames.TryGetValue(text, out var known)
            || TypeKeyNames.PostgresAliases.TryGetValue(text, out known)
        )
        {
            normalized = isArray ? known + "[]" : known;
            return true;
        }

        if (TryReadArrayName(text, out var element))
        {
            normalized = isArray ? "_" + element + "[]" : element + "[]";
            return true;
        }

        return false;
    }

    // A quoted name is exact, so it names a type of the map only in lower case, and has no alias.
    private static bool TryNormalizeQuoted(string text, bool isArray, out string normalized)
    {
        normalized = string.Empty;
        if (TryGetExactName(TypeKeyNames.PostgresNames, text, out var known))
        {
            normalized = isArray ? known + "[]" : known;
            return true;
        }

        if (
            text.Length > 1
            && text[0] == '_'
            && TryGetExactName(TypeKeyNames.PostgresNames, text.Substring(1), out var element)
        )
        {
            normalized = isArray ? "_" + element + "[]" : element + "[]";
            return true;
        }

        return false;
    }

    private static string NormalizeQuotedCatalogName(ReadOnlySpan<char> name, bool isArray)
    {
        var text = TypeKeyText.TextOf(name).ToString();
        if (TryGetExactName(TypeKeyNames.PostgresNames, text, out var known))
        {
            return isArray ? known + "[]" : known;
        }

        if (text.Length > 1 && text[0] == '_')
        {
            var ofElement = text.Substring(1);
            var isKnownElement = TryGetExactName(TypeKeyNames.PostgresNames, ofElement, out var knownElement);

            // With [], the catalog's name is kept with its _, as a plain one is.
            if (isArray)
            {
                return isKnownElement ? "_" + knownElement + "[]" : AsWritten(name) + "[]";
            }

            return (isKnownElement ? knownElement : TypeKeyText.WritePart(ofElement)) + "[]";
        }

        return isArray ? AsWritten(name) + "[]" : AsWritten(name);
    }

    // A plain name of sys: a name SQL Server's map knows in lower case, and any other as written.
    private static string NormalizePlainSqlServerName(ReadOnlySpan<char> name)
    {
        var text = name.ToString();
        return TypeKeyNames.SqlServerNames.TryGetValue(text, out var known) ? known : text;
    }

    // A quoted name of sys: the name SQL Server's map knows that it holds exactly, or the name as a key keeps it.
    private static string NormalizeQuotedSqlServerName(ReadOnlySpan<char> name) =>
        TryGetExactName(TypeKeyNames.SqlServerNames, TypeKeyText.TextOf(name).ToString(), out var known)
            ? known
            : AsWritten(name);

    // Whether text is a name of names exactly, in its case: a quoted name has no other spelling.
    private static bool TryGetExactName(Dictionary<string, string> names, string text, out string known) =>
        names.TryGetValue(text, out known) && string.Equals(known, text, StringComparison.Ordinal);

    // Whether a schema as a user wrote it in a key is the engine's system schema, pg_catalog or sys: an identifier is
    // read as an engine reads it, so a bare one in any case and a quoted one exactly.  A schema that a server reported
    // is another rule: SystemSchemas.IsSqlServer compares it exactly.
    private static bool IsSchema(ReadOnlySpan<char> schema, string systemSchema) =>
        TypeKeyText.IsQuoted(schema)
            ? TypeKeyText.TextOf(schema).Equals(systemSchema.AsSpan(), StringComparison.Ordinal)
            : schema.Equals(systemSchema.AsSpan(), StringComparison.OrdinalIgnoreCase);

    // The catalog's name of an array of a type the map knows, _int4, gives that type's name.
    private static bool TryReadArrayName(string part, out string element)
    {
        element = string.Empty;
        return part.Length > 1
            && part[0] == '_'
            && TypeKeyNames.PostgresNames.TryGetValue(part.Substring(1), out element);
    }
}
