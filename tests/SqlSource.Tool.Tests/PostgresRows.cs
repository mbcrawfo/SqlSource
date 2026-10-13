using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

// Rows of "types" as a server answers them, for a test that has no server.  A row is declared after every row it
// is made from.
internal static class PostgresRows
{
    public static PostgresTypeRow Int4 { get; } = BaseOf(23, "int4", "integer", "N");

    public static PostgresTypeRow Int8 { get; } = BaseOf(20, "int8", "bigint", "N");

    public static PostgresTypeRow Text { get; } = BaseOf(25, "text", "text", "S");

    public static PostgresTypeRow Varchar { get; } = BaseOf(1043, "varchar", "character varying", "S");

    public static PostgresTypeRow Numeric { get; } = BaseOf(1700, "numeric", "numeric", "N");

    public static PostgresTypeRow Timestamptz { get; } = BaseOf(1184, "timestamptz", "timestamp with time zone", "D");

    public static PostgresTypeRow TextArray { get; } = ArrayOf(1009, "_text", Text);

    public static PostgresTypeRow VarcharArray { get; } = ArrayOf(1015, "_varchar", Varchar);

    public static PostgresTypeRow EmailT { get; } = DomainOf(16387, "email_t", Varchar);

    // A domain over a domain.
    public static PostgresTypeRow D2 { get; } = DomainOf(16500, "d2", EmailT);

    public static PostgresTypeRow Tid { get; } = BaseOf(27, "tid", "tid", "U");

    public static PostgresTypeRow UserStatus { get; } = EnumOf(16390, "user_status", "active", "disabled");

    public static PostgresTypeRow Tstzrange { get; } = RangeOf(3910, "tstzrange", Timestamptz);

    public static PostgresTypeRow Int4range { get; } = RangeOf(3904, "int4range", Int4);

    public static PostgresTypeRow Int4multirange { get; } = MultirangeOf(4451, "int4multirange", Int4range);

    public static PostgresTypeRow Record { get; } = PseudoOf(2249, "record");

    public static PostgresTypeRow BaseOf(
        long oid,
        string typname,
        string formatType,
        string category,
        string schema = "pg_catalog"
    ) => new(oid, typname, schema, "b", category, 0, 0, null, null, formatType, null);

    public static PostgresTypeRow ArrayOf(long oid, string typname, PostgresTypeRow element) =>
        new(oid, typname, element.Nspname, "b", "A", element.Oid, 0, null, null, element.FormatType + "[]", null);

    // A domain has the category of its base.
    public static PostgresTypeRow DomainOf(
        long oid,
        string typname,
        PostgresTypeRow baseType,
        string schema = "public"
    ) => new(oid, typname, schema, "d", baseType.Typcategory, 0, baseType.Oid, null, null, typname, null);

    public static PostgresTypeRow EnumOf(long oid, string typname, params string[] labels) =>
        new(oid, typname, "public", "e", "E", 0, 0, null, null, typname, labels);

    public static PostgresTypeRow RangeOf(long oid, string typname, PostgresTypeRow subtype) =>
        new(oid, typname, "pg_catalog", "r", "R", 0, 0, subtype.Oid, null, typname, null);

    public static PostgresTypeRow MultirangeOf(long oid, string typname, PostgresTypeRow range) =>
        new(oid, typname, "pg_catalog", "m", "R", 0, 0, null, range.Rngsubtype, typname, null);

    public static PostgresTypeRow PseudoOf(long oid, string typname) =>
        new(oid, typname, "pg_catalog", "p", "P", 0, 0, null, null, typname, null);

    public static IReadOnlyDictionary<long, PostgresTypeRow> Of(params PostgresTypeRow[] rows) =>
        rows.ToDictionary(static row => row.Oid);
}
