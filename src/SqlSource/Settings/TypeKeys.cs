using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Settings;

/// <summary>
/// The keys a type override may name: the names of the PostgreSQL types that the base map knows, their one-word
/// aliases, and a schema-qualified name of any type.  The settings check a key here, and the map reads the names of
/// the extensions' types here.  The map keeps its own rows, and
/// <c>PostgresTypeMapTests.Keys_OfTheMap_AreTheKnownNamesOfTheSettings</c> holds them equal to the names here.
/// </summary>
internal static class TypeKeys
{
    private const string SystemSchema = "pg_catalog";

    private static readonly Dictionary<string, string> Extensions = NamesOf(
        "citext",
        "hstore",
        "ltree",
        "lquery",
        "ltxtquery",
        "cube"
    );

    // Every key of the base map, its refused types and the built-in multiranges included, by itself.
    private static readonly Dictionary<string, string> Names = NamesOf(
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

    // The one-word aliases of PostgreSQL's table of data types, by the name each stands for.  "char" is the key of
    // the one-byte type, not an alias of bpchar.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>The names of the types the base map knows, in lower case.</summary>
    public static IReadOnlyCollection<string> KnownNames => Names.Keys;

    /// <summary>
    /// Whether <paramref name="internalName" /> is the name of a type of an extension that the base map knows:
    /// <c>citext</c>, <c>hstore</c>, <c>ltree</c>, <c>lquery</c>, <c>ltxtquery</c> or <c>cube</c>, ignoring case.
    /// Such a type is matched by its name in any schema.
    /// </summary>
    public static bool IsExtension(string internalName) => Extensions.ContainsKey(internalName);

    /// <summary>
    /// Reads a key, <c>[part "."] part ["[]"]</c>, where a part is plain, letters, digits, <c>_</c> and <c>$</c>, or
    /// quoted, in double quotes with <c>""</c> for a quote, which can hold any character and keeps its case.  An
    /// unqualified part must be a name the base map knows, an alias of one, or <c>_</c> and a name it knows, and is
    /// given as the name in lower case: <c>integer[]</c> gives <c>int4[]</c>.  A quoted one must hold the name
    /// exactly, in lower case, and has no alias: <c>"char"</c> gives <c>char</c>, and <c>"Int4"</c> and
    /// <c>"integer"</c> are not keys.  <c>_</c> and a name it knows is the catalog's name of an array, and is given as
    /// the array's key, so <c>_int4</c> and <c>"_int4"</c> give <c>int4[]</c>, and <c>_int4[]</c> stays as written.
    /// <c>pg_catalog.</c>, plain in any case or quoted in lower case, is taken off and what is left is not checked,
    /// but that every name of <c>pg_catalog</c> that starts with <c>_</c> is an array's, so <c>pg_catalog.newtype</c>
    /// gives <c>newtype</c>, <c>pg_catalog._int4</c> gives <c>int4[]</c> and <c>pg_catalog._pg_class</c> gives
    /// <c>pg_class[]</c>.  A quoted name there that holds a name the map knows is that name, and one that holds
    /// <c>_</c> and a name is that name's key with <c>[]</c>, with a name the map does not know written as
    /// <see cref="WritePart" /> writes it: <c>pg_catalog."_odd"</c> gives <c>odd[]</c> and <c>pg_catalog."_Odd"</c>
    /// gives <c>"Odd"[]</c>, and with <c>[]</c> a name that holds <c>_</c> and a name the map knows is <c>_</c>, that
    /// name and <c>[]</c>, as a plain one is: <c>pg_catalog."_int4"[]</c> gives <c>_int4[]</c>.  Any other quoted name
    /// stays as written.  Any other
    /// qualified key is given as written, unchecked: <c>public._tag</c> is not <c>public.tag[]</c>, since a type of
    /// another schema may be named with a <c>_</c> (TD-0059).  Returns false for anything else, with an empty
    /// <paramref name="normalized" />.
    /// </summary>
    public static bool TryNormalize(ReadOnlySpan<char> key, out string normalized)
    {
        normalized = string.Empty;
        if (!TrySplit(key, out var schema, out var name, out var isArray))
        {
            return false;
        }

        if (schema.IsEmpty)
        {
            return IsQuoted(name)
                ? TryNormalizeQuoted(TextOf(name).ToString(), isArray, out normalized)
                : TryNormalizePlain(name.ToString(), isArray, out normalized);
        }

        if (!IsSystemSchema(schema))
        {
            normalized = key.ToString();
        }
        else if (IsQuoted(name))
        {
            normalized = NormalizeQuotedCatalogName(name, isArray);
        }
        else if (!isArray && name.Length > 1 && name[0] == '_')
        {
            // Every type of pg_catalog whose name starts with _ is an array, of the type named by the rest.
            var ofElement = name.Slice(1).ToString();
            normalized = (Names.TryGetValue(ofElement, out var knownElement) ? knownElement : ofElement) + "[]";
        }
        else
        {
            normalized = isArray ? name.ToString() + "[]" : name.ToString();
        }

        return true;
    }

    /// <summary>
    /// Splits a key as it is written, which is what <see cref="TryNormalize" /> gives and <see cref="WritePart" />
    /// builds, into its parts, quotes included: <c>"a.b".c[]</c> gives <c>"a.b"</c>, <c>c</c> and true.
    /// <paramref name="schema" /> is empty for an unqualified key.  Returns false for text that is not
    /// <c>[part "."] part ["[]"]</c>, the grammar of <see cref="TryNormalize" />.
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

    /// <summary>Whether <paramref name="part" />, as <see cref="TrySplit" /> gives it, is in quotes.</summary>
    public static bool IsQuoted(ReadOnlySpan<char> part) => !part.IsEmpty && part[0] == '"';

    /// <summary>
    /// The text of <paramref name="part" />, as <see cref="TrySplit" /> gives it, without the quotes of a quoted one
    /// and with each <c>""</c> in it a single <c>"</c>.  Allocates only for a part that holds <c>""</c>.
    /// </summary>
    public static ReadOnlySpan<char> TextOf(ReadOnlySpan<char> part)
    {
        if (!IsQuoted(part))
        {
            return part;
        }

        var inside = part.Slice(1, part.Length - 2);
        if (inside.IndexOf('"') < 0)
        {
            return inside;
        }

        // The span is over the one array that is made, so the text costs a single allocation.
        var text = new char[inside.Length];
        var length = 0;
        var index = 0;
        while (index < inside.Length)
        {
            text[length++] = inside[index];

            // A quote is doubled in a part that is well formed, and the second of the two is skipped.
            index += inside[index] == '"' ? 2 : 1;
        }

        return new ReadOnlySpan<char>(text, 0, length);
    }

    /// <summary>
    /// Writes <paramref name="text" /> as a part of a key: as it is when it is not empty and holds only letters,
    /// digits, <c>_</c> and <c>$</c>, none of them upper case, since a plain part is read in lower case; otherwise in
    /// double quotes with each <c>"</c> doubled.
    /// </summary>
    public static string WritePart(string text)
    {
        var isPlain = text.Length > 0 && text.All(character => IsPlainCharacter(character) && !char.IsUpper(character));
        return isPlain ? text : "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static bool TryNormalizePlain(string text, bool isArray, out string normalized)
    {
        normalized = string.Empty;
        if (Names.TryGetValue(text, out var known) || Aliases.TryGetValue(text, out known))
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
        if (TryGetExactName(text, out var known))
        {
            normalized = isArray ? known + "[]" : known;
            return true;
        }

        if (text.Length > 1 && text[0] == '_' && TryGetExactName(text.Substring(1), out var element))
        {
            normalized = isArray ? "_" + element + "[]" : element + "[]";
            return true;
        }

        return false;
    }

    private static string NormalizeQuotedCatalogName(ReadOnlySpan<char> name, bool isArray)
    {
        var text = TextOf(name).ToString();
        if (TryGetExactName(text, out var known))
        {
            return isArray ? known + "[]" : known;
        }

        if (text.Length > 1 && text[0] == '_')
        {
            var ofElement = text.Substring(1);
            var isKnownElement = TryGetExactName(ofElement, out var knownElement);

            // The catalog's name of an array, with [], is the array of arrays' key, as it is for a plain name.
            if (isArray)
            {
                return isKnownElement ? "_" + knownElement + "[]" : name.ToString() + "[]";
            }

            return (isKnownElement ? knownElement : WritePart(ofElement)) + "[]";
        }

        return isArray ? name.ToString() + "[]" : name.ToString();
    }

    private static bool TryGetExactName(string text, out string known) =>
        Names.TryGetValue(text, out known) && string.Equals(known, text, StringComparison.Ordinal);

    private static bool IsSystemSchema(ReadOnlySpan<char> schema) =>
        IsQuoted(schema)
            ? TextOf(schema).Equals(SystemSchema.AsSpan(), StringComparison.Ordinal)
            : schema.Equals(SystemSchema.AsSpan(), StringComparison.OrdinalIgnoreCase);

    // The catalog's name of an array of a type the map knows, _int4, gives that type's name.
    private static bool TryReadArrayName(string part, out string element)
    {
        element = string.Empty;
        return part.Length > 1 && part[0] == '_' && Names.TryGetValue(part.Substring(1), out element);
    }

    // Reads the part that starts at start, plain or quoted, and gives the index after it.  A quoted part holds at least
    // one character, and ends at the first quote that is not doubled.
    private static bool TryReadPart(ReadOnlySpan<char> key, int start, out int end)
    {
        end = start;
        if (start >= key.Length)
        {
            return false;
        }

        if (key[start] != '"')
        {
            while (end < key.Length && IsPlainCharacter(key[end]))
            {
                end++;
            }

            return end > start;
        }

        end = start + 1;
        while (end < key.Length)
        {
            if (key[end] != '"')
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
                return end - start > 2;
            }
        }

        return false;
    }

    private static bool IsPlainCharacter(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';

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
