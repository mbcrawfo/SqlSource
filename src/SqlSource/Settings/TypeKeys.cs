using System;
using System.Collections.Generic;

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
    /// Reads a key, <c>[part "."] part ["[]"]</c>, where a part is letters, digits, <c>_</c> and <c>$</c>.  An
    /// unqualified part must be a name the base map knows, an alias of one, or <c>_</c> and a name it knows, and is
    /// given as the name in lower case: <c>integer[]</c> gives <c>int4[]</c>.  <c>_</c> and a name it knows is the
    /// catalog's name of an array, and is given as the array's key, so <c>_int4</c> gives <c>int4[]</c>, and
    /// <c>_int4[]</c> stays as written.  <c>pg_catalog.</c> is taken off and what is left is not checked, but that
    /// every name of <c>pg_catalog</c> that starts with <c>_</c> is an array's, so <c>pg_catalog.newtype</c> gives
    /// <c>newtype</c>, <c>pg_catalog._int4</c> gives <c>int4[]</c> and <c>pg_catalog._pg_class</c> gives
    /// <c>pg_class[]</c>.  Any other qualified key is given as written, unchecked: <c>public._tag</c> is not
    /// <c>public.tag[]</c>, since a type of another schema may be named with a <c>_</c> (TD-0059).  Returns false for
    /// anything else, with an empty <paramref name="normalized" />.
    /// </summary>
    public static bool TryNormalize(ReadOnlySpan<char> key, out string normalized)
    {
        normalized = string.Empty;
        var isArray = key.EndsWith("[]".AsSpan(), StringComparison.Ordinal);
        var name = isArray ? key.Slice(0, key.Length - 2) : key;
        var period = name.IndexOf('.');
        if (period >= 0)
        {
            var schema = name.Slice(0, period);
            var part = name.Slice(period + 1);
            if (!IsPart(schema) || !IsPart(part))
            {
                return false;
            }

            if (!schema.Equals(SystemSchema.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                normalized = key.ToString();
            }
            else if (!isArray && part.Length > 1 && part[0] == '_')
            {
                // Every type of pg_catalog whose name starts with _ is an array, of the type named by the rest.
                var ofElement = part.Slice(1).ToString();
                normalized = (Names.TryGetValue(ofElement, out var knownElement) ? knownElement : ofElement) + "[]";
            }
            else
            {
                normalized = key.Slice(period + 1).ToString();
            }

            return true;
        }

        if (!IsPart(name))
        {
            return false;
        }

        var text = name.ToString();
        if (Names.TryGetValue(text, out var known) || Aliases.TryGetValue(text, out known))
        {
            normalized = isArray ? known + "[]" : known;
            return true;
        }

        if (TryReadArrayName(name, out var element))
        {
            normalized = isArray ? "_" + element + "[]" : element + "[]";
            return true;
        }

        return false;
    }

    // The catalog's name of an array of a type the map knows, _int4, gives that type's name.
    private static bool TryReadArrayName(ReadOnlySpan<char> part, out string element)
    {
        element = string.Empty;
        return part.Length > 1 && part[0] == '_' && Names.TryGetValue(part.Slice(1).ToString(), out element);
    }

    // No quotes: a type whose schema or name needs them for more than its case has no key (TD-0058).
    private static bool IsPart(ReadOnlySpan<char> part)
    {
        foreach (var character in part)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('_' or '$'))
            {
                return false;
            }
        }

        return !part.IsEmpty;
    }

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
