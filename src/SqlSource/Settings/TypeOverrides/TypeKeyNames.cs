using System;
using System.Collections.Generic;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// The names of the types that each engine's base map knows, and each engine's one-word aliases, that
/// <see cref="TypeKeys" /> reads a key against, and the names of the extensions' types, which the map reads.  Each map
/// keeps its own rows, and <c>PostgresTypeMapTests.Keys_OfTheMap_AreTheKnownPostgresNamesOfTheSettings</c> and
/// <c>SqlServerTypeMapTests.Keys_OfTheMap_AreTheKnownSqlServerNamesOfTheSettings</c> hold them equal to the names here.
/// </summary>
internal static class TypeKeyNames
{
    internal static readonly Dictionary<string, string> Extensions = NamesOf(
        "citext",
        "hstore",
        "ltree",
        "lquery",
        "ltxtquery",
        "cube"
    );

    // Every key of the base map, its refused types and the built-in multiranges included, by itself.
    internal static readonly Dictionary<string, string> PostgresNames = NamesOf(
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
    internal static readonly Dictionary<string, string> SqlServerNames = NamesOf(
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
    internal static readonly Dictionary<string, string> PostgresAliases = new(StringComparer.OrdinalIgnoreCase)
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
    internal static readonly Dictionary<string, string> SqlServerAliases = new(StringComparer.OrdinalIgnoreCase)
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
