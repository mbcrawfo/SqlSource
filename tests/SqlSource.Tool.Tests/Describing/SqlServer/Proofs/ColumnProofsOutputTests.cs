using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.ColumnProofsCases;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The proofs of the columns of an INSERT, an UPDATE or a DELETE with OUTPUT: a pseudo-table's PERSISTED NOT NULL
// column by the catalog, its column the WHERE keeps from NULL by the text, its column the SET writes by the value
// written, and every other item as a SELECT's over the statement's rows.  No text of "operands" names a pseudo-table,
// which the server refuses in a SELECT, or DEFAULT.
public sealed partial class ColumnProofsOutputTests
{
    // The query of the realistic case update-orders-from-join-output.
    private const string UpdateFromJoin = """
        UPDATE o
        SET o.status = 'delivered'
        OUTPUT
            INSERTED.id,
            DELETED.status AS previous_status,
            INSERTED.status,
            INSERTED.shipped_at,
            INSERTED.ship_city,
            c.name AS customer_name,
            c.email AS customer_email
        FROM dbo.orders AS o
        JOIN dbo.customers AS c ON c.id = o.customer_id
        WHERE
            o.status = 'shipped'
            AND o.shipped_at < @shipped_before;
        """;

    // The query of the realistic case cte-feeding-update.
    private const string CteFeedingUpdate = """
        WITH due AS (
            SELECT
                i.order_id,
                i.number,
                i.due_at,
                i.amount - ISNULL(SUM(p.amount), 0) AS outstanding
            FROM billing.invoices AS i
            LEFT JOIN billing.payments AS p ON p.invoice_id = i.id
            WHERE
                i.voided_at IS NULL
                AND i.due_at < @as_of
            GROUP BY i.order_id, i.number, i.due_at, i.amount
            HAVING i.amount > ISNULL(SUM(p.amount), 0)
        )

        UPDATE o
        SET o.note = CONCAT(o.note + N'; ', N'Invoice ', d.number, N' overdue since ', CONVERT(char(10), d.due_at, 23))
        OUTPUT
            INSERTED.id,
            INSERTED.status,
            DELETED.note AS previous_note,
            INSERTED.note,
            d.number AS invoice_number,
            d.due_at,
            d.outstanding,
            INSERTED.shipped_at
        FROM dbo.orders AS o
        JOIN due AS d ON d.order_id = o.id;
        """;

    // The statements of the review that assign the column set cs of dbo.t, whose sparse column s1 is NULL afterwards
    // in a row the WHERE kept.
    private const string SparseSetXml = "UPDATE dbo.t SET cs = '<s2>1</s2>' OUTPUT inserted.s1 WHERE s1 IS NOT NULL";
    private const string SparseSetNull = "UPDATE dbo.t SET cs = NULL OUTPUT inserted.s1 WHERE s1 IS NOT NULL";
    private const string SparseSetFromAlias =
        "UPDATE o SET o.cs = NULL OUTPUT inserted.s1 FROM dbo.t AS o WHERE o.s1 IS NOT NULL";

    // Everything a pseudo-table's column may be: the server answers each operand not null and stored, of no CLR type.
    private static readonly OperandFacts Proving = new(NotNull: true, NotClr: true, Stored: true);

    private static readonly (string Schema, string Table) Orders = ("dbo", "orders");

    // A stored column of a table, in the place given, its name the column's.
    private static SqlServerColumnRow Stored(int ordinal, string name, bool nullable, string table = "orders") =>
        IdColumn with
        {
            ColumnOrdinal = ordinal,
            Name = name,
            IsNullable = nullable,
            SourceTable = table,
            SourceColumn = name,
            IsIdentityColumn = false,
        };

    // The OUTPUT of the realistic update proves inserted.shipped_at, which the WHERE keeps from NULL and the SET does
    // not write, by the text over the rows of "table" for dbo.orders, and nothing else: inserted.ship_city is kept by
    // nothing, and c.email alone is what the server answered.  So no operand is asked.
    [Fact]
    public void UpdateFromJoinOutput_ShippedAtKeptByTheWhere_IsProved()
    {
        var shippedAt = Stored(4, "shipped_at", nullable: true);
        var proofs = Read(
            UpdateFromJoin,
            Stored(1, "id", nullable: false) with
            {
                IsIdentityColumn = true,
            },
            Stored(2, "status", nullable: false) with
            {
                Name = "previous_status",
            },
            Stored(3, "status", nullable: false),
            shippedAt,
            Stored(5, "ship_city", nullable: true),
            Stored(6, "name", nullable: false, "customers") with
            {
                Name = "customer_name",
            },
            Stored(7, "email", nullable: true, "customers") with
            {
                Name = "customer_email",
            }
        );

        proofs.OperandQueries.ShouldBeEmpty();
        proofs.ProofTables.ShouldBe([Orders]);
        Nullability(proofs.Apply(null, null, TablesOf(proofs, shippedAt)))
            .ShouldBe([
                (false, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Expression),
                (true, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Server),
                (true, SidecarValues.NullableSource.Server),
            ]);
    }

    // inserted.note is the value the SET writes, asked over the UPDATE's rows, and d.due_at and d.outstanding are
    // columns of a CTE of one branch, asked there and of the branch.
    [Fact]
    public void CteFeedingUpdate_ProvesNoteDueAtAndOutstanding()
    {
        var with = CteFeedingUpdate[..CteFeedingUpdate.IndexOf("\n\nUPDATE", StringComparison.Ordinal)];
        var value = Between(CteFeedingUpdate, "SET o.note = ", "\nOUTPUT");
        var from = Between(CteFeedingUpdate, "    INSERTED.shipped_at\n", ";");
        var branchFrom = Between(CteFeedingUpdate, "AS outstanding\n", "\n    GROUP BY").TrimStart();
        var proofs = Read(
            CteFeedingUpdate,
            Stored(1, "id", nullable: false) with
            {
                IsIdentityColumn = true,
            },
            Stored(2, "status", nullable: false),
            Stored(3, "note", nullable: true) with
            {
                Name = "previous_note",
            },
            Stored(4, "note", nullable: true),
            Stored(5, "number", nullable: false, "invoices") with
            {
                Name = "invoice_number",
                SourceSchema = "billing",
            },
            Stored(6, "due_at", nullable: true, "invoices") with
            {
                SourceSchema = "billing",
            },
            ExpressionRow(7, "outstanding"),
            Stored(8, "shipped_at", nullable: true)
        );

        proofs
            .OperandQueries.Select(static query => query.Sql)
            .ShouldBe([
                $"{with}\nSELECT {value}\nAS [1],\nd.due_at\nAS [2],\nd.outstanding\nAS [3]\n{from}",
                $"{with}\nSELECT i.due_at\nAS [1],\ni.amount\nAS [2]\n{branchFrom}",
            ]);
        ShouldAskNoPseudoTable(proofs);
        Nullability(proofs.Apply(Answers(true, true, true, true, true)))
            .ShouldBe([
                (false, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Server),
                (true, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Expression),
                (false, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Expression),
                (false, SidecarValues.NullableSource.Expression),
                (true, SidecarValues.NullableSource.Server),
            ]);

        // The value answered nullable, or of a CLR type; the CTE's column and its branch's operand nullable.
        Nullability(proofs.Apply(Answers(false, true, false, true, false)))
            .Select(static column => column.Nullable)
            .ShouldBe([false, false, true, true, false, false, true, true]);
        Nullability(proofs.Apply([new OperandFacts(true, NotClr: false), .. Answers(true, true, true, true)]))[3]
            .ShouldBe((true, SidecarValues.NullableSource.Server));
        Nullability(proofs.Apply(null))
            .Select(static column => column.Nullable)
            .ShouldBe([false, false, true, true, false, true, true, true]);
    }

    // A pseudo-table's PERSISTED NOT NULL column, alone or by its *, is a row of the target, which the catalog proves,
    // whatever the statement's rows.
    [Theory]
    [InlineData("INSERT INTO dbo.users (name) OUTPUT inserted.id, inserted.name_len_p VALUES (@x)")]
    [InlineData("INSERT dbo.users (name) OUTPUT inserted.* SELECT name FROM dbo.people")]
    [InlineData("UPDATE dbo.users SET name = @x OUTPUT INSERTED.id, inserted.name_len_p WHERE id = @id")]
    [InlineData("UPDATE dbo.users SET name = @x OUTPUT deleted.id, [deleted].[name_len_p] WHERE id = @id")]
    [InlineData("UPDATE dbo.users SET name = @x OUTPUT deleted.*")]
    [InlineData("DELETE FROM dbo.users OUTPUT deleted.id, deleted.name_len_p WHERE id = @id")]
    [InlineData(
        "DELETE u OUTPUT deleted.* FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id "
            + "WHERE o.id IS NULL"
    )]
    public void Output_PersistedNotNullOfAPseudoTable_IsCatalog(string sql)
    {
        var proofs = Read(sql, IdColumn, PersistedColumn(2));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Catalog)]);
    }

    // The pseudo-table the statement has not, an expression over the column, a * among other items, a row of a view
    // and a misread name are no row of the target.
    [Theory]
    [InlineData("INSERT INTO dbo.users (name) OUTPUT inserted.id, deleted.name_len_p VALUES (@x)", "U")]
    [InlineData("DELETE FROM dbo.users OUTPUT deleted.id, inserted.name_len_p WHERE id = @id", "U")]
    [InlineData("DELETE FROM dbo.users OUTPUT deleted.id, (deleted.name_len_p) AS name_len_p", "U")]
    [InlineData("DELETE FROM dbo.users OUTPUT deleted.id, deleted.name_len_p + 0 AS name_len_p", "U")]
    [InlineData("DELETE FROM dbo.users OUTPUT deleted.id, dbo.deleted.name_len_p", "U")]
    [InlineData("DELETE FROM dbo.users OUTPUT deleted.*, deleted.*", "U")]
    [InlineData("DELETE FROM dbo.users OUTPUT inserted.*", "U")]
    [InlineData("UPDATE dbo.users SET name = @x OUTPUT inserted.id, inserted.name_len_p", "V")]
    [InlineData("UPDATE dbo.users SET name = @x OUTPUT inserted.*", "V")]
    public void Output_PersistedNotNullNoRowOfTheTarget_StaysNullable(string sql, string sourceType)
    {
        var proofs = Read(sql, IdColumn, PersistedColumn(2) with { SourceType = sourceType });

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null))[1].ShouldBe((true, SidecarValues.NullableSource.Server));
    }

    // deleted holds the value the WHERE tested, whatever the SET writes; a DELETE writes nothing.
    [Theory]
    [InlineData("UPDATE dbo.orders SET shipped_at = NULL OUTPUT deleted.shipped_at WHERE shipped_at IS NOT NULL")]
    [InlineData(
        "UPDATE o SET o.shipped_at = NULL OUTPUT deleted.shipped_at FROM dbo.orders AS o "
            + "JOIN dbo.customers AS c ON c.id = o.customer_id WHERE o.shipped_at < @x"
    )]
    [InlineData("UPDATE dbo.orders SET note = NULL OUTPUT deleted.shipped_at WHERE orders.shipped_at > @x")]
    public void UpdateDeleted_KeptByTheWhere_IsProvedThoughTheSetWritesIt(string sql) => ShouldProveByTheText(sql);

    [Theory]
    [InlineData("DELETE FROM dbo.orders OUTPUT deleted.shipped_at WHERE shipped_at IS NOT NULL")]
    [InlineData("DELETE dbo.orders OUTPUT deleted.shipped_at WHERE shipped_at LIKE @x")]
    [InlineData(
        "DELETE o OUTPUT deleted.shipped_at FROM dbo.orders AS o JOIN dbo.customers AS c ON c.id = o.customer_id "
            + "WHERE o.shipped_at < @x"
    )]
    public void DeleteDeleted_KeptByTheWhere_IsProved(string sql) => ShouldProveByTheText(sql);

    // inserted holds the value the WHERE tested when the SET writes no column of its name, and "table" for the target
    // shows no sparse column set and lists the column; without that answer it proves nothing.
    [Theory]
    [InlineData("UPDATE dbo.orders SET note = NULL OUTPUT inserted.shipped_at WHERE shipped_at IS NOT NULL")]
    [InlineData("UPDATE o SET note = @n OUTPUT inserted.shipped_at FROM dbo.orders AS o WHERE shipped_at IS NOT NULL")]
    public void UpdateInserted_KeptByTheWhereAndNotWritten_IsProved(string sql)
    {
        var shippedAt = Stored(1, "shipped_at", nullable: true);
        var proofs = Read(sql, shippedAt);

        proofs.OperandQueries.ShouldBeEmpty();
        proofs.ProofTables.ShouldBe([Orders]);
        Nullability(proofs.Apply(null, null, TablesOf(proofs, shippedAt)))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // Assigning a sparse column set writes every sparse column its XML leaves out, so inserted.s1 of dbo.t (id, s1 and
    // s2 SPARSE NULL, cs the column set) is NULL though the WHERE kept it.  "table" for the target is the proof's
    // condition: an answer with the column set, one that lacks the column, as SELECT * lacks a sparse column of a
    // table with a column set, and none, or one with an error, prove nothing; a plain table's answer keeps the proof.
    [Theory]
    [InlineData(SparseSetXml, "column set", true)]
    [InlineData(SparseSetNull, "column set", true)]
    [InlineData(SparseSetFromAlias, "column set", true)]
    [InlineData(SparseSetXml, "as the server answers", true)]
    [InlineData(SparseSetNull, "as the server answers", true)]
    [InlineData(SparseSetFromAlias, "as the server answers", true)]
    [InlineData(SparseSetXml, "lacks the column", true)]
    [InlineData(SparseSetNull, "lacks the column", true)]
    [InlineData(SparseSetFromAlias, "lacks the column", true)]
    [InlineData(SparseSetXml, "not asked", true)]
    [InlineData(SparseSetNull, "not asked", true)]
    [InlineData(SparseSetFromAlias, "not asked", true)]
    [InlineData(SparseSetXml, "plain", false)]
    [InlineData(SparseSetNull, "plain", false)]
    [InlineData(SparseSetFromAlias, "plain", false)]
    public void UpdateInserted_KeptByTheWhereOfATableWithAColumnSet_StaysNullable(
        string sql,
        string answer,
        bool nullable
    )
    {
        var s1 = Stored(1, "s1", nullable: true, "t");
        var proofs = Read(sql, s1);
        var id = Stored(1, "id", nullable: false, "t");
        var s2 = Stored(2, "s2", nullable: true, "t");
        var columnSet = Stored(3, "cs", nullable: true, "t") with { SystemTypeName = "xml", IsSparseColumnSet = true };
        IReadOnlyList<SqlServerColumnRow>? rows = answer switch
        {
            "column set" => [id, s1 with { ColumnOrdinal = 2 }, columnSet, Rowstat with { ColumnOrdinal = 4 }],
            "as the server answers" => [id, columnSet with { ColumnOrdinal = 2 }, Rowstat with { ColumnOrdinal = 3 }],
            "lacks the column" => [id, s2, Rowstat with { ColumnOrdinal = 3 }],
            "plain" =>
            [
                id,
                s1 with
                {
                    ColumnOrdinal = 2,
                },
                s2 with
                {
                    ColumnOrdinal = 3,
                },
                Rowstat with
                {
                    ColumnOrdinal = 4,
                },
            ],
            _ => null,
        };

        proofs.OperandQueries.ShouldBeEmpty();
        proofs.ProofTables.ShouldBe([("dbo", "t")]);
        var tables = new Dictionary<(string Schema, string Table), IReadOnlyList<SqlServerColumnRow>>();
        if (rows is not null)
        {
            tables[("dbo", "t")] = rows;
        }

        Nullability(proofs.Apply(null, null, tables))
            .ShouldBe([
                nullable
                    ? (true, SidecarValues.NullableSource.Server)
                    : (false, SidecarValues.NullableSource.Expression),
            ]);
    }

    // deleted holds the value the WHERE tested, which no assignment of a column set changes, so it asks nothing.
    [Theory]
    [InlineData("UPDATE dbo.t SET cs = '<s2>1</s2>' OUTPUT deleted.s1 WHERE s1 IS NOT NULL")]
    [InlineData("UPDATE o SET o.cs = NULL OUTPUT deleted.s1 FROM dbo.t AS o WHERE o.s1 IS NOT NULL")]
    public void UpdateDeleted_KeptByTheWhereOfATableWithAColumnSet_IsProvedWithoutTable(string sql)
    {
        var proofs = Read(sql, Stored(1, "s1", nullable: true, "t"));

        proofs.OperandQueries.ShouldBeEmpty();
        proofs.ProofTables.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    // A value of the text alone needs no rows, and one the server must answer is asked of none without a FROM.
    [Theory]
    [InlineData("UPDATE dbo.orders SET note = N'x' OUTPUT inserted.note WHERE id = @id")]
    [InlineData("UPDATE dbo.orders SET orders.note = 'a' + 'b', status = NULL OUTPUT inserted.note")]
    public void UpdateSetLiteral_IsProvedWithoutAFrom(string sql)
    {
        var proofs = Read(sql, Stored(1, "note", nullable: true));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    [Theory]
    [InlineData("UPDATE dbo.orders SET note = ship_city OUTPUT inserted.note WHERE ship_city IS NOT NULL")]
    [InlineData("UPDATE dbo.orders SET note = CONCAT(note, N'x') OUTPUT inserted.note WHERE id = @id")]
    [InlineData("UPDATE dbo.orders SET note = N'x' + ship_city OUTPUT inserted.note")]
    public void UpdateSetColumn_WithoutAFrom_AsksNothing(string sql)
    {
        var proofs = Read(sql, Stored(1, "note", nullable: true));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // A value the server must answer is asked over the UPDATE's rows, and proves the column when it is answered not
    // null and of no CLR type.
    [Fact]
    public void UpdateSetColumn_WithAFrom_AsksItOverTheRows()
    {
        var proofs = Read(
            "UPDATE o SET o.note = c.name OUTPUT inserted.note FROM dbo.orders AS o "
                + "JOIN dbo.customers AS c ON c.id = o.customer_id WHERE o.id = @id",
            Stored(1, "note", nullable: true)
        );

        proofs
            .OperandQueries.ShouldHaveSingleItem()
            .Sql.ShouldBe(
                "SELECT c.name\nAS [1]\nFROM dbo.orders AS o JOIN dbo.customers AS c ON c.id = o.customer_id "
                    + "WHERE o.id = @id"
            );
        Nullability(proofs.Apply(Answers(true))).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply(Answers(false))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply([new OperandFacts(true, NotClr: false)]))
            .ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // An item that names no pseudo-table is a SELECT's item over the statement's rows.
    [Fact]
    public void DeleteOutputOfAnotherSource_IsProvedAsASelectsItem()
    {
        var proofs = Read(
            "DELETE o OUTPUT ISNULL(c.email, c.name) AS contact FROM dbo.orders AS o "
                + "JOIN dbo.customers AS c ON c.id = o.customer_id WHERE o.id = @id",
            ExpressionRow(1, "contact")
        );

        proofs
            .OperandQueries.ShouldHaveSingleItem()
            .Sql.ShouldBe(
                "SELECT c.email\nAS [1],\nc.name\nAS [2]\nFROM dbo.orders AS o "
                    + "JOIN dbo.customers AS c ON c.id = o.customer_id WHERE o.id = @id"
            );
        Nullability(proofs.Apply(Answers(false, true))).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply(Answers(false, false))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // The cases of the review: a target that is not the aliased instance, a SET that writes the filtered column by any
    // spelling, a target that is a view, a CTE or a derived table, a MERGE, a filter on another source's column, and a
    // pseudo-table inside an expression.  Then a computed column, the other pseudo-table, a source named as one, and
    // filters that keep nothing.  Every operand the server is asked is answered not null and stored, and nothing is
    // proved.
    [Theory]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE t SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE o SET o.b = NULL OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE o SET B = NULL OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE o SET @v = b = NULL OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE o SET b += 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE o SET b = DEFAULT OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE dbo.t SET b = DEFAULT OUTPUT inserted.b WHERE b IS NOT NULL")]
    [InlineData("UPDATE o SET b = NULL, a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE b > 0")]
    [InlineData("UPDATE o SET b.STSrid = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("UPDATE o SET b.WRITE(N'x', 0, 1) OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL")]
    [InlineData("WITH c AS (SELECT * FROM dbo.t) UPDATE c SET a = 1 OUTPUT inserted.b WHERE b IS NOT NULL")]
    [InlineData("UPDATE x SET a = 1 OUTPUT inserted.b FROM (SELECT * FROM dbo.t) AS x WHERE x.b IS NOT NULL")]
    [InlineData(
        "MERGE dbo.t AS o USING dbo.u AS u ON u.id = o.id WHEN MATCHED AND o.b IS NOT NULL THEN UPDATE SET a = 1 "
            + "WHEN NOT MATCHED BY SOURCE THEN DELETE OUTPUT inserted.b;"
    )]
    [InlineData(
        "UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o JOIN dbo.u AS u ON u.id = o.id WHERE u.b IS NOT NULL"
    )]
    [InlineData("DELETE o OUTPUT deleted.b FROM dbo.t AS o JOIN dbo.u AS u ON u.id = o.id WHERE u.b IS NOT NULL")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o JOIN dbo.u AS u ON u.id = o.id WHERE b > 0")]
    [InlineData(
        "UPDATE o SET a = 1 OUTPUT COALESCE(inserted.b, 0) AS b FROM dbo.t AS o JOIN dbo.u AS u ON u.id = o.id "
            + "WHERE o.b IS NOT NULL"
    )]
    [InlineData("DELETE o OUTPUT ISNULL([DELETED] . b, u.b) AS b FROM dbo.t AS o JOIN dbo.u AS u ON u.id = o.id")]
    [InlineData(
        "UPDATE o SET b = ISNULL(deleted.b, 0) OUTPUT inserted.b FROM dbo.t AS o JOIN dbo.u AS u ON u.id = o.id"
    )]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.B IS NOT NULL")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE O.b IS NOT NULL")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NULL")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b > 0 OR o.a > 0")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE NOT o.b > 0")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b = NULL")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE dbo.t.b IS NOT NULL")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o")]
    [InlineData("INSERT INTO dbo.t (b) OUTPUT inserted.b SELECT b FROM dbo.u WHERE b IS NOT NULL")]
    [InlineData("INSERT INTO dbo.t (b) OUTPUT deleted.b VALUES (1)")]
    [InlineData("DELETE FROM dbo.t OUTPUT inserted.b WHERE b IS NOT NULL")]
    [InlineData(
        "UPDATE o SET a = 1 OUTPUT deleted.b FROM dbo.t AS o JOIN dbo.u AS inserted ON inserted.id = o.id "
            + "WHERE o.b IS NOT NULL"
    )]
    [InlineData(
        "UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o JOIN dbo.u AS Deleted ON Deleted.id = o.id "
            + "WHERE o.b IS NOT NULL"
    )]
    public void Output_ReviewCases_StayNullable(string sql) => ShouldProveNothing(sql, Stored(1, "b", true, "t"));

    // SQL Server ignores a name's trailing spaces, so each SET writes the column the WHERE keeps, and the server names
    // an item as it is written.
    [Theory]
    [InlineData("UPDATE dbo.t SET [b ] = NULL OUTPUT inserted.b WHERE b IS NOT NULL", "b")]
    [InlineData("UPDATE dbo.t SET [b  ] = NULL OUTPUT inserted.b WHERE b IS NOT NULL", "b")]
    [InlineData("UPDATE dbo.t SET [B ] = NULL OUTPUT inserted.b WHERE b IS NOT NULL", "b")]
    [InlineData("UPDATE o SET o.[b ] = NULL OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL", "b")]
    [InlineData("UPDATE dbo.t SET b = NULL OUTPUT inserted.[b ] WHERE [b ] IS NOT NULL", "b ")]
    public void Output_SetOfTheColumnWithTrailingSpaces_StaysNullable(string sql, string name) =>
        ShouldProveNothing(sql, Stored(1, name, true, "t") with { SourceColumn = "b" });

    // A computed column, a view's and one of a CLR type are not the target's stored column the filter tested.
    [Theory]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.b FROM dbo.t AS o WHERE o.b IS NOT NULL", "computed")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT deleted.b WHERE b IS NOT NULL", "computed")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.b WHERE b IS NOT NULL", "computed")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.b WHERE b IS NOT NULL", "view")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.b WHERE b IS NOT NULL", "view")]
    [InlineData("UPDATE dbo.t SET b = 'POINT (0 0)' OUTPUT inserted.b WHERE id = @id", "view")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.b WHERE b IS NOT NULL", "clr")]
    [InlineData("UPDATE dbo.t SET b = 'POINT (0 0)' OUTPUT inserted.b WHERE id = @id", "clr")]
    public void Output_NoStoredColumnOfTheTarget_StaysNullable(string sql, string how)
    {
        var row = Stored(1, "b", nullable: true, "t");
        row = how switch
        {
            "computed" => row with { IsComputedColumn = true },
            "view" => row with { SourceType = "V" },
            _ => row with
            {
                SystemTypeName = "geometry",
                AssemblyQualifiedTypeName = "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types",
            },
        };

        ShouldProveNothing(sql, row);
    }

    // No call that a SET, an OUTPUT or a WHERE of an UPDATE asks names a pseudo-table or DEFAULT, though every item
    // does.
    [Fact]
    public void Output_ItemsOfEveryKind_AskNoPseudoTable()
    {
        var proofs = Read(
            "UPDATE o SET o.b = c.name, o.a = DEFAULT OUTPUT inserted.b, deleted.b AS old_b, inserted.a, "
                + "ISNULL(inserted.a, c.id) AS x, c.email FROM dbo.t AS o "
                + "LEFT JOIN dbo.customers AS c ON c.id = o.customer_id WHERE c.name IS NOT NULL AND o.b IS NOT NULL",
            Stored(1, "b", nullable: true, "t"),
            Stored(2, "b", nullable: true, "t") with
            {
                Name = "old_b",
            },
            Stored(3, "a", nullable: true, "t"),
            ExpressionRow(4, "x"),
            Stored(5, "email", nullable: true, "customers")
        );

        proofs.OperandQueries.ShouldNotBeEmpty();
        ShouldAskNoPseudoTable(proofs);
    }

    // A proof of deleted by the WHERE, which needs no "table".
    private static void ShouldProveByTheText(string sql)
    {
        var proofs = Read(sql, Stored(1, "shipped_at", nullable: true));

        proofs.OperandQueries.ShouldBeEmpty();
        proofs.ProofTables.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    // Nothing is proved though every operand asked is answered proving and every table asked lists the column.
    private static void ShouldProveNothing(string sql, SqlServerColumnRow row)
    {
        var proofs = Read(sql, row);

        ShouldAskNoPseudoTable(proofs);
        var asked = proofs.OperandQueries.Sum(static query => query.Count);
        Nullability(proofs.Apply([.. Enumerable.Repeat(Proving, asked)], null, TablesOf(proofs, row)))
            .ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // The rows of "table" for each table the proofs ask: the rows given, each named as its source column, of no
    // sparse column set, and the hidden row.
    private static Dictionary<(string Schema, string Table), IReadOnlyList<SqlServerColumnRow>> TablesOf(
        ColumnProofs proofs,
        params SqlServerColumnRow[] rows
    )
    {
        IReadOnlyList<SqlServerColumnRow> described =
        [
            .. rows.Select(static (row, index) => row with { ColumnOrdinal = index + 1, Name = row.SourceColumn }),
            Rowstat with
            {
                ColumnOrdinal = rows.Length + 1,
            },
        ];
        return proofs.ProofTables.ToDictionary(static table => table, _ => described);
    }

    private static void ShouldAskNoPseudoTable(ColumnProofs proofs)
    {
        foreach (var query in proofs.OperandQueries)
        {
            PseudoTableOrDefault().IsMatch(query.Sql).ShouldBeFalse(query.Sql);
        }
    }

    // The text between the end of the first marker and the start of the next after it.
    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return text[from..text.IndexOf(end, from, StringComparison.Ordinal)];
    }

    [GeneratedRegex(@"\b(inserted|deleted)\s*\]?\s*\.|\bDEFAULT\b", RegexOptions.IgnoreCase)]
    private static partial Regex PseudoTableOrDefault();
}
