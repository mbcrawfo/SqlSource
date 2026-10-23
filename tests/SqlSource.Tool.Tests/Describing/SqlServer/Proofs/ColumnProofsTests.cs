using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The proofs of one describe's columns: which operands they ask, and the columns as the answer corrects them.  The rows
// are the server's answer of "columns", built as the session builds them.
public sealed class ColumnProofsTests
{
    [Fact]
    public void Read_NothingToAsk_OperandsSqlIsNull()
    {
        var proofs = Read("SELECT COUNT(*) AS n FROM dbo.users", ExpressionRow(1, "n"));

        proofs.OperandsSql.ShouldBeNull();
        proofs.OperandCount.ShouldBe(0);
        Nullability(proofs.Apply(null)).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    [Fact]
    public void Apply_OperandNotNull_ColumnProved()
    {
        var proofs = Read("SELECT COALESCE(nickname, name) AS shown FROM dbo.users", ExpressionRow(1, "shown"));

        proofs.OperandsSql.ShouldBe("SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users");
        proofs.OperandCount.ShouldBe(2);
        Nullability(proofs.Apply([false, true])).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply([false, false])).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // The operands are one list for the describe, and each column's proof reads its own of them.
    [Fact]
    public void Apply_OperandsSharedByTwoColumns_EachColumnByItsOwn()
    {
        var proofs = Read(
            "SELECT COALESCE(nickname, name) AS a, CAST(nickname AS nvarchar(60)) AS b FROM dbo.users",
            ExpressionRow(1, "a"),
            ExpressionRow(2, "b")
        );

        proofs.OperandsSql.ShouldBe("SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users");
        Nullability(proofs.Apply([false, true]))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression), (true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply([true, false]))
            .ShouldBe([
                (false, SidecarValues.NullableSource.Expression),
                (false, SidecarValues.NullableSource.Expression),
            ]);
    }

    // A column alone is what "columns" answered already, over the same rows: it asks nothing and keeps the server's
    // answer.
    [Fact]
    public void Read_BareNullableColumn_AsksNothing()
    {
        var proofs = Read(
            "SELECT id, nickname FROM dbo.users",
            IdColumn,
            ExpressionRow(2, "nickname") with
            {
                SourceDatabase = Database,
                SourceSchema = "dbo",
                SourceTable = "users",
                SourceColumn = "nickname",
                SourceType = "U",
            }
        );

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // The reader has misread the select list: no column of it is proved.
    [Fact]
    public void Read_NameDiffers_ProvesNothing()
    {
        var proofs = Read("SELECT COUNT(*) AS n FROM dbo.users", ExpressionRow(1, "other"));

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_CountDiffers_ProvesNothing()
    {
        var proofs = Read("SELECT COUNT(*) AS n, COUNT(*) AS m FROM dbo.users", ExpressionRow(1, "n"));

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_RefusedText_ProvesNothing()
    {
        var proofs = Read(
            "SELECT COUNT(*) AS n FROM dbo.users UNION ALL SELECT COUNT(*) AS n FROM dbo.orders",
            ExpressionRow(1, "n")
        );

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_StarAmongTheItems_ProvesNothing()
    {
        var proofs = Read("SELECT u.*, COUNT(*) OVER () AS n FROM dbo.users AS u", IdColumn, ExpressionRow(2, "n"));

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // A column the server answers not null needs no proof, and asks nothing.
    [Fact]
    public void Apply_ServerAlreadyNotNull_KeepsServer()
    {
        var proofs = Read(
            "SELECT COALESCE(nickname, name) AS shown FROM dbo.users",
            ExpressionRow(1, "shown") with
            {
                IsNullable = false,
            }
        );

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null)).ShouldBe([(false, SidecarValues.NullableSource.Server)]);
    }

    // A PERSISTED NOT NULL column read from its one table alone: every row of the result is a row of the table, which
    // the constraint keeps not null.  The catalog says so, and no operand is asked.
    [Theory]
    [InlineData("SELECT id, name_len_p FROM dbo.users WHERE id = @id")]
    [InlineData("SELECT u.id, u.name_len_p AS name_len_p FROM dbo.users AS u WITH (NOLOCK) ORDER BY u.id")]
    [InlineData("SELECT DISTINCT TOP (5) id, name_len_p FROM dbo.users GROUP BY id, name_len_p")]
    [InlineData("WITH n AS (SELECT 1 AS one) SELECT id, name_len_p FROM [dbo].[users]")]
    public void Apply_PersistedNotNullFromOneTable_Catalog(string sql)
    {
        var proofs = Read(sql, IdColumn, PersistedColumn(2));

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Catalog)]);
    }

    // A * or a q.* that is the only item is every column of the one table, each corrected by its row.
    [Theory]
    [InlineData("SELECT * FROM dbo.users")]
    [InlineData("SELECT u.* FROM dbo.users AS u WHERE u.id = @id")]
    public void Apply_PersistedNotNullByStar_Catalog(string sql)
    {
        var proofs = Read(sql, IdColumn, ExpressionRow(2, "nickname"), PersistedColumn(3));

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null))
            .ShouldBe([
                (false, SidecarValues.NullableSource.Server),
                (true, SidecarValues.NullableSource.Server),
                (false, SidecarValues.NullableSource.Catalog),
            ]);
    }

    // The column through a join, a derived table or a CTE, under a grouping set, as an operand of an expression or of
    // a scalar subquery, in parentheses, which IsColumn does not take for a column alone, or by a * that is not the
    // one item of one table: the server's answer stands, since a row of the result may be no row of the table (an
    // outer join gives NULL).
    [Theory]
    [InlineData("SELECT o.id, u.name_len_p FROM dbo.orders AS o LEFT JOIN dbo.users AS u ON u.id = o.user_id")]
    [InlineData("SELECT o.id, u.name_len_p FROM dbo.orders AS o JOIN dbo.users AS u ON u.id = o.user_id")]
    [InlineData("SELECT u.id, u.name_len_p FROM (SELECT * FROM dbo.users) AS u")]
    [InlineData("WITH users AS (SELECT * FROM dbo.users) SELECT id, name_len_p FROM users")]
    [InlineData("SELECT id, name_len_p FROM dbo.users GROUP BY ROLLUP (id, name_len_p)")]
    [InlineData("SELECT id, name_len_p FROM dbo.users GROUP BY GROUPING SETS ((id, name_len_p), ())")]
    [InlineData("SELECT id, name_len_p FROM dbo.users GROUP BY ALL id, name_len_p")]
    [InlineData("SELECT id, CAST(name_len_p AS bigint) AS name_len_p FROM dbo.users")]
    [InlineData("SELECT id, (SELECT name_len_p FROM dbo.users WHERE id = 0) AS name_len_p FROM dbo.users")]
    [InlineData("SELECT id, (name_len_p) AS name_len_p FROM dbo.users")]
    [InlineData("SELECT id, name_len_p FROM dbo.users UNION ALL SELECT id, name_len_p FROM dbo.users")]
    [InlineData("SELECT *, 1 AS one FROM dbo.users")]
    [InlineData("SELECT u.*, u.* FROM dbo.users AS u")]
    [InlineData("SELECT u.* FROM dbo.orders AS o LEFT JOIN dbo.users AS u ON u.id = o.user_id")]
    public void Apply_PersistedNotNull_StaysNullable(string sql)
    {
        var proofs = Read(sql, IdColumn, PersistedColumn(2));

        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // A column whose row does not say PERSISTED NOT NULL, or says nothing, as a capture before the member, or whose
    // source is a view or of no type, keeps the server's answer even from one table.
    [Theory]
    [InlineData(false, "U")]
    [InlineData(null, "U")]
    [InlineData(true, "V")]
    [InlineData(true, null)]
    public void Apply_NotPersistedNotNullOfATable_StaysNullable(bool? persisted, string? sourceType)
    {
        var proofs = Read(
            "SELECT id, name_len_p FROM dbo.users",
            IdColumn,
            PersistedColumn(2) with
            {
                PersistedNotNull = persisted,
                SourceType = sourceType,
            }
        );

        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // A column the server answers not null keeps its answer, and its source, whatever the catalog says.
    [Fact]
    public void Apply_PersistedNotNullServerAlreadyNotNull_KeepsServer()
    {
        var proofs = Read(
            "SELECT id, name_len_p FROM dbo.users",
            IdColumn,
            PersistedColumn(2) with
            {
                IsNullable = false,
            }
        );

        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Server)]);
    }

    // The rows of "table", SELECT * from the table alone, are corrected as the columns of such a query are, so the
    // match compares a corrected query with a corrected table; every other row comes as it came.
    [Fact]
    public void TableRows_PersistedNotNull_NotNullable()
    {
        IReadOnlyList<SqlServerColumnRow> rows =
        [
            IdColumn,
            ExpressionRow(2, "nickname"),
            PersistedColumn(3),
            PersistedColumn(4) with
            {
                PersistedNotNull = false,
            },
            PersistedColumn(5) with
            {
                SourceType = "V",
            },
            PersistedColumn(6) with
            {
                SourceType = null,
            },
            Rowstat with
            {
                ColumnOrdinal = 7,
            },
        ];

        ColumnProofs
            .TableRows(rows)
            .Select(static row => row.IsNullable)
            .ShouldBe([false, true, false, true, true, true, false]);
    }

    // The proofs of the SQL over the rows, built as the session builds them, the hidden row of browse mode 2 after
    // them.
    private static ColumnProofs Read(string sql, params SqlServerColumnRow[] visible)
    {
        IReadOnlyList<SqlServerColumnRow> rows = [.. visible, Rowstat with { ColumnOrdinal = visible.Length + 1 }];
        var built = SqlServerColumns.Build(rows, Database).Value.ShouldNotBeNull();
        return ColumnProofs.Read(sql, built, rows);
    }

    private static List<(bool? Nullable, string? Source)> Nullability(IReadOnlyList<BuiltColumn> columns) =>
        [.. columns.Select(static column => (column.Column.Nullable, column.Column.NullableSource))];
}
