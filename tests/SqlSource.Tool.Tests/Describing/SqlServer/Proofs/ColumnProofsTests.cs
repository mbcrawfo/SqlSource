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
