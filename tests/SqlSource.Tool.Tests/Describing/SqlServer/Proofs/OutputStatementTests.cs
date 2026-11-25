using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The reader of an INSERT, an UPDATE or a DELETE with OUTPUT: its items, its FROM and WHERE, its target and its SET, or
// null for any text that is not one it can follow.
public sealed class OutputStatementTests
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

    [Fact]
    public void Read_UpdateFromJoin_GivesItemsFromWhereAndAssignments()
    {
        var (tokens, statement) = ReadOne(UpdateFromJoin);

        statement.Kind.ShouldBe(OutputKind.Update);
        statement
            .Rows.Items.Select(static item => item.Name)
            .ShouldBe([
                "id",
                "previous_status",
                "status",
                "shipped_at",
                "ship_city",
                "customer_name",
                "customer_email",
            ]);
        statement.Target.ShouldBe("o");
        statement.TargetIsAlias.ShouldBeTrue();
        statement.Assignments.ShouldNotBeNull().Select(static assignment => assignment.Column).ShouldBe(["status"]);
        Text(tokens, statement.Assignments[0].Value).ShouldBe("'delivered'");
        var rows = statement.Rows.FromThroughWhere.ShouldNotBeNull();
        tokens[rows.Start].Text.ShouldBe("FROM");
        tokens[rows.End - 1].Text.ShouldBe("@shipped_before");
        var where = statement.Rows.Where.ShouldNotBeNull();
        tokens[where.Start].Text.ShouldBe("WHERE");
        statement.Rows.From.ShouldNotBeNull().End.ShouldBe(where.Start);
        statement.Rows.HasFrom.ShouldBeTrue();
        statement.Rows.IsOneTable.ShouldBeFalse();
        statement.Rows.Grouping.ShouldBe(SelectGrouping.None);
        statement.With.IsEmpty.ShouldBeTrue();
        statement.Ctes.ShouldBeEmpty();
    }

    [Fact]
    public void Read_CteFeedingUpdate_KeepsTheWithClause()
    {
        var (tokens, statement) = ReadOne(CteFeedingUpdate);

        var cte = statement.Ctes.ShouldHaveSingleItem();
        cte.Name.ShouldBe("due");
        cte.Branches.ShouldNotBeNull().Count.ShouldBe(1);
        tokens[statement.With.Start].Text.ShouldBe("WITH");
        statement.With.End.ShouldBe(cte.Close + 1);
        tokens[statement.With.End - 1].Text.ShouldBe(")");
        tokens[statement.With.End].Text.ShouldBe("UPDATE");
        statement.Rows.Items.Count.ShouldBe(8);
        statement.Assignments.ShouldNotBeNull().ShouldHaveSingleItem().Column.ShouldBe("note");
        statement.Rows.Where.ShouldBeNull();
        tokens[statement.Rows.FromThroughWhere.ShouldNotBeNull().End - 1].Text.ShouldBe("id");
    }

    [Fact]
    public void Read_UpdateWithoutFrom_TargetIsTheTable()
    {
        var (tokens, statement) = ReadOne("UPDATE dbo.orders SET note = @n OUTPUT inserted.id WHERE id = @id");

        statement.Kind.ShouldBe(OutputKind.Update);
        statement.Target.ShouldBe("orders");
        statement.TargetIsAlias.ShouldBeFalse();
        statement.Rows.FromThroughWhere.ShouldBeNull();
        statement.Rows.From.ShouldBeNull();
        statement.Rows.HasFrom.ShouldBeFalse();
        Text(tokens, statement.Rows.Where.ShouldNotBeNull()).ShouldBe("WHERE id = @id");
        statement.Rows.Items.ShouldHaveSingleItem().Name.ShouldBe("id");
        statement.IsTargetColumn(tokens, ["id"], "id").ShouldBeTrue();
        statement.IsTargetColumn(tokens, ["orders", "id"], "id").ShouldBeTrue();
        statement.IsTargetColumn(tokens, ["dbo", "orders", "id"], "id").ShouldBeFalse();
        statement.IsTargetColumn(tokens, ["note"], "id").ShouldBeFalse();
    }

    // The clauses an UPDATE may hold around its SET and OUTPUT, each once and in order.
    [Fact]
    public void Read_UpdateWithTopHintsAndOption_EndsTheWhereAtTheOption()
    {
        var (tokens, statement) = ReadOne(
            "UPDATE TOP (5) PERCENT [dbo].[t] WITH (ROWLOCK) SET a = 1, b = CASE WHEN c = 1 THEN 2 END "
                + "OUTPUT inserted.a WHERE b = 2 OPTION (RECOMPILE)"
        );

        statement.Target.ShouldBe("t");
        statement.Assignments.ShouldNotBeNull().Select(static assignment => assignment.Column).ShouldBe(["a", "b"]);
        Text(tokens, statement.Rows.Where.ShouldNotBeNull()).ShouldBe("WHERE b = 2");
    }

    [Fact]
    public void Read_DeleteWithFrom_GivesItemsFromWhere()
    {
        var (tokens, statement) = ReadOne(
            "DELETE o OUTPUT deleted.id FROM dbo.orders AS o JOIN dbo.customers AS c ON c.id = o.customer_id "
                + "WHERE o.shipped_at < @x"
        );

        statement.Kind.ShouldBe(OutputKind.Delete);
        statement.Rows.Items.ShouldHaveSingleItem().Name.ShouldBe("id");
        statement.Target.ShouldBe("o");
        statement.TargetIsAlias.ShouldBeTrue();
        statement.Assignments.ShouldNotBeNull().ShouldBeEmpty();
        statement.Writes("shipped_at").ShouldBeFalse();
        Text(tokens, statement.Rows.From.ShouldNotBeNull())
            .ShouldBe("FROM dbo . orders AS o JOIN dbo . customers AS c ON c . id = o . customer_id");
        Text(tokens, statement.Rows.Where.ShouldNotBeNull()).ShouldBe("WHERE o . shipped_at < @x");
        var rows = statement.Rows.FromThroughWhere.ShouldNotBeNull();
        tokens[rows.Start].Text.ShouldBe("FROM");
        tokens[rows.End - 1].Text.ShouldBe("@x");
        statement.Rows.HasFrom.ShouldBeTrue();
        statement.IsTargetColumn(tokens, ["o", "shipped_at"], "shipped_at").ShouldBeTrue();
        statement.IsTargetColumn(tokens, ["shipped_at"], "shipped_at").ShouldBeFalse();
    }

    // The first FROM of a DELETE is optional, and comes before its target.
    [Theory]
    [InlineData("DELETE FROM dbo.orders OUTPUT deleted.id WHERE id = @id")]
    [InlineData("DELETE dbo.orders OUTPUT deleted.id WHERE id = @id")]
    public void Read_DeleteWithoutFrom_TargetIsTheTable(string sql)
    {
        var (tokens, statement) = ReadOne(sql);

        statement.Kind.ShouldBe(OutputKind.Delete);
        statement.Target.ShouldBe("orders");
        statement.TargetIsAlias.ShouldBeFalse();
        statement.Assignments.ShouldNotBeNull().ShouldBeEmpty();
        statement.Rows.FromThroughWhere.ShouldBeNull();
        statement.Rows.From.ShouldBeNull();
        statement.Rows.HasFrom.ShouldBeFalse();
        Text(tokens, statement.Rows.Where.ShouldNotBeNull()).ShouldBe("WHERE id = @id");
        statement.Rows.Items.ShouldHaveSingleItem().Name.ShouldBe("id");
        statement.IsTargetColumn(tokens, ["id"], "id").ShouldBeTrue();
    }

    // The clauses a DELETE may hold around its OUTPUT, each once and in order.
    [Fact]
    public void Read_DeleteWithTopHintsAndOption_EndsTheWhereAtTheOption()
    {
        var (tokens, statement) = ReadOne(
            "DELETE TOP (5) PERCENT FROM [dbo].[t] WITH (ROWLOCK) OUTPUT deleted.a WHERE b = 2 OPTION (RECOMPILE)"
        );

        statement.Target.ShouldBe("t");
        Text(tokens, statement.Rows.Where.ShouldNotBeNull()).ShouldBe("WHERE b = 2");
    }

    [Fact]
    public void Read_InsertValues_GivesTheItems()
    {
        var (tokens, statement) = ReadOne("INSERT INTO dbo.t (a) OUTPUT inserted.p, inserted.id VALUES (@x)");

        statement.Kind.ShouldBe(OutputKind.Insert);
        statement.Rows.Items.Select(static item => item.Name).ShouldBe(["p", "id"]);
        statement.Rows.Items.Select(item => Text(tokens, item.Expression)).ShouldBe(["inserted . p", "inserted . id"]);
        statement.Target.ShouldBe("t");
        statement.TargetIsAlias.ShouldBeFalse();
        statement.Assignments.ShouldBeNull();
        statement.Rows.FromThroughWhere.ShouldBeNull();
        statement.Rows.From.ShouldBeNull();
        statement.Rows.Where.ShouldBeNull();
        statement.Rows.HasFrom.ShouldBeFalse();
    }

    [Theory]
    [InlineData("INSERT dbo.t OUTPUT inserted.id DEFAULT VALUES")]
    [InlineData("INSERT TOP (1) INTO dbo.t WITH (TABLOCK) (a) OUTPUT inserted.id SELECT a FROM dbo.s WHERE a > 0")]
    [InlineData("INSERT INTO dbo.t OUTPUT inserted.id EXEC dbo.p @a = 1")]
    [InlineData("INSERT INTO dbo.t OUTPUT inserted.id EXECUTE dbo.p")]
    [InlineData(";WITH c AS (SELECT 1 AS a) INSERT INTO dbo.t (a) OUTPUT inserted.id SELECT a FROM c;")]
    public void Read_InsertOfEachSource_GivesTheItem(string sql) =>
        Read(sql).Statement.ShouldNotBeNull().Rows.Items.ShouldHaveSingleItem().Name.ShouldBe("id");

    [Theory]
    [InlineData(
        "MERGE dbo.t AS o USING dbo.s AS s ON s.id = o.id WHEN MATCHED THEN UPDATE SET o.a = s.a OUTPUT inserted.a;"
    )]
    [InlineData("MERGE dbo.t AS o USING dbo.s AS s ON s.id = o.id WHEN MATCHED THEN DELETE OUTPUT deleted.a;")]
    [InlineData("SELECT a FROM dbo.t")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a INTO @t WHERE id = 1")]
    [InlineData("INSERT INTO dbo.t (a) OUTPUT inserted.a INTO @t VALUES (1)")]
    [InlineData("UPDATE dbo.t SET output = 1 OUTPUT inserted.a")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a FROM dbo.t AS o")]
    [InlineData("UPDATE t SET a = 1 OUTPUT inserted.a FROM dbo.t AS o")]
    [InlineData("UPDATE t SET a = 1 OUTPUT inserted.a FROM other.t")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.a FROM dbo.t AS o JOIN dbo.u AS o ON o.id = 1")]
    [InlineData("UPDATE x SET a = 1 OUTPUT inserted.a FROM (SELECT a FROM dbo.t) AS x")]
    [InlineData("UPDATE o SET a = 1 OUTPUT inserted.a FROM dbo.t AS o, dbo.u AS u")]
    [InlineData("WITH c AS (SELECT a FROM dbo.t) UPDATE c SET a = 1 OUTPUT inserted.a")]
    [InlineData("WITH c AS (SELECT a FROM dbo.t) UPDATE C SET a = 1 OUTPUT inserted.a")]
    [InlineData("WITH c AS (SELECT a FROM dbo.t) UPDATE o SET a = 1 OUTPUT inserted.a FROM c AS o")]
    [InlineData("WITH c AS (SELECT a FROM dbo.t) INSERT INTO c (a) OUTPUT inserted.a VALUES (1)")]
    [InlineData("WITH XMLNAMESPACES ('u' AS x) UPDATE dbo.t SET a = 1 OUTPUT inserted.a")]
    [InlineData("UPDATE @t SET a = 1 OUTPUT inserted.a")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a WHERE CURRENT OF k")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a WHERE id = 1; SELECT 1")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a WHERE id = 1 SELECT 1")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a SET b = 2")]
    [InlineData("INSERT INTO dbo.t (a) OUTPUT inserted.a VALUES (1) DELETE dbo.t")]
    [InlineData("UPDATE dbo.t SET a = 1 WHERE id = 1 OUTPUT inserted.a")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a WHERE id = 1 FROM dbo.t")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a WHERE id = 1 WHERE id = 2")]
    [InlineData("UPDATE dbo.t SET a = 1")]
    [InlineData("UPDATE dbo.t OUTPUT inserted.a SET a = 1")]
    [InlineData("UPDATE dbo.t SET a = (1 OUTPUT inserted.a")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT")]
    [InlineData("UPDATE dbo.t SET a = 1 OUTPUT inserted.a, WHERE id = 1")]
    [InlineData("UPDATE a.b.c.d.e SET a = 1 OUTPUT inserted.a")]
    [InlineData("UPDATE TOP 5 dbo.t SET a = 1 OUTPUT inserted.a")]
    [InlineData("INSERT INTO dbo.t OUTPUT inserted.a")]
    [InlineData("INSERT INTO dbo.t OUTPUT inserted.a FROM dbo.s SELECT 1")]
    [InlineData("INSERT INTO dbo.t (a) VALUES (1) OUTPUT inserted.a")]
    [InlineData("INSERT INTO dbo.t OUTPUT inserted.a DEFAULT VALUES SELECT 1")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a INTO @t WHERE id = 1")]
    [InlineData("DELETE FROM dbo.t SET output = 1 OUTPUT deleted.a")]
    [InlineData("DELETE dbo.t OUTPUT deleted.a FROM dbo.t AS o")]
    [InlineData("DELETE t OUTPUT deleted.a FROM dbo.t AS o")]
    [InlineData("DELETE FROM t OUTPUT deleted.a FROM other.t")]
    [InlineData("DELETE o OUTPUT deleted.a FROM dbo.t AS o JOIN dbo.u AS o ON o.id = 1")]
    [InlineData("DELETE x OUTPUT deleted.a FROM (SELECT a FROM dbo.t) AS x")]
    [InlineData("DELETE o OUTPUT deleted.a FROM dbo.t AS o, dbo.u AS u")]
    [InlineData("WITH c AS (SELECT a FROM dbo.t) DELETE c OUTPUT deleted.a")]
    [InlineData("WITH c AS (SELECT a FROM dbo.t) DELETE FROM C OUTPUT deleted.a")]
    [InlineData("WITH c AS (SELECT a FROM dbo.t) DELETE o OUTPUT deleted.a FROM c AS o")]
    [InlineData("WITH XMLNAMESPACES ('u' AS x) DELETE FROM dbo.t OUTPUT deleted.a")]
    [InlineData("DELETE @t OUTPUT deleted.a")]
    [InlineData("DELETE FROM @t OUTPUT deleted.a")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a WHERE CURRENT OF k")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a WHERE id = 1; SELECT 1")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a WHERE id = 1 SELECT 1")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a DELETE dbo.t")]
    [InlineData("DELETE FROM dbo.t WHERE id = 1 OUTPUT deleted.a")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a WHERE id = 1 FROM dbo.t")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a WHERE id = 1 WHERE id = 2")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a OPTION (RECOMPILE) WHERE id = 1")]
    [InlineData("DELETE FROM dbo.t WHERE id = 1")]
    [InlineData("DELETE FROM dbo.t OUTPUT")]
    [InlineData("DELETE FROM dbo.t OUTPUT deleted.a, WHERE id = 1")]
    [InlineData("DELETE FROM a.b.c.d.e OUTPUT deleted.a")]
    [InlineData("DELETE TOP 5 FROM dbo.t OUTPUT deleted.a")]
    [InlineData("DELETE FROM FROM dbo.t OUTPUT deleted.a")]
    [InlineData("DELETE FROM OUTPUT deleted.a")]
    [InlineData("DELETE FROM dbo.t x OUTPUT deleted.a")]
    public void Read_NotOneToFollow_IsNull(string sql) => Read(sql).Statement.ShouldBeNull();

    // A SET item that is not "c = v", "q.c = v" of the target, or "@v = e" leaves the SET unread, and the statement
    // read.
    [Theory]
    [InlineData("@v = a = 1")]
    [InlineData("a += 1")]
    [InlineData("g.STSrid = 1")]
    [InlineData("x.a = 1")]
    [InlineData("a.WRITE(N'x', 0, 1)")]
    [InlineData("a = 1, a.WRITE(N'x', 0, 1)")]
    [InlineData("dbo.t.a = 1")]
    [InlineData("a =")]
    public void Read_UnreadableSet_HasNullAssignments(string set)
    {
        var statement = Read($"UPDATE o SET {set} OUTPUT inserted.a FROM dbo.t AS o").Statement;

        statement.ShouldNotBeNull().Assignments.ShouldBeNull();
        statement.Writes("anything").ShouldBeTrue();
    }

    // "@v = e" assigns a variable and writes no column.
    [Fact]
    public void Read_SetOfAVariable_GivesNoAssignment()
    {
        var statement = Read("UPDATE o SET @v = 1, o.a = @v OUTPUT inserted.a FROM dbo.t AS o").Statement;

        statement.ShouldNotBeNull().Assignments.ShouldNotBeNull().ShouldHaveSingleItem().Column.ShouldBe("a");
    }

    [Fact]
    public void Writes_MatchesASpellingTheCollationMayEquate()
    {
        var (tokens, statement) = ReadOne(
            "UPDATE o SET Shipped_At = NULL, NOTE = 'x' OUTPUT inserted.id "
                + "FROM dbo.orders AS o JOIN dbo.customers AS u ON u.id = o.customer_id"
        );

        statement.Writes("shipped_at").ShouldBeTrue();
        statement.Writes("SHIPPED_AT").ShouldBeTrue();
        statement.Writes("note").ShouldBeTrue();
        statement.Writes("status").ShouldBeFalse();
        statement.IsTargetColumn(tokens, ["o", "shipped_at"], "shipped_at").ShouldBeTrue();
        statement.IsTargetColumn(tokens, ["O", "shipped_at"], "shipped_at").ShouldBeFalse();
        statement.IsTargetColumn(tokens, ["u", "shipped_at"], "shipped_at").ShouldBeFalse();
        statement.IsTargetColumn(tokens, ["shipped_at"], "shipped_at").ShouldBeFalse();
        statement.IsTargetColumn(tokens, ["o", "Shipped_At"], "shipped_at").ShouldBeFalse();
    }

    // SQL Server ignores a name's trailing spaces, and a collation may equate a name of any character but an ASCII
    // letter, a digit, _, @, # or $ with another spelling, so such a name may write every column.
    [Theory]
    [InlineData("[b ] = NULL", "b")]
    [InlineData("[b  ] = NULL", "b")]
    [InlineData("[B ] = NULL", "b")]
    [InlineData("o.[b ] = NULL", "b")]
    [InlineData("b = NULL", "b ")]
    [InlineData("[ｂ] = NULL", "b")]
    [InlineData("[ß] = NULL", "ss")]
    [InlineData("ss = NULL", "ß")]
    [InlineData("[æ] = NULL", "ae")]
    [InlineData("[b c] = NULL", "a")]
    [InlineData("[a-b] = NULL", "a")]
    public void Writes_ANameTheServerMayEquate_WritesTheColumn(string set, string column) =>
        ReadOne($"UPDATE o SET {set} OUTPUT inserted.b FROM dbo.t AS o").Statement.Writes(column).ShouldBeTrue();

    [Theory]
    [InlineData("a = NULL", "b")]
    [InlineData("[a ] = NULL", "b")]
    [InlineData("a = NULL", "b ")]
    [InlineData("a_1 = NULL, [$a#] = NULL", "a_2")]
    public void Writes_AnotherPlainName_DoesNotWriteTheColumn(string set, string column) =>
        ReadOne($"UPDATE o SET {set} OUTPUT inserted.b FROM dbo.t AS o").Statement.Writes(column).ShouldBeFalse();

    // The one assignment that writes a column is its value's only when it names the column ordinally.
    [Fact]
    public void AssignmentOf_OneAssignmentOfTheSameName_GivesIt()
    {
        var (tokens, statement) = ReadOne(
            "UPDATE o SET Shipped_At = NULL, o.note = 'x' OUTPUT inserted.id FROM dbo.t AS o"
        );

        var note = statement.AssignmentOf("note").ShouldNotBeNull();
        Text(tokens, note.Value).ShouldBe("'x'");
        statement.AssignmentOf("shipped_at").ShouldBeNull();
        statement.AssignmentOf("Shipped_At").ShouldNotBeNull().Column.ShouldBe("Shipped_At");
        statement.AssignmentOf("status").ShouldBeNull();
    }

    // A name the server may equate with any column is the assignment of its own spelling alone.
    [Theory]
    [InlineData("[ß] = 1", "ß")]
    [InlineData("[b ] = 1, a = 2", "b ")]
    public void AssignmentOf_AlonePossiblyEveryColumn_GivesItForItsOwnSpelling(string set, string column) =>
        ReadOne($"UPDATE o SET {set} OUTPUT inserted.a FROM dbo.t AS o")
            .Statement.AssignmentOf(column)
            .ShouldNotBeNull()
            .Column.ShouldBe(column);

    [Theory]
    [InlineData("UPDATE o SET a = 1, A = 2 OUTPUT inserted.a FROM dbo.t AS o")]
    [InlineData("UPDATE o SET [a ] = 1 OUTPUT inserted.a FROM dbo.t AS o")]
    [InlineData("UPDATE o SET a = 1, [ß] = 2 OUTPUT inserted.a FROM dbo.t AS o")]
    [InlineData("UPDATE o SET a += 1 OUTPUT inserted.a FROM dbo.t AS o")]
    [InlineData("DELETE o OUTPUT deleted.a FROM dbo.t AS o")]
    [InlineData("INSERT INTO dbo.t (a) OUTPUT inserted.a VALUES (1)")]
    public void AssignmentOf_NoneOrSeveralOrUnread_IsNull(string sql) =>
        ReadOne(sql).Statement.AssignmentOf("a").ShouldBeNull();

    // With a FROM of one source, that source is the target, so a column alone is the target's.
    [Fact]
    public void IsTargetColumn_FromOfOneSource_TakesAColumnAlone()
    {
        var (tokens, statement) = ReadOne("UPDATE o SET a = 1 OUTPUT inserted.a FROM dbo.t AS o WHERE b IS NOT NULL");

        statement.IsTargetColumn(tokens, ["b"], "b").ShouldBeTrue();
        statement.IsTargetColumn(tokens, ["o", "b"], "b").ShouldBeTrue();
        statement.IsTargetColumn(tokens, ["t", "b"], "b").ShouldBeFalse();
    }

    private static (IReadOnlyList<TsqlToken> Tokens, OutputStatement? Statement) Read(string sql)
    {
        var tokens = TsqlTokens.Read(sql);
        return (tokens, OutputStatement.Read(tokens));
    }

    private static (IReadOnlyList<TsqlToken> Tokens, OutputStatement Statement) ReadOne(string sql)
    {
        var (tokens, statement) = Read(sql);
        return (tokens, statement.ShouldNotBeNull());
    }

    private static string Text(IReadOnlyList<TsqlToken> tokens, TokenRange range) =>
        string.Join(' ', tokens.Skip(range.Start).Take(range.Count).Select(static token => token.Text));
}
