using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;

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

    // format_type(1560, -1) quotes bit, since plain bit means bit(1).
    public static PostgresTypeRow Bit { get; } = BaseOf(1560, "bit", "\"bit\"", "V");

    public static PostgresTypeRow Varbit { get; } = BaseOf(1562, "varbit", "bit varying", "V");

    // The single-byte "char", whose typname is char and which format_type quotes.
    public static PostgresTypeRow QuotedChar { get; } = BaseOf(18, "char", "\"char\"", "Z");

    public static PostgresTypeRow Timestamptz { get; } = BaseOf(1184, "timestamptz", "timestamp with time zone", "D");

    public static PostgresTypeRow Interval { get; } = BaseOf(1186, "interval", "interval", "T");

    // A base type of another schema with a modifier of its own, made over varcharin and varchartypmodin.
    public static PostgresTypeRow Tagged { get; } = BaseOf(16700, "tagged", "tagged", "U", "public");

    public static PostgresTypeRow TextArray { get; } = ArrayOf(1009, "_text", Text);

    public static PostgresTypeRow VarcharArray { get; } = ArrayOf(1015, "_varchar", Varchar);

    public static PostgresTypeRow NumericArray { get; } = ArrayOf(1231, "_numeric", Numeric);

    public static PostgresTypeRow TaggedArray { get; } = ArrayOf(16699, "_tagged", Tagged);

    public static PostgresTypeRow EmailT { get; } = DomainOf(16387, "email_t", Varchar);

    // email_t with the modifier of its varchar(254).
    public static PostgresTypeRow EmailT254 { get; } = EmailT with { Typtypmod = Typmods.Varchar254 };

    // A domain over a domain.
    public static PostgresTypeRow D2 { get; } = DomainOf(16500, "d2", EmailT);

    public static PostgresTypeRow CheckPair { get; } = CompositeOf(16600, "check_pair");

    // A schema and a type that need quotes, and a schema that is a reserved word.
    public static PostgresTypeRow MySchemaStatus { get; } = EnumIn(16610, "My Schema", "Status", "on", "off");

    public static PostgresTypeRow MySchemaStatusArray { get; } = ArrayOf(16611, "_Status", MySchemaStatus);

    public static PostgresTypeRow UserStatusInReservedSchema { get; } = EnumIn(16620, "user", "status", "on", "off");

    // A schema or a type whose name holds a dot.
    public static PostgresTypeRow InDottedSchema { get; } = EnumIn(16630, "a.b", "t", "on", "off");

    public static PostgresTypeRow InDottedSchemaArray { get; } = ArrayOf(16631, "_t", InDottedSchema);

    public static PostgresTypeRow WithDottedName { get; } = EnumIn(16640, "public", "x.y", "on", "off");

    public static PostgresTypeRow WithDottedNameArray { get; } = ArrayOf(16641, "_x.y", WithDottedName);

    public static PostgresTypeRow Tid { get; } = BaseOf(27, "tid", "tid", "U");

    public static PostgresTypeRow UserStatus { get; } = EnumOf(16390, "user_status", "active", "disabled");

    public static PostgresTypeRow Tstzrange { get; } = RangeOf(3910, "tstzrange", Timestamptz);

    public static PostgresTypeRow Int4range { get; } = RangeOf(3904, "int4range", Int4);

    public static PostgresTypeRow Int4multirange { get; } = MultirangeOf(4451, "int4multirange", Int4range);

    public static PostgresTypeRow Record { get; } = PseudoOf(2249, "record");

    // record[] is a pseudo-type itself, as the server has it: its element is no part of the closure the server
    // answers, which follows the element of a base array alone.
    public static PostgresTypeRow RecordArray { get; } =
        PseudoOf(2287, "_record") with
        {
            Typelem = Record.Oid,
            FormatType = "record[]",
        };

    public static PostgresTypeRow Cstring { get; } = PseudoOf(2275, "cstring");

    // cstring[] is the one base array of a pseudo-type that the server has.
    public static PostgresTypeRow CstringArray { get; } = ArrayOf(1263, "_cstring", Cstring);

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
        EnumIn(oid, "public", typname, labels);

    public static PostgresTypeRow EnumIn(long oid, string schema, string typname, params string[] labels) =>
        new(oid, typname, schema, "e", "E", 0, 0, null, null, typname, labels);

    public static PostgresTypeRow CompositeOf(long oid, string typname, string schema = "public") =>
        new(oid, typname, schema, "c", "C", 0, 0, null, null, typname, null);

    public static PostgresTypeRow RangeOf(long oid, string typname, PostgresTypeRow subtype) =>
        new(oid, typname, "pg_catalog", "r", "R", 0, 0, subtype.Oid, null, typname, null);

    public static PostgresTypeRow MultirangeOf(long oid, string typname, PostgresTypeRow range) =>
        new(oid, typname, "pg_catalog", "m", "R", 0, 0, null, range.Rngsubtype, typname, null);

    public static PostgresTypeRow PseudoOf(long oid, string typname) =>
        new(oid, typname, "pg_catalog", "p", "P", 0, 0, null, null, typname, null);

    public static IReadOnlyDictionary<long, PostgresTypeRow> Of(params PostgresTypeRow[] rows) =>
        rows.ToDictionary(static row => row.Oid);
}
