using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// The keys a type override may name: the names of the types that each engine's base map knows, each engine's with
/// its one-word aliases, and a schema-qualified name of any type.  Each engine reads a key by its own rules,
/// PostgreSQL's by <see cref="TryNormalizePostgres" /> and SQL Server's by <see cref="TryNormalizeSqlServer" />.  The
/// settings check a key here, and the map reads the names of the extensions' types here.  Each map keeps its own rows,
/// and <c>PostgresTypeMapTests.Keys_OfTheMap_AreTheKnownPostgresNamesOfTheSettings</c> and
/// <c>SqlServerTypeMapTests.Keys_OfTheMap_AreTheKnownSqlServerNamesOfTheSettings</c> hold them equal to the names here.
/// A written key is read as an identifier is: the system schema, <c>pg_catalog</c> or <c>sys</c>, is bare in any case
/// or quoted exactly.  A schema that a server reported is not a written key, and is compared with
/// <see cref="SystemSchemas.IsSqlServer" />.
/// </summary>
internal static class TypeKeys
{
    // The start of a part in the escaped form, with the U in either case.
    private const string EscapedOpening = "U&\"";

    private static readonly Dictionary<string, string> Extensions = NamesOf(
        "citext",
        "hstore",
        "ltree",
        "lquery",
        "ltxtquery",
        "cube"
    );

    // Every key of the base map, its refused types and the built-in multiranges included, by itself.
    private static readonly Dictionary<string, string> PostgresNames = NamesOf(
        "bool",
        "int2",
        "int4",
        "int8",
        "float4",
        "float8",
        "numeric",
        "money",
        "text",
        "varchar",
        "bpchar",
        "name",
        "refcursor",
        "char",
        "citext",
        "bytea",
        "date",
        "time",
        "timetz",
        "timestamp",
        "timestamptz",
        "interval",
        "uuid",
        "json",
        "jsonb",
        "jsonpath",
        "xml",
        "inet",
        "cidr",
        "macaddr",
        "macaddr8",
        "bit",
        "varbit",
        "point",
        "line",
        "lseg",
        "box",
        "path",
        "polygon",
        "circle",
        "tsvector",
        "tsquery",
        "oid",
        "xid",
        "cid",
        "regtype",
        "regconfig",
        "xid8",
        "tid",
        "pg_lsn",
        "int2vector",
        "oidvector",
        "hstore",
        "ltree",
        "lquery",
        "ltxtquery",
        "cube",
        "int4range",
        "int8range",
        "numrange",
        "tsrange",
        "tstzrange",
        "daterange",
        "int4multirange",
        "int8multirange",
        "nummultirange",
        "tsmultirange",
        "tstzmultirange",
        "datemultirange",
        "regclass",
        "regproc",
        "regprocedure",
        "regoper",
        "regoperator",
        "regrole",
        "regnamespace",
        "regcollation",
        "regdictionary",
        "pg_snapshot",
        "txid_snapshot",
        "pg_node_tree",
        "record",
        "unknown",
        "void"
    );

    // Every key of the SQL Server map.  rowversion is an alias of timestamp, and sysname is nvarchar's alias type.
    private static readonly Dictionary<string, string> SqlServerNames = NamesOf(
        "bigint",
        "int",
        "smallint",
        "tinyint",
        "bit",
        "decimal",
        "numeric",
        "money",
        "smallmoney",
        "float",
        "real",
        "date",
        "datetime",
        "datetime2",
        "smalldatetime",
        "time",
        "datetimeoffset",
        "char",
        "varchar",
        "text",
        "nchar",
        "nvarchar",
        "ntext",
        "xml",
        "json",
        "binary",
        "varbinary",
        "image",
        "timestamp",
        "uniqueidentifier",
        "sql_variant",
        "vector",
        "hierarchyid",
        "geography",
        "geometry",
        "sysname"
    );

    // The one-word aliases of PostgreSQL's table of data types, by the name each stands for.  "char" is the key of
    // the one-byte type, not an alias of bpchar.
    private static readonly Dictionary<string, string> PostgresAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["integer"] = "int4",
        ["int"] = "int4",
        ["smallint"] = "int2",
        ["bigint"] = "int8",
        ["real"] = "float4",
        ["float"] = "float8",
        ["boolean"] = "bool",
        ["decimal"] = "numeric",
        ["character"] = "bpchar",
    };

    // SQL Server's one-word synonyms of its types, by the name each stands for.
    private static readonly Dictionary<string, string> SqlServerAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["integer"] = "int",
        ["character"] = "char",
        ["dec"] = "decimal",
        ["rowversion"] = "timestamp",
    };

    /// <summary>The names of the PostgreSQL types the base map knows, in lower case.</summary>
    public static IReadOnlyCollection<string> KnownPostgresNames => PostgresNames.Keys;

    /// <summary>The names of the SQL Server types the base map knows, in lower case.</summary>
    public static IReadOnlyCollection<string> KnownSqlServerNames => SqlServerNames.Keys;

    /// <summary>
    /// Whether <paramref name="internalName" /> is the name of a type of an extension that the base map knows:
    /// <c>citext</c>, <c>hstore</c>, <c>ltree</c>, <c>lquery</c>, <c>ltxtquery</c> or <c>cube</c>, ignoring case.
    /// Such a type is matched by its name in any schema.
    /// </summary>
    public static bool IsExtension(string internalName) => Extensions.ContainsKey(internalName);

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
    /// <c>[]</c>, with a name the map does not know written as <see cref="WritePart" /> writes it:
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
    /// in the quoted form <see cref="WritePart" /> writes for its text, so <c>public.U&amp;"st\0061tus"</c> gives
    /// <c>public."status"</c> and <c>public.U&amp;"a\000ab"</c> gives <c>public.U&amp;"a\000Ab"</c>.
    /// </para>
    /// </summary>
    public static bool TryNormalizePostgres(ReadOnlySpan<char> key, out string normalized)
    {
        normalized = string.Empty;
        return TrySplit(key, out var schema, out var name, out var isArray)
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
        return TrySplit(key, out var schema, out var name, out var isArray)
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
        if (!TrySplit(key, out var schema, out var name, out var isArray))
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
            return IsQuoted(name)
                ? TryNormalizeQuoted(TextOf(name).ToString(), isArray, out normalized)
                : TryNormalizePlain(name.ToString(), isArray, out normalized);
        }

        if (!IsSchema(schema, SystemSchemas.Postgres))
        {
            normalized = UsersKey(key, schema, name, isArray);
        }
        else if (IsQuoted(name))
        {
            normalized = NormalizeQuotedCatalogName(name, isArray);
        }
        else if (!isArray && name.Length > 1 && name[0] == '_')
        {
            // Every type of pg_catalog whose name starts with _ is an array, of the type named by the rest.
            var ofElement = name.Slice(1).ToString();
            normalized = (PostgresNames.TryGetValue(ofElement, out var knownElement) ? knownElement : ofElement) + "[]";
        }
        else
        {
            // A name the map knows is given in lower case, as it is without the schema; any other stays as written.
            var text = name.ToString();
            if (PostgresNames.TryGetValue(text, out var known))
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
            var text = TextOf(name).ToString();
            if (IsQuoted(name))
            {
                if (!TryGetExactName(SqlServerNames, text, out var exact))
                {
                    return false;
                }

                normalized = exact;
                return true;
            }

            if (!SqlServerNames.TryGetValue(text, out var known) && !SqlServerAliases.TryGetValue(text, out known))
            {
                return false;
            }

            normalized = known;
            return true;
        }

        if (IsSchema(schema, SystemSchemas.SqlServer))
        {
            normalized = IsQuoted(name) ? NormalizeQuotedSqlServerName(name) : NormalizePlainSqlServerName(name);
            return true;
        }

        if (IsSchema(schema, SystemSchemas.Postgres))
        {
            return false;
        }

        normalized = UsersKey(key, schema, name, isArray: false);
        return true;
    }

    /// <summary>
    /// Splits a key as it is written, which is what <see cref="TryNormalizePostgres" /> gives and
    /// <see cref="WritePart" /> builds, into its parts, quotes included: <c>"a.b".c[]</c> gives <c>"a.b"</c>, <c>c</c>
    /// and true, and <c>U&amp;"a.b".c</c> gives <c>U&amp;"a.b"</c>, whose escapes are checked here.
    /// <paramref name="schema" /> is empty for an unqualified key.  Returns false for text that is not
    /// <c>[part "."] part ["[]"]</c>, the grammar of <see cref="TryNormalizePostgres" />.
    /// </summary>
    public static bool TrySplit(
        ReadOnlySpan<char> key,
        out ReadOnlySpan<char> schema,
        out ReadOnlySpan<char> name,
        out bool isArray
    )
    {
        schema = default;
        name = default;
        isArray = false;

        // A key is read from the left, so that the brackets of a quoted name are not the key's own.
        if (!TryReadPart(key, 0, out var end))
        {
            return false;
        }

        name = key.Slice(0, end);
        if (end < key.Length && key[end] == '.')
        {
            var start = end + 1;
            if (!TryReadPart(key, start, out end))
            {
                return false;
            }

            schema = name;
            name = key.Slice(start, end - start);
        }

        var rest = key.Slice(end);
        isArray = rest.SequenceEqual("[]".AsSpan());
        return isArray || rest.IsEmpty;
    }

    /// <summary>
    /// Whether <paramref name="part" />, as <see cref="TrySplit" /> gives it, is in quotes: <c>"x"</c>, or
    /// <c>U&amp;"x"</c> in the escaped form, with <c>u</c> allowed.
    /// </summary>
    public static bool IsQuoted(ReadOnlySpan<char> part) => (!part.IsEmpty && part[0] == '"') || IsEscaped(part);

    /// <summary>
    /// The text of <paramref name="part" />, as <see cref="TrySplit" /> gives it, without the quotes of a quoted one
    /// and with each <c>""</c> in it a single <c>"</c>, and each escape of an escaped one the character it names.
    /// Allocates only for a part that holds <c>""</c>, or <c>\</c> in an escaped one.  A <c>\</c> that is not a valid
    /// escape, which <see cref="TrySplit" /> does not give, is kept as a character.
    /// </summary>
    public static ReadOnlySpan<char> TextOf(ReadOnlySpan<char> part)
    {
        if (!IsQuoted(part))
        {
            return part;
        }

        var isEscaped = IsEscaped(part);
        var opening = isEscaped ? EscapedOpening.Length : 1;
        if (part.Length <= opening)
        {
            return default;
        }

        var inside = part.Slice(opening, part.Length - opening - 1);
        if (inside.IndexOf('"') < 0 && (!isEscaped || inside.IndexOf('\\') < 0))
        {
            return inside;
        }

        // The span is over the one array that is made, so the text costs a single allocation.  No step writes more
        // characters than it reads, so the text is never longer than the inside.
        var text = new char[inside.Length];
        var length = 0;
        var index = 0;
        while (index < inside.Length)
        {
            var character = inside[index];
            if (
                isEscaped
                && character == '\\'
                && TryReadEscape(inside, index, out var first, out var second, out var next)
            )
            {
                text[length++] = first;
                if (second != '\0')
                {
                    text[length++] = second;
                }

                index = next;
                continue;
            }

            text[length++] = character;

            // A quote is doubled in a part that is well formed, and the second of the two is skipped.
            index += character == '"' ? 2 : 1;
        }

        return new ReadOnlySpan<char>(text, 0, length);
    }

    /// <summary>
    /// Writes <paramref name="text" /> as a part of a key: as it is when it is not empty and holds only letters,
    /// digits, <c>_</c> and <c>$</c>, none of them upper case, since a plain part is read in lower case; otherwise in
    /// double quotes with each <c>"</c> doubled, or in the escaped form when the quotes would not keep it.  A value
    /// written in MSBuild is trimmed, and the compiler cuts it at <c>;</c> and <c>#</c>, and a marker is one line, so
    /// a text with a control character other than NUL, a white space other than a lone space, a <c>;</c> or a
    /// <c>#</c> is written <c>U&amp;"a\000Ab"</c>, with <c>\XXXX</c> for each such character, <c>\\</c> for a
    /// <c>\</c> and <c>""</c> for a <c>"</c>.  A NUL stays as it is: a PostgreSQL name cannot hold it, and
    /// <c>\0000</c> is no escape.
    /// </summary>
    public static string WritePart(string text)
    {
        var isPlain = text.Length > 0 && text.All(character => IsPlainCharacter(character) && !char.IsUpper(character));
        return isPlain ? text : Quote(text);
    }

    // A part in double quotes, with each quote doubled, or in the escaped form when the quotes would not keep it.
    private static string Quote(string text)
    {
        if (!NeedsEscape(text))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        var written = new StringBuilder(EscapedOpening, text.Length + EscapedOpening.Length + 8);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (MustEscape(text, index))
            {
                _ = written.Append('\\').Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            }
            else if (character is '\\' or '"')
            {
                _ = written.Append(character).Append(character);
            }
            else
            {
                _ = written.Append(character);
            }
        }

        return written.Append('"').ToString();
    }

    private static bool NeedsEscape(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (MustEscape(text, index))
            {
                return true;
            }
        }

        return false;
    }

    // A character that a quoted part would not keep: the trim reads any white space but U+0020 as a space and a run
    // of them as one, the compiler cuts at ; and #, and a control character may end the line, but a NUL, which no
    // PostgreSQL name holds and \0000 cannot write.
    private static bool MustEscape(string text, int index)
    {
        var character = text[index];
        return (character != '\0' && char.IsControl(character))
            || (char.IsWhiteSpace(character) && character != ' ')
            || (character == ' ' && index > 0 && text[index - 1] == ' ')
            || character is ';' or '#';
    }

    // The key of a type of a user's schema, which both engines read alike: as written, or rewritten when a part is
    // escaped.
    private static string UsersKey(
        ReadOnlySpan<char> key,
        ReadOnlySpan<char> schema,
        ReadOnlySpan<char> name,
        bool isArray
    ) => IsEscaped(schema) || IsEscaped(name) ? Rewrite(schema, name, isArray) : key.ToString();

    // A qualified key as written, except that an escaped part is in the quoted form of its text.
    private static string Rewrite(ReadOnlySpan<char> schema, ReadOnlySpan<char> name, bool isArray)
    {
        var key = AsWritten(schema) + "." + AsWritten(name);
        return isArray ? key + "[]" : key;
    }

    // A part as a key keeps it: as written, except that an escaped one is in the quoted form of its text.
    private static string AsWritten(ReadOnlySpan<char> part) =>
        IsEscaped(part) ? Quote(TextOf(part).ToString()) : part.ToString();

    private static bool TryNormalizePlain(string text, bool isArray, out string normalized)
    {
        normalized = string.Empty;
        if (PostgresNames.TryGetValue(text, out var known) || PostgresAliases.TryGetValue(text, out known))
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
        if (TryGetExactName(PostgresNames, text, out var known))
        {
            normalized = isArray ? known + "[]" : known;
            return true;
        }

        if (text.Length > 1 && text[0] == '_' && TryGetExactName(PostgresNames, text.Substring(1), out var element))
        {
            normalized = isArray ? "_" + element + "[]" : element + "[]";
            return true;
        }

        return false;
    }

    private static string NormalizeQuotedCatalogName(ReadOnlySpan<char> name, bool isArray)
    {
        var text = TextOf(name).ToString();
        if (TryGetExactName(PostgresNames, text, out var known))
        {
            return isArray ? known + "[]" : known;
        }

        if (text.Length > 1 && text[0] == '_')
        {
            var ofElement = text.Substring(1);
            var isKnownElement = TryGetExactName(PostgresNames, ofElement, out var knownElement);

            // With [], the catalog's name is kept with its _, as a plain one is.
            if (isArray)
            {
                return isKnownElement ? "_" + knownElement + "[]" : AsWritten(name) + "[]";
            }

            return (isKnownElement ? knownElement : WritePart(ofElement)) + "[]";
        }

        return isArray ? AsWritten(name) + "[]" : AsWritten(name);
    }

    // A plain name of sys: a name SQL Server's map knows in lower case, and any other as written.
    private static string NormalizePlainSqlServerName(ReadOnlySpan<char> name)
    {
        var text = name.ToString();
        return SqlServerNames.TryGetValue(text, out var known) ? known : text;
    }

    // A quoted name of sys: the name SQL Server's map knows that it holds exactly, or the name as a key keeps it.
    private static string NormalizeQuotedSqlServerName(ReadOnlySpan<char> name) =>
        TryGetExactName(SqlServerNames, TextOf(name).ToString(), out var known) ? known : AsWritten(name);

    // Whether text is a name of names exactly, in its case: a quoted name has no other spelling.
    private static bool TryGetExactName(Dictionary<string, string> names, string text, out string known) =>
        names.TryGetValue(text, out known) && string.Equals(known, text, StringComparison.Ordinal);

    // Whether a schema as a user wrote it in a key is the engine's system schema, pg_catalog or sys: an identifier is
    // read as an engine reads it, so a bare one in any case and a quoted one exactly.  A schema that a server reported
    // is another rule: SystemSchemas.IsSqlServer compares it exactly.
    private static bool IsSchema(ReadOnlySpan<char> schema, string systemSchema) =>
        IsQuoted(schema)
            ? TextOf(schema).Equals(systemSchema.AsSpan(), StringComparison.Ordinal)
            : schema.Equals(systemSchema.AsSpan(), StringComparison.OrdinalIgnoreCase);

    // The catalog's name of an array of a type the map knows, _int4, gives that type's name.
    private static bool TryReadArrayName(string part, out string element)
    {
        element = string.Empty;
        return part.Length > 1 && part[0] == '_' && PostgresNames.TryGetValue(part.Substring(1), out element);
    }

    /// <summary>
    /// Reads the part of a key that starts at <paramref name="start" />, plain, quoted or escaped, and gives the index
    /// after it.  A quoted part holds at least one character, and ends at the first quote that is not doubled; an
    /// escaped one holds only valid escapes.  False for an empty part, or a quoted one that does not close.
    /// </summary>
    public static bool TryReadPart(ReadOnlySpan<char> key, int start, out int end)
    {
        end = start;
        if (start >= key.Length)
        {
            return false;
        }

        var isEscaped = IsEscaped(key.Slice(start));
        if (key[start] != '"' && !isEscaped)
        {
            while (end < key.Length && IsPlainCharacter(key[end]))
            {
                end++;
            }

            return end > start;
        }

        var opening = isEscaped ? start + EscapedOpening.Length - 1 : start;
        end = opening + 1;
        while (end < key.Length)
        {
            if (isEscaped && key[end] == '\\')
            {
                if (!TryReadEscape(key, end, out _, out _, out end))
                {
                    return false;
                }
            }
            else if (key[end] != '"')
            {
                end++;
            }
            else if (end + 1 < key.Length && key[end + 1] == '"')
            {
                end += 2;
            }
            else
            {
                end++;
                return end - opening > 2;
            }
        }

        return false;
    }

    // Whether the part starts as the escaped form does, U&" in either case of the U.
    private static bool IsEscaped(ReadOnlySpan<char> part) =>
        part.Length >= EscapedOpening.Length && part[0] is 'U' or 'u' && part[1] == '&' && part[2] == '"';

    // Reads the escape at text[at], a backslash that is followed by a second one, by 4 hex digits, or by + and 6.  A
    // character outside the BMP is given as its surrogates, in first and second, and a high surrogate must be followed
    // at once by an escape of a low one.  The second is '\0' for a single character, which no escape can name.  next is
    // the index after what was read.
    private static bool TryReadEscape(ReadOnlySpan<char> text, int at, out char first, out char second, out int next)
    {
        first = '\0';
        second = '\0';
        if (!TryReadCodePoint(text, at, out var codePoint, out next))
        {
            return false;
        }

        if (codePoint is >= 0xDC00 and <= 0xDFFF)
        {
            return false;
        }

        if (codePoint is >= 0xD800 and <= 0xDBFF)
        {
            // The high surrogate waits for its low one.
            if (!TryReadCodePoint(text, next, out var low, out next) || low is < 0xDC00 or > 0xDFFF)
            {
                return false;
            }

            first = (char)codePoint;
            second = (char)low;
            return true;
        }

        if (codePoint > 0xFFFF)
        {
            first = (char)(0xD800 + ((codePoint - 0x10000) >> 10));
            second = (char)(0xDC00 + ((codePoint - 0x10000) & 0x3FF));
            return true;
        }

        first = (char)codePoint;
        return true;
    }

    // Reads one escape to its code point, from U+0001 to U+10FFFF, a surrogate included.
    private static bool TryReadCodePoint(ReadOnlySpan<char> text, int at, out int codePoint, out int next)
    {
        codePoint = 0;
        next = at;
        if (at >= text.Length || text[at] != '\\' || at + 1 >= text.Length)
        {
            return false;
        }

        if (text[at + 1] == '\\')
        {
            codePoint = '\\';
            next = at + 2;
            return true;
        }

        var isLong = text[at + 1] == '+';
        var start = at + (isLong ? 2 : 1);
        var digits = isLong ? 6 : 4;
        if (start + digits > text.Length)
        {
            return false;
        }

        for (var index = start; index < start + digits; index++)
        {
            var value = HexValue(text[index]);
            if (value < 0)
            {
                return false;
            }

            codePoint = (codePoint << 4) | value;
        }

        next = start + digits;
        return codePoint is >= 1 and <= 0x10FFFF;
    }

    private static int HexValue(char character) =>
        character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'a' and <= 'f' => character - 'a' + 10,
            >= 'A' and <= 'F' => character - 'A' + 10,
            _ => -1,
        };

    /// <summary>Whether a character may stand in a plain part: a letter, a digit, <c>_</c> or <c>$</c>.</summary>
    public static bool IsPlainCharacter(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';

    private static Dictionary<string, string> NamesOf(params string[] names)
    {
        var map = new Dictionary<string, string>(names.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            map[name] = name;
        }

        return map;
    }
}
