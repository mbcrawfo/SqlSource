using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

// Tables of "tables" as a server answers them, for a test that has no server, and columns of "columns" that name
// them.
internal static class PostgresTables
{
    public static PostgresTableRow Users { get; } =
        TableOf(
            16396,
            "users",
            Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true, identity: "a"),
            Attribute(2, "name", PostgresRows.Varchar.Oid, notNull: true),
            Attribute(3, "email", PostgresRows.EmailT.Oid),
            Attribute(9, "name_upper", PostgresRows.Text.Oid, generated: "s")
        );

    // users.id as "columns" answers it for a query that selects it first.  Declared after Users, which it reads.
    public static PostgresColumnRow UsersId { get; } = ColumnOf(0, "id", PostgresRows.Int4.Oid, Users, 1);

    public static PostgresTableRow Orders { get; } =
        TableOf(
            16415,
            "orders",
            Attribute(2, "user_id", PostgresRows.Int4.Oid, notNull: true),
            Attribute(3, "total", PostgresRows.Numeric.Oid, notNull: true)
        );

    public static PostgresTableRow TableOf(long oid, string name, params PostgresAttributeRow[] columns) =>
        new(oid, "r", "public", name, columns, false, false, false);

    public static PostgresAttributeRow Attribute(
        int attnum,
        string name,
        long typeOid,
        bool notNull = false,
        string identity = "",
        string generated = "",
        bool? notNullValidated = null
    ) => new(attnum, name, notNull, identity, generated, typeOid, notNullValidated);

    public static IReadOnlyDictionary<long, PostgresTableRow> Of(params PostgresTableRow[] tables) =>
        tables.ToDictionary(static table => table.Oid);

    // A column of "columns" that comes from an attribute of a table.
    public static PostgresColumnRow ColumnOf(
        int ordinal,
        string name,
        long typeOid,
        PostgresTableRow table,
        int attnum,
        int? length = null,
        int? precision = null,
        int? scale = null
    ) => new(ordinal, name, typeOid, table.Oid, attnum, length, precision, scale);

    // A column of "columns" that comes from an expression: it names no table.
    public static PostgresColumnRow ExpressionOf(
        int ordinal,
        string name,
        long typeOid,
        int? length = null,
        int? precision = null,
        int? scale = null,
        int? typeModifier = null
    ) => new(ordinal, name, typeOid, 0, 0, length, precision, scale, typeModifier);
}
