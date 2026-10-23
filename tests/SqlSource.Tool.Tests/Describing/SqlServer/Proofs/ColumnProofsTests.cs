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
        var proofs = Read("SELECT COUNT(*) AS n FROM dbo.users", Expression(1, "n"));

        proofs.OperandsSql.ShouldBeNull();
        proofs.OperandCount.ShouldBe(0);
        Nullability(proofs.Apply(null)).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    [Fact]
    public void Apply_OperandNotNull_ColumnProved()
    {
        var proofs = Read("SELECT COALESCE(nickname, name) AS shown FROM dbo.users", Expression(1, "shown"));

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
            Expression(1, "a"),
            Expression(2, "b")
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

    // The reader has misread the select list: no column of it is proved.
    [Fact]
    public void Read_NameDiffers_ProvesNothing()
    {
        var proofs = Read("SELECT COUNT(*) AS n FROM dbo.users", Expression(1, "other"));

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_CountDiffers_ProvesNothing()
    {
        var proofs = Read("SELECT COUNT(*) AS n, COUNT(*) AS m FROM dbo.users", Expression(1, "n"));

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_RefusedText_ProvesNothing()
    {
        var proofs = Read(
            "SELECT COUNT(*) AS n FROM dbo.users UNION ALL SELECT COUNT(*) AS n FROM dbo.orders",
            Expression(1, "n")
        );

        proofs.OperandsSql.ShouldBeNull();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_StarAmongTheItems_ProvesNothing()
    {
        var proofs = Read("SELECT u.*, COUNT(*) OVER () AS n FROM dbo.users AS u", IdColumn, Expression(2, "n"));

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
            Expression(1, "shown") with
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

    // The row of a column of no origin that the server answers nullable.
    private static SqlServerColumnRow Expression(int ordinal, string name) =>
        IdColumn with
        {
            ColumnOrdinal = ordinal,
            Name = name,
            IsNullable = true,
            SourceDatabase = null,
            SourceSchema = null,
            SourceTable = null,
            SourceColumn = null,
            SourceType = null,
            IsIdentityColumn = null,
            IsComputedColumn = null,
        };

    private static List<(bool? Nullable, string? Source)> Nullability(IReadOnlyList<BuiltColumn> columns) =>
        [.. columns.Select(static column => (column.Column.Nullable, column.Column.NullableSource))];
}
