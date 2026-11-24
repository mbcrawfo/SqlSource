using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Columns;
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

        proofs.OperandQueries.ShouldBeEmpty();
        proofs.OperandQueries.Count.ShouldBe(0);
        Nullability(proofs.Apply(null)).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
    }

    [Fact]
    public void Apply_OperandNotNull_ColumnProved()
    {
        var proofs = Read("SELECT COALESCE(nickname, name) AS shown FROM dbo.users", ExpressionRow(1, "shown"));

        proofs
            .OperandQueries.ShouldHaveSingleItem()
            .Sql.ShouldBe("SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users");
        proofs.OperandQueries.ShouldHaveSingleItem().Count.ShouldBe(2);
        Nullability(proofs.Apply([new OperandFacts(false, true, Stored: true), new OperandFacts(true, true)]))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply(Answers(false, true))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(Answers(false, false))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
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

        proofs
            .OperandQueries.ShouldHaveSingleItem()
            .Sql.ShouldBe("SELECT nickname\nAS [1],\nname\nAS [2]\nFROM dbo.users");
        Nullability(proofs.Apply([new OperandFacts(false, true, Stored: true), new OperandFacts(true, true)]))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression), (true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(Answers(true, false)))
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

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // The reader has misread the select list: no column of it is proved.
    [Fact]
    public void Read_NameDiffers_ProvesNothing()
    {
        var proofs = Read("SELECT COUNT(*) AS n FROM dbo.users", ExpressionRow(1, "other"));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_CountDiffers_ProvesNothing()
    {
        var proofs = Read("SELECT COUNT(*) AS n, COUNT(*) AS m FROM dbo.users", ExpressionRow(1, "n"));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // A column of a set operator is proved when its item is proved in every branch, each asked over its own rows.
    [Fact]
    public void Apply_SetOperator_ProvedWhenEveryBranchIs()
    {
        var proofs = Read(
            "SELECT nickname AS shown FROM dbo.users UNION SELECT name FROM dbo.orders",
            ExpressionRow(1, "shown")
        );

        proofs.OperandQueries.ShouldBe([
            new ColumnProofs.OperandQuery("SELECT nickname\nAS [1]\nFROM dbo.users", 1),
            new ColumnProofs.OperandQuery("SELECT name\nAS [1]\nFROM dbo.orders", 1),
        ]);
        Nullability(proofs.Apply(Answers(true, true))).ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply(Answers(true, false))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(Answers(false, true))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Theory]
    [InlineData("SELECT COUNT(*) AS n FROM dbo.users UNION ALL SELECT COUNT(*) FROM dbo.orders", false)]
    [InlineData("SELECT COUNT(*) AS n FROM dbo.users EXCEPT SELECT 1", false)]
    [InlineData("SELECT COUNT(*) AS n FROM dbo.users INTERSECT SELECT COUNT(*) FROM dbo.orders", false)]
    [InlineData("SELECT COUNT(*) AS n FROM dbo.users UNION ALL SELECT NULL", true)]
    [InlineData("SELECT NULL AS n UNION ALL SELECT COUNT(*) FROM dbo.users", true)]
    [InlineData("SELECT COUNT(*) AS n FROM dbo.users UNION ALL SELECT 1, 2", true)]
    [InlineData("SELECT COUNT(*) AS n FROM dbo.users UNION ALL SELECT * FROM dbo.orders", true)]
    [InlineData("SELECT COUNT(*) AS other FROM dbo.users UNION ALL SELECT COUNT(*) AS n FROM dbo.orders", true)]
    public void Apply_SetOperatorOfTheText_ProvedOnlyByEveryBranch(string sql, bool nullable)
    {
        var proofs = Read(sql, ExpressionRow(1, "n"));

        Nullability(proofs.Apply(null))
            .ShouldBe([
                (nullable, nullable ? SidecarValues.NullableSource.Server : SidecarValues.NullableSource.Expression),
            ]);
    }

    // The operands of a later branch come after the earlier branch's in the one list of answers.
    [Fact]
    public void Apply_SecondColumnOfASetOperator_ReadsItsBranchesOwnOperands()
    {
        var proofs = Read(
            "SELECT a AS x, b AS y FROM dbo.t UNION ALL SELECT c, d FROM dbo.u",
            ExpressionRow(1, "x"),
            ExpressionRow(2, "y")
        );

        proofs.OperandQueries.Select(static query => query.Count).ShouldBe([2, 2]);
        Nullability(proofs.Apply(Answers(true, false, true, false)))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression), (true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(Answers(false, true, false, true)))
            .ShouldBe([(true, SidecarValues.NullableSource.Server), (false, SidecarValues.NullableSource.Expression)]);
    }

    // A filter proves a column only when the server shows it stored, since the result reads the value again.
    [Theory]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname IS NOT NULL")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname LIKE @q")]
    [InlineData("SELECT u.nickname FROM dbo.users AS u WHERE u.nickname > 'a' AND u.id > 0")]
    [InlineData("WITH q AS (SELECT nickname FROM dbo.users WHERE nickname > 'a') SELECT nickname FROM q")]
    [InlineData("WITH q AS (SELECT nickname FROM dbo.users WHERE nickname > 'a') SELECT r.nickname FROM q AS r")]
    [InlineData("WITH q (nickname) AS (SELECT n FROM dbo.users WHERE n > 'a') SELECT nickname FROM q")]
    [InlineData(
        "WITH p AS (SELECT nickname FROM dbo.users WHERE nickname > 'a'), q AS (SELECT nickname FROM p) "
            + "SELECT nickname FROM q"
    )]
    [InlineData(
        "WITH q AS (SELECT id, nickname FROM dbo.users WHERE nickname > 'a') "
            + "SELECT q.nickname FROM q JOIN dbo.orders AS o ON o.user_id = q.id"
    )]
    public void Apply_FilteredColumn_ProvedWhenStored(string sql)
    {
        var proofs = Read(sql, ExpressionRow(1, "nickname"));

        proofs.OperandQueries.ShouldHaveSingleItem().Count.ShouldBe(1);
        Nullability(proofs.Apply([new OperandFacts(false, true, Stored: true)]))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply([new OperandFacts(false, true, Stored: false)]))
            .ShouldBe([(true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // What no filter keeps from NULL asks nothing of a lone column.
    [Theory]
    [InlineData("SELECT nickname FROM dbo.users WHERE name IS NOT NULL")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname IS NULL")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname > 'a' OR id > 0")]
    [InlineData("SELECT nickname FROM dbo.users WHERE NOT nickname > 'a'")]
    [InlineData("SELECT u.nickname FROM dbo.users AS u WHERE nickname > 'a'")]
    [InlineData("SELECT nickname FROM dbo.users WHERE NICKNAME > 'a'")]
    // A text that turns ANSI_NULLS off is several statements, which the reader does not read.
    [InlineData("SET ANSI_NULLS OFF; SELECT nickname FROM dbo.users WHERE nickname = @p")]
    [InlineData("SET ANSI_NULLS OFF SELECT nickname FROM dbo.users WHERE nickname = @p")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname = @p; SET ANSI_NULLS OFF")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname = @p SET ANSI_NULLS OFF")]
    // Inside q, p is a table: a CTE sees only the CTEs before it.
    [InlineData(
        "WITH q AS (SELECT nickname FROM p), p AS (SELECT nickname FROM t WHERE nickname > 'a') "
            + "SELECT nickname FROM q"
    )]
    // A join in a CTE's body, which has no call of "operands" to show that its filter is of a stored column.
    [InlineData(
        "WITH q AS (SELECT id, nickname FROM dbo.users WHERE nickname > 'a'), r AS (SELECT q.nickname "
            + "FROM dbo.orders AS o LEFT JOIN q ON q.id = o.user_id WHERE q.Lat > 0) SELECT nickname FROM r"
    )]
    [InlineData("SELECT a.nickname FROM dbo.users AS a JOIN dbo.users AS b ON b.id = a.id WHERE b.nickname > 'a'")]
    [InlineData("WITH q AS (SELECT nickname FROM dbo.users) SELECT nickname FROM q")]
    [InlineData("WITH q AS (SELECT nickname FROM dbo.users WHERE nickname > 'a') SELECT nickname FROM Q")]
    [InlineData("WITH q AS (SELECT nickname FROM dbo.users WHERE id > 0 OR nickname > 'a') SELECT nickname FROM q")]
    [InlineData(
        "WITH q AS (SELECT nickname FROM dbo.users WHERE nickname > 'a' GROUP BY ROLLUP (nickname)) "
            + "SELECT nickname FROM q"
    )]
    [InlineData(
        "WITH q AS (SELECT nickname FROM dbo.users WHERE nickname > 'a' GROUP BY ALL nickname) SELECT nickname FROM q"
    )]
    [InlineData(
        "WITH q AS (SELECT nickname FROM dbo.users WHERE nickname > 'a' UNION ALL SELECT NULL) SELECT nickname FROM q"
    )]
    [InlineData("WITH q AS (SELECT * FROM dbo.users WHERE nickname > 'a') SELECT nickname FROM q")]
    [InlineData("WITH q AS (SELECT name AS nickname FROM dbo.users WHERE nickname > 'a') SELECT nickname FROM q")]
    [InlineData(
        "WITH q AS (SELECT id, nickname FROM dbo.users WHERE nickname > 'a') "
            + "SELECT q.nickname FROM dbo.orders AS o LEFT JOIN q ON q.id = o.user_id"
    )]
    [InlineData(
        "WITH q AS (SELECT id, nickname FROM dbo.users WHERE nickname > 'a') "
            + "SELECT q.nickname FROM q RIGHT JOIN dbo.orders AS o ON q.id = o.user_id"
    )]
    [InlineData(
        "WITH q AS (SELECT id, nickname FROM dbo.users WHERE nickname > 'a') "
            + "SELECT nickname FROM q CROSS APPLY (SELECT 1 AS one) AS o"
    )]
    public void Apply_ColumnNoFilterKeeps_StaysNullable(string sql)
    {
        var proofs = Read(sql, ExpressionRow(1, "nickname"));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // Under a grouping set a total's row holds NULL, and GROUP BY ALL a group no row of the WHERE holds, so a filter
    // proves nothing of a grouping column.
    [Theory]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname > 'a' GROUP BY ROLLUP (nickname)")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname > 'a' GROUP BY CUBE (nickname)")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname > 'a' GROUP BY GROUPING SETS ((nickname), ())")]
    [InlineData("SELECT nickname FROM dbo.users WHERE nickname > 'a' GROUP BY ALL nickname")]
    public void Apply_FilteredGroupingColumnOfATotal_StaysNullable(string sql)
    {
        var proofs = Read(sql, ExpressionRow(1, "nickname"));

        Nullability(proofs.Apply([new OperandFacts(true, true, Stored: true)]))
            .ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // A conjunct over the optional side of a LEFT JOIN makes it inner, so "operands" reads the join without its LEFT
    // and asks a lone column there, whose answer over those rows is what the server gives an inner join.
    [Theory]
    [InlineData("LEFT JOIN")]
    [InlineData("LEFT OUTER JOIN")]
    [InlineData("LEFT /* c */ OUTER -- d\n JOIN")]
    public void Read_LeftJoinTheWhereMakesInner_AsksItWithoutItsLeft(string join)
    {
        var proofs = Read(
            $"SELECT o.total FROM dbo.users AS u {join} dbo.orders AS o ON o.user_id = u.id WHERE o.status = 1",
            ExpressionRow(1, "total")
        );

        proofs
            .OperandQueries.ShouldHaveSingleItem()
            .Sql.ShouldBe(
                "SELECT o.total\nAS [1],\no.status\nAS [2]\nFROM dbo.users AS u\nJOIN dbo.orders AS o "
                    + "ON o.user_id = u.id WHERE o.status = 1"
            );
        Nullability(proofs.Apply([new OperandFacts(true, true), new OperandFacts(false, true, Stored: true)]))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply([new OperandFacts(false, true), new OperandFacts(false, true, Stored: true)]))
            .ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // A name under a join's alias can be a property of a CLR column of another source, which the server names no
    // source for: the filter then holds of a row with no match, and what the text without the LEFT answers proves
    // nothing.
    [Fact]
    public void Apply_FilterOfTheJoinIsNoStoredColumn_StaysNullable()
    {
        var proofs = Read(
            "SELECT loc.total FROM dbo.users AS u LEFT JOIN dbo.orders AS loc ON loc.user_id = u.id WHERE loc.Lat > 0",
            ExpressionRow(1, "total")
        );

        Nullability(proofs.Apply([new OperandFacts(true, true), new OperandFacts(false, true, Stored: false)]))
            .ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // Each LEFT JOIN made inner needs its own stored column, and one of a join's columns is enough.
    [Theory]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, false, true, true)]
    public void Apply_TwoJoinsMadeInner_EachNeedsAStoredColumn(bool first, bool alsoFirst, bool second, bool nullable)
    {
        var proofs = Read(
            "SELECT b.total FROM dbo.users AS u LEFT JOIN dbo.orders AS a ON a.user_id = u.id "
                + "LEFT JOIN dbo.orders AS b ON b.user_id = u.id WHERE a.x > 0 AND a.y > 0 AND b.z > 0",
            ExpressionRow(1, "total")
        );

        Nullability(
                proofs.Apply([
                    new OperandFacts(true, true),
                    new OperandFacts(false, true, Stored: first),
                    new OperandFacts(false, true, Stored: alsoFirst),
                    new OperandFacts(false, true, Stored: second),
                ])
            )
            .ShouldBe([
                (nullable, nullable ? SidecarValues.NullableSource.Server : SidecarValues.NullableSource.Expression),
            ]);
    }

    // A filter of another source, or a chain the reader cannot follow, leaves every LEFT in place and asks no lone
    // column.
    [Theory]
    [InlineData("SELECT o.total FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id WHERE u.id = 1")]
    [InlineData("SELECT o.total FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id WHERE status = 1")]
    [InlineData("SELECT o.total FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id WHERE O.status = 1")]
    [InlineData(
        "SELECT o.total FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id "
            + "WHERE o.status = 1 OR u.id = 1"
    )]
    // A name of three parts names a table's column, here of the preserved side, whatever the alias of the join.
    [InlineData(
        "SELECT dbo.total FROM dbo.users LEFT JOIN dbo.orders AS dbo ON dbo.user_id = users.id WHERE dbo.users.id > 0"
    )]
    [InlineData(
        "SELECT o.total FROM dbo.users AS u LEFT HASH JOIN dbo.orders AS o ON o.user_id = u.id WHERE o.status = 1"
    )]
    [InlineData(
        "SELECT o.total FROM dbo.users AS u LEFT JOIN dbo.orders AS o JOIN dbo.items AS i ON i.order_id = o.id "
            + "ON o.user_id = u.id WHERE o.status = 1"
    )]
    [InlineData(
        "SELECT o.total FROM dbo.users AS u OUTER APPLY (SELECT total, status FROM dbo.orders) AS o WHERE o.status = 1"
    )]
    public void Read_NoJoinMadeInner_AsksNothing(string sql) =>
        Read(sql, ExpressionRow(1, "total")).OperandQueries.ShouldBeEmpty();

    [Fact]
    public void Read_RefusedText_ProvesNothing()
    {
        var proofs = Read("SELECT COUNT(*) AS n FROM dbo.users FOR JSON PATH", ExpressionRow(1, "n"));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    [Fact]
    public void Read_StarAmongTheItems_ProvesNothing()
    {
        var proofs = Read("SELECT u.*, COUNT(*) OVER () AS n FROM dbo.users AS u", IdColumn, ExpressionRow(2, "n"));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(null))
            .ShouldBe([(false, SidecarValues.NullableSource.Server), (true, SidecarValues.NullableSource.Server)]);
    }

    // "window" is no reserved word: an alias or a column so named is no WINDOW clause, and the query is not read,
    // since a clause read from it would cut the FROM or the GROUP BY short and ask its operands over other rows.
    [Theory]
    [InlineData("SELECT x + 1 AS y FROM dbo.users window RIGHT JOIN dbo.orders AS o ON o.user_id = id")]
    [InlineData("SELECT CAST(a AS bigint) AS y FROM dbo.w GROUP BY b, window, ROLLUP (a)")]
    public void Read_WindowAsAName_ProvesNothing(string sql)
    {
        var proofs = Read(sql, ExpressionRow(1, "y"));

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(Answers(true, true))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // "offset" is no reserved word either, and an alias so named stays in the FROM, whose rows the operand is asked
    // over: the server answers it over the outer join.
    [Fact]
    public void Read_OffsetAsAnAlias_AsksOverTheWholeFrom()
    {
        var proofs = Read(
            "SELECT x + 1 AS y FROM dbo.users offset RIGHT JOIN dbo.orders AS o ON o.user_id = id",
            ExpressionRow(1, "y")
        );

        proofs
            .OperandQueries.ShouldHaveSingleItem()
            .Sql.ShouldBe("SELECT x\nAS [1]\nFROM dbo.users offset RIGHT JOIN dbo.orders AS o ON o.user_id = id");
    }

    // A column whose own row is of a CLR type gets no proof of its expression: a string converted to the type may be
    // its Null, as N'NULL' to geometry is.
    [Theory]
    [InlineData("SELECT COALESCE(shape, N'NULL') AS g FROM dbo.shapes")]
    [InlineData("SELECT COALESCE(shape, (SELECT COUNT(*) FROM dbo.shapes)) AS g FROM dbo.shapes")]
    public void Read_ColumnOfAClrType_ProvesNothing(string sql)
    {
        var proofs = Read(
            sql,
            ExpressionRow(1, "g") with
            {
                SystemTypeName = "geometry",
                MaxLength = -1,
                Precision = 0,
                UserTypeSchema = "sys",
                UserTypeName = "geometry",
                AssemblyQualifiedTypeName = "Microsoft.SqlServer.Types.SqlGeometry, Microsoft.SqlServer.Types",
            }
        );

        proofs.OperandQueries.ShouldBeEmpty();
        Nullability(proofs.Apply(Answers(true))).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
    }

    // COALESCE converts each argument to the type of highest precedence, which a CLR type has: an operand answered of
    // a CLR type proves nothing, however the column's row is typed, and one of no CLR type proves it with the literal.
    [Fact]
    public void Apply_OperandOfAClrType_StaysNullable()
    {
        var proofs = Read("SELECT COALESCE(nickname, N'NULL') AS shown FROM dbo.users", ExpressionRow(1, "shown"));

        proofs.OperandQueries.ShouldHaveSingleItem().Sql.ShouldBe("SELECT nickname\nAS [1]\nFROM dbo.users");
        Nullability(proofs.Apply([new OperandFacts(NotNull: true, NotClr: false, Stored: true)]))
            .ShouldBe([(true, SidecarValues.NullableSource.Server)]);
        Nullability(proofs.Apply([new OperandFacts(false, NotClr: true, Stored: true)]))
            .ShouldBe([(false, SidecarValues.NullableSource.Expression)]);
        Nullability(proofs.Apply(null)).ShouldBe([(true, SidecarValues.NullableSource.Server)]);
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

        proofs.OperandQueries.ShouldBeEmpty();
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

        proofs.OperandQueries.ShouldBeEmpty();
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

        proofs.OperandQueries.ShouldBeEmpty();
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
    // An alias named window or offset, neither a reserved word, before an outer join.
    [InlineData("SELECT id, name_len_p FROM dbo.users window RIGHT JOIN dbo.orders AS o ON o.user_id = window.id")]
    [InlineData("SELECT id, name_len_p FROM dbo.users offset RIGHT JOIN dbo.orders AS o ON o.user_id = offset.id")]
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
