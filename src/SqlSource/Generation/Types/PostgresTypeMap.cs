using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Settings;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Types;

/// <summary>The type override's answer for <paramref name="type" />, or null when it names none.</summary>
internal delegate TypeMapping? OverrideOf(PostgresType type);

/// <summary>
/// The base map: the C# type Npgsql boxes for each PostgreSQL type it reads, under the column of a project's driver.
/// Each row is what Npgsql 8.0.3, 9.0.3 and 10.0.3 give for <c>GetValue</c> against a server, not what their
/// documentation implies.
/// </summary>
internal static class PostgresTypeMap
{
    private const string SystemSchema = "pg_catalog";
    private const string BitArray = "global::System.Collections.BitArray";
    private const string DateTime = "global::System.DateTime";
    private const string DateOnly = "global::System.DateOnly";
    private const string StringKeyword = "string";
    private const string NpgsqlTypesNamespace = "global::NpgsqlTypes.";

    private static readonly string[] BitKeys = ["bit", "varbit"];

    private static readonly Dictionary<string, Row> Rows = BuildRows();

    /// <summary>The keys of the map: every type of <c>pg_catalog</c> it names, and the extensions' types.</summary>
    public static IEnumerable<string> Keys => Rows.Keys.Concat(BitKeys);

    /// <summary>
    /// The C# type of <paramref name="type" /> under <paramref name="column" />.  At each level,
    /// <paramref name="overrideOf" /> is asked first; then an array is its element's type with <c>[]</c>, a domain
    /// its base's, and any other type the map's row or the rule of its kind.  An array's or a domain's failure is its
    /// element's or base's, with that key.  Never throws.
    /// </summary>
    public static TypeMapping Map(PostgresType type, DriverColumn column, OverrideOf? overrideOf = null)
    {
        if (overrideOf?.Invoke(type) is { } overridden)
        {
            return overridden;
        }

        // Every type of an extension the map knows is a base type: a domain or a composite of the same name is not one.
        var isOfTheMap =
            IsSystemType(type)
            || (
                type.Kind.Equals(SidecarValues.Kind.Base, StringComparison.Ordinal)
                && TypeKeys.IsExtension(type.InternalName)
            );
        if (isOfTheMap && IsBit(type.InternalName))
        {
            return type.Length == 1
                ? TypeMapping.Mapped(type.InternalName, "bool", true)
                : TypeMapping.Mapped(type.InternalName, BitArray, false);
        }

        if (isOfTheMap && Rows.TryGetValue(type.InternalName, out var row))
        {
            return row.Under(column, type.InternalName);
        }

        var key = KeyOf(type);
        if (!type.TryGetKind(out var kind))
        {
            return TypeMapping.Failed(TypeMappingKind.NotWrittenByTheTool, key);
        }

        return kind switch
        {
            PostgresTypeKind.Array => type.Element is { } element
                ? ArrayOf(Map(element, column, overrideOf))
                : TypeMapping.Failed(TypeMappingKind.NotWrittenByTheTool, key),
            PostgresTypeKind.Domain => type.Base is { } baseType
                ? Map(baseType, column, overrideOf)
                : TypeMapping.Failed(TypeMappingKind.NotWrittenByTheTool, key),
            PostgresTypeKind.Enum => TypeMapping.Mapped(key, StringKeyword, false),
            PostgresTypeKind.Range or PostgresTypeKind.Multirange => TypeMapping.Failed(
                TypeMappingKind.UserDefinedRange,
                key
            ),
            PostgresTypeKind.Base or PostgresTypeKind.Composite or _ => TypeMapping.Failed(
                TypeMappingKind.Unreadable,
                key
            ),
        };
    }

    /// <summary>
    /// The key of <paramref name="type" />: its <c>InternalName</c> for a type of <c>pg_catalog</c>, and
    /// <c>Schema.InternalName</c> for any other.
    /// </summary>
    public static string KeyOf(PostgresType type) =>
        IsSystemType(type) ? type.InternalName : type.Schema + "." + type.InternalName;

    // bit and varbit are decided by their length, not by a row.
    private static bool IsBit(string internalName) =>
        internalName.Equals("bit", StringComparison.OrdinalIgnoreCase)
        || internalName.Equals("varbit", StringComparison.OrdinalIgnoreCase);

    private static bool IsSystemType(PostgresType type) => type.Schema.Equals(SystemSchema, StringComparison.Ordinal);

    // An array of a type that a cast to oid reads is read by a cast to an array of oid, of the same rank.
    private static TypeMapping ArrayOf(TypeMapping element)
    {
        if (element.Kind == TypeMappingKind.Mapped)
        {
            return element with { Text = element.Text + "[]", IsValueType = false };
        }

        return element.Kind == TypeMappingKind.CastToOid ? element with { Detail = element.Detail + "[]" } : element;
    }

    private static Dictionary<string, Row> BuildRows()
    {
        var rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);

        void Add(Row row, params string[] keys)
        {
            foreach (var key in keys)
            {
                rows.Add(key, row);
            }
        }

        Add(Row.Same("bool", true), "bool");
        Add(Row.Same("short", true), "int2");
        Add(Row.Same("int", true), "int4");
        Add(Row.Same("long", true), "int8");
        Add(Row.Same("float", true), "float4");
        Add(Row.Same("double", true), "float8");
        Add(Row.Same("decimal", true), "numeric", "money");
        Add(Row.Same(StringKeyword, false), "text", "varchar", "bpchar", "name", "refcursor");
        Add(Row.Same("char", true), "char");
        Add(Row.Same(StringKeyword, false), "citext");
        Add(Row.Same("byte[]", false), "bytea");
        Add(new Row(DateTime, DateOnly, true, false, null), "date");
        Add(new Row("global::System.TimeSpan", "global::System.TimeOnly", true, false, null), "time");
        Add(Row.Same("global::System.DateTimeOffset", true), "timetz");
        Add(Row.Same(DateTime, true), "timestamp", "timestamptz");
        Add(Row.Same("global::System.TimeSpan", true), "interval");
        Add(Row.Same("global::System.Guid", true), "uuid");
        Add(Row.Same(StringKeyword, false), "json", "jsonb", "jsonpath", "xml");
        Add(Row.Same("global::System.Net.IPAddress", false), "inet");
        Add(new Row("global::NpgsqlTypes.NpgsqlCidr", "global::System.Net.IPNetwork", true, true, null), "cidr");
        Add(Row.Same("global::System.Net.NetworkInformation.PhysicalAddress", false), "macaddr", "macaddr8");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlPoint", true), "point");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlLine", true), "line");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlLSeg", true), "lseg");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlBox", true), "box");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlPath", true), "path");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlPolygon", true), "polygon");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlCircle", true), "circle");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlTsVector", false), "tsvector");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlTsQuery", false), "tsquery");
        Add(Row.Same("uint", true), "oid", "xid", "cid", "regtype", "regconfig");
        Add(Row.Same("ulong", true), "xid8");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlTid", true), "tid");
        Add(Row.OfNpgsql("global::NpgsqlTypes.NpgsqlLogSequenceNumber", true), "pg_lsn");
        Add(Row.Same("short[]", false), "int2vector");
        Add(Row.Same("uint[]", false), "oidvector");
        Add(Row.Same("global::System.Collections.Generic.Dictionary<string, string?>", false), "hstore");
        Add(Row.Same(StringKeyword, false), "ltree", "lquery", "ltxtquery");
        Add(new Row(null, "global::NpgsqlTypes.NpgsqlCube", true, true, null), "cube");
        Add(Row.Range("int"), "int4range");
        Add(Row.Range("long"), "int8range");
        Add(Row.Range("decimal"), "numrange");
        Add(Row.Range(DateTime), "tsrange", "tstzrange");
        Add(Row.Range(DateTime, DateOnly), "daterange");
        Add(Row.Multirange("int"), "int4multirange");
        Add(Row.Multirange("long"), "int8multirange");
        Add(Row.Multirange("decimal"), "nummultirange");
        Add(Row.Multirange(DateTime), "tsmultirange", "tstzmultirange");
        Add(Row.Multirange(DateTime, DateOnly), "datemultirange");
        Add(
            Row.Refused(TypeMappingKind.CastToOid),
            "regclass",
            "regproc",
            "regprocedure",
            "regoper",
            "regoperator",
            "regrole",
            "regnamespace",
            "regcollation",
            "regdictionary"
        );
        Add(Row.Refused(TypeMappingKind.Unreadable), "pg_snapshot", "txid_snapshot", "pg_node_tree");
        Add(Row.Refused(TypeMappingKind.NotWrittenByTheTool), "record", "unknown", "void");
        return rows;
    }

    /// <summary>A row of the map.</summary>
    /// <param name="Npgsql8">The C# type on Npgsql 8 and 9, or null when they cannot read the type.</param>
    /// <param name="Npgsql10">The C# type on Npgsql 10 where it differs, or null.</param>
    /// <param name="IsValueType">Whether the C# type is a value type.</param>
    /// <param name="IsOfNpgsql">Whether the C# type is of the <c>NpgsqlTypes</c> namespace.</param>
    /// <param name="Refusal">Why no driver can read the type, or null.</param>
    private sealed record Row(
        string? Npgsql8,
        string? Npgsql10,
        bool IsValueType,
        bool IsOfNpgsql,
        TypeMappingKind? Refusal
    )
    {
        public static Row Same(string text, bool isValueType) => new(text, null, isValueType, false, null);

        public static Row OfNpgsql(string text, bool isValueType) => new(text, null, isValueType, true, null);

        public static Row Refused(TypeMappingKind kind) => new(null, null, false, false, kind);

        // A range names its subtype's type; a multirange is an array of its range's.
        public static Row Range(string subtype, string? subtypeOn10 = null) =>
            new(RangeOf(subtype), subtypeOn10 is null ? null : RangeOf(subtypeOn10), true, true, null);

        public static Row Multirange(string subtype, string? subtypeOn10 = null) =>
            new(RangeOf(subtype) + "[]", subtypeOn10 is null ? null : RangeOf(subtypeOn10) + "[]", false, true, null);

        // With no driver, a type whose two cells are both C# types differs between the drivers, which wins over
        // needing the driver: only the override can say which one the project runs on, and the failure's detail
        // gives the entry for each.
        public TypeMapping Under(DriverColumn column, string key)
        {
            if (Refusal is { } refusal)
            {
                return TypeMapping.Failed(refusal, key, refusal == TypeMappingKind.CastToOid ? "oid" : null);
            }

            if (column == DriverColumn.None && Npgsql8 is not null && Npgsql10 is not null)
            {
                return TypeMapping.Failed(TypeMappingKind.DiffersByDriver, key, EntriesFor(key, Npgsql8, Npgsql10));
            }

            if (column == DriverColumn.None && IsOfNpgsql)
            {
                return TypeMapping.Failed(TypeMappingKind.NeedsDriver, key);
            }

            var text = column == DriverColumn.Npgsql10 ? Npgsql10 ?? Npgsql8 : Npgsql8;
            return text is null
                ? TypeMapping.Failed(TypeMappingKind.Unreadable, key)
                : TypeMapping.Mapped(key, text, IsValueType);
        }

        private static string RangeOf(string subtype) => "global::NpgsqlTypes.NpgsqlRange<" + subtype + ">";

        // The type override's entry for each driver whose C# type a project without Npgsql can name, as a user
        // writes it, without global::.  A type of NpgsqlTypes cannot be found without the driver, so it is left
        // out, and null when both are.
        private static string? EntriesFor(string key, string npgsql8, string npgsql10)
        {
            var eight = IsOfNpgsqlTypes(npgsql8) ? null : key + "=" + AsWritten(npgsql8) + " on Npgsql 8 and 9";
            var ten = IsOfNpgsqlTypes(npgsql10) ? null : key + "=" + AsWritten(npgsql10) + " on Npgsql 10";
            if (eight is null || ten is null)
            {
                return eight ?? ten;
            }

            return eight + ", " + ten;
        }

        private static bool IsOfNpgsqlTypes(string text) =>
            text.StartsWith(NpgsqlTypesNamespace, StringComparison.Ordinal);

        // A C# type as a user writes it in the type override, without global::.
        private static string AsWritten(string text) => text.Replace("global::", string.Empty);
    }
}
