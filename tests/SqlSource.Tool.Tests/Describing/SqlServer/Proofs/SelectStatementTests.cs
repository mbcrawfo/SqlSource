using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The reader of one SELECT: its items and their names, and the clauses the proofs need, or null for any text that is
// not one SELECT it can follow.
public sealed class SelectStatementTests
{
    [Fact]
    public void Read_PlainSelect_ItemsAndNames()
    {
        var (tokens, statement) = Read(
            "SELECT c.id, c.name AS display, n = COUNT(*), 'k' = 1, x y, [a b] "
                + "FROM dbo.c AS c WHERE c.id > 0 ORDER BY 1"
        );

        var body = statement.ShouldNotBeNull().Body;
        body.Items.Select(static item => item.Name).ShouldBe(["id", "display", "n", "k", "y", "a b"]);
        body.Grouping.ShouldBe(SelectGrouping.None);
        body.HasFrom.ShouldBeTrue();
        var from = body.FromThroughWhere.ShouldNotBeNull();
        tokens[from.Start].Text.ShouldBe("FROM");
        tokens[from.End - 1].Text.ShouldBe("0");
        tokens[from.End].Text.ShouldBe("ORDER");
        statement.With.IsEmpty.ShouldBeTrue();
        statement.CteNames.ShouldBeEmpty();
    }

    // An item's expression leaves its alias out.
    [Fact]
    public void Read_PlainSelect_ExpressionsWithoutTheirAliases()
    {
        var (tokens, statement) = Read("SELECT c.id, c.name AS display, n = COUNT(*), x y FROM dbo.c AS c");

        statement
            .ShouldNotBeNull()
            .Body.Items.Select(item => Text(tokens, item.Expression))
            .ShouldBe(["c . id", "c . name", "COUNT ( * )", "x"]);
    }

    [Fact]
    public void Read_WithClause_KeepsItAndItsNames()
    {
        var (tokens, statement) = Read(";WITH a AS (SELECT 1 AS x), b (y) AS (SELECT 2) SELECT a.x FROM a");

        statement.ShouldNotBeNull().CteNames.ShouldBe(["a", "b"]);
        tokens[statement.With.Start].Text.ShouldBe("WITH");
        tokens[statement.With.End - 1].Text.ShouldBe(")");
        tokens[statement.With.End].Text.ShouldBe("SELECT");
        statement.Body.Items.ShouldHaveSingleItem().Name.ShouldBe("x");
    }

    [Fact]
    public void Read_WithClause_QuotedNamesAreUnquoted() =>
        Read("WITH [a b] AS (SELECT 1 AS x) SELECT x FROM [a b]")
            .Statement.ShouldNotBeNull()
            .CteNames.ShouldBe(["a b"]);

    [Theory]
    [InlineData("SELECT DISTINCT TOP (@n) PERCENT WITH TIES a FROM t")]
    [InlineData("SELECT TOP 10 a FROM t")]
    [InlineData("SELECT ALL TOP (1) a FROM t")]
    public void Read_TopDistinct_SkipsThem(string sql)
    {
        var body = Read(sql).Statement.ShouldNotBeNull().Body;

        body.Items.ShouldHaveSingleItem().Name.ShouldBe("a");
        body.HasTop.ShouldBeTrue();
    }

    [Fact]
    public void Read_NoTop_HasNoTop() =>
        Read("SELECT a FROM t").Statement.ShouldNotBeNull().Body.HasTop.ShouldBeFalse();

    [Fact]
    public void Read_CaseAndWithinGroup_OneItemEach()
    {
        var body = Read(
            "SELECT CASE WHEN a > 1 THEN 'x' ELSE 'y' END AS k, STRING_AGG(b, ',') WITHIN GROUP (ORDER BY b) AS s "
                + "FROM t GROUP BY a"
        )
            .Statement.ShouldNotBeNull()
            .Body;

        body.Items.Select(static item => item.Name).ShouldBe(["k", "s"]);
        body.Grouping.ShouldBe(SelectGrouping.Plain);
    }

    [Fact]
    public void Read_NextValueFor_IsAnItem()
    {
        var body = Read("SELECT NEXT VALUE FOR dbo.s AS n").Statement.ShouldNotBeNull().Body;

        body.Items.ShouldHaveSingleItem().Name.ShouldBe("n");
        body.HasFrom.ShouldBeFalse();
        body.FromThroughWhere.ShouldBeNull();
        body.IsOneTable.ShouldBeFalse();
    }

    [Theory]
    [InlineData("GROUP BY a", nameof(SelectGrouping.Plain))]
    [InlineData("GROUP BY a, b HAVING COUNT(*) > 1", nameof(SelectGrouping.Plain))]
    [InlineData("GROUP BY ALL a", nameof(SelectGrouping.All))]
    [InlineData("GROUP BY ROLLUP (a)", nameof(SelectGrouping.Sets))]
    [InlineData("GROUP BY CUBE (a)", nameof(SelectGrouping.Sets))]
    [InlineData("GROUP BY GROUPING SETS ((a), ())", nameof(SelectGrouping.Sets))]
    [InlineData("GROUP BY a WITH ROLLUP", nameof(SelectGrouping.Sets))]
    [InlineData("GROUP BY a WITH CUBE", nameof(SelectGrouping.Sets))]
    [InlineData("GROUP BY ()", nameof(SelectGrouping.Sets))]
    [InlineData("GROUP BY a, ()", nameof(SelectGrouping.Sets))]
    [InlineData("ORDER BY a", nameof(SelectGrouping.None))]
    // The grouping is named, since a theory's parameter is public and the enum is not.
    public void Read_Groupings_AreRead(string clauses, string grouping) =>
        Read($"SELECT a FROM t {clauses}").Statement.ShouldNotBeNull().Body.Grouping.ToString().ShouldBe(grouping);

    [Fact]
    public void Read_Having_HasHaving() =>
        Read("SELECT a FROM t GROUP BY a HAVING COUNT(*) > 1")
            .Statement.ShouldNotBeNull()
            .Body.HasHaving.ShouldBeTrue();

    [Fact]
    public void Read_OffsetFetch_HasOffset() =>
        Read("SELECT a FROM t ORDER BY a OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY")
            .Statement.ShouldNotBeNull()
            .Body.HasOffset.ShouldBeTrue();

    [Fact]
    public void Read_EveryClause_FromThroughWhereEndsBeforeTheGroup()
    {
        var (tokens, statement) = Read(
            "SELECT a, SUM(b) OVER w AS s FROM t WHERE a > 0 GROUP BY a, b HAVING COUNT(*) > 1 "
                + "WINDOW w AS (ORDER BY a) ORDER BY a OPTION (RECOMPILE);"
        );

        var body = statement.ShouldNotBeNull().Body;
        var from = body.FromThroughWhere.ShouldNotBeNull();
        Text(tokens, from).ShouldBe("FROM t WHERE a > 0");
        body.HasHaving.ShouldBeTrue();
        body.Items.Select(static item => item.Name).ShouldBe(["a", "s"]);
    }

    [Fact]
    public void Read_ForSystemTime_IsPartOfTheFrom()
    {
        var (tokens, statement) = Read("SELECT a FROM t FOR SYSTEM_TIME ALL WHERE a = 1");

        Text(tokens, statement.ShouldNotBeNull().Body.FromThroughWhere.ShouldNotBeNull())
            .ShouldBe("FROM t FOR SYSTEM_TIME ALL WHERE a = 1");
    }

    [Fact]
    public void Read_ForSystemTimeAsOf_IsPartOfTheFrom()
    {
        var (tokens, statement) = Read("SELECT a FROM t FOR SYSTEM_TIME AS OF @t ORDER BY a");

        Text(tokens, statement.ShouldNotBeNull().Body.FromThroughWhere.ShouldNotBeNull())
            .ShouldBe("FROM t FOR SYSTEM_TIME AS OF @t");
    }

    // A WHERE without a FROM is left out of the rows the operands are asked over, as OperandsQuery builds its
    // text (docs/architecture/sqlserver-describer.md, "The text of operands").
    [Fact]
    public void Read_WhereWithoutFrom_HasNoFrom()
    {
        var body = Read("SELECT 1 AS a WHERE @x = 1").Statement.ShouldNotBeNull().Body;

        body.HasFrom.ShouldBeFalse();
        body.FromThroughWhere.ShouldBeNull();
    }

    [Theory]
    [InlineData("SELECT * FROM t")]
    [InlineData("SELECT t.* FROM t")]
    [InlineData("SELECT [s].[t].* FROM s.t")]
    public void Read_Star_IsAStarItem(string sql)
    {
        var item = Read(sql).Statement.ShouldNotBeNull().Body.Items.ShouldHaveSingleItem();

        item.IsStar.ShouldBeTrue();
        item.Name.ShouldBeNull();
    }

    [Theory]
    [InlineData("SELECT COUNT(*) FROM t")]
    [InlineData("SELECT a * b FROM t")]
    public void Read_StarInsideAnExpression_IsNoStarItem(string sql) =>
        Read(sql).Statement.ShouldNotBeNull().Body.Items.ShouldHaveSingleItem().IsStar.ShouldBeFalse();

    [Theory]
    [InlineData("FROM dbo.t")]
    [InlineData("FROM dbo.t AS x WITH (NOLOCK) WHERE x.a = 1")]
    [InlineData("FROM [s].[t] x")]
    [InlineData("FROM t WITH (NOLOCK)")]
    [InlineData("FROM t ORDER BY a")]
    [InlineData("FROM b")]
    public void Read_OneTable_IsOneTable(string from) =>
        Read($"WITH a AS (SELECT 1 AS x) SELECT x {from}").Statement.ShouldNotBeNull().Body.IsOneTable.ShouldBeTrue();

    [Theory]
    [InlineData("FROM t JOIN u ON u.id = t.id")]
    [InlineData("FROM t INNER JOIN u ON u.id = t.id")]
    [InlineData("FROM t x LEFT JOIN u ON u.id = x.id")]
    [InlineData("FROM t, u")]
    [InlineData("FROM t CROSS APPLY dbo.f(t.id) f")]
    [InlineData("FROM (SELECT 1 AS x) d")]
    [InlineData("FROM dbo.f(1) f")]
    [InlineData("FROM t FOR SYSTEM_TIME ALL")]
    [InlineData("FROM t PIVOT (MAX(v) FOR k IN ([p])) p")]
    [InlineData("FROM t TABLESAMPLE (10 PERCENT)")]
    [InlineData("FROM a")]
    [InlineData("FROM A x")]
    [InlineData("FROM [a]")]
    [InlineData("FROM @t")]
    [InlineData("FROM t (NOLOCK)")]
    [InlineData("FROM db..t")]
    [InlineData("FROM t FOR SYSTEM_TIME AS OF @t")]
    public void Read_NotOneTable_IsNotOneTable(string from) =>
        Read($"WITH a AS (SELECT 1 AS x) SELECT x {from}").Statement.ShouldNotBeNull().Body.IsOneTable.ShouldBeFalse();

    [Theory]
    [InlineData("SELECT a INTO #x FROM t")]
    [InlineData("SELECT a FROM t FOR JSON PATH")]
    [InlineData("SELECT a FROM t FOR XML AUTO")]
    [InlineData("SELECT a FROM t FOR BROWSE")]
    [InlineData("SELECT a FROM t UNION SELECT b FROM u")]
    [InlineData("SELECT a FROM t UNION ALL SELECT b FROM u")]
    [InlineData("SELECT a FROM t EXCEPT SELECT b FROM u")]
    [InlineData("SELECT a FROM t INTERSECT SELECT b FROM u")]
    [InlineData("SELECT a FROM t; SELECT b FROM u")]
    [InlineData("SELECT a FROM t SELECT b FROM u")]
    [InlineData("SELECT a FROM t UPDATE t SET a = 1")]
    [InlineData("UPDATE t SET a = 1 OUTPUT inserted.a")]
    [InlineData("DECLARE @x int; SELECT 1 AS a")]
    [InlineData("(SELECT a FROM t)")]
    [InlineData("WITH c AS (SELECT 1 AS a) INSERT INTO t SELECT a FROM c")]
    [InlineData("WITH XMLNAMESPACES ('urn:x' AS x) SELECT 1 AS a")]
    [InlineData("WITH c AS SELECT 1 AS a")]
    [InlineData("")]
    [InlineData("SELECT FROM t")]
    [InlineData("SELECT a, FROM t")]
    [InlineData("SELECT a FROM t WHERE (a = 1")]
    [InlineData("SELECT CASE WHEN a = 1 THEN 1 FROM t")]
    [InlineData("SELECT a FROM t WHERE a = 1 FROM u")]
    [InlineData("SELECT a FROM t ORDER BY a WHERE a = 1")]
    [InlineData("SELECT TOP a FROM t")]
    public void Read_Refused_IsNull(string sql) => Read(sql).Statement.ShouldBeNull();

    // WINDOW is no reserved word, so "window" may be an alias or a column.  A WINDOW clause is only
    // "WINDOW name AS ( ... ) [, name AS ( ... )]..."; any other "window" of the select's level gives null, since a
    // clause read from it would cut the FROM or the GROUP BY short.
    [Theory]
    [InlineData("SELECT c2 FROM dbo.t window RIGHT JOIN dbo.u AS o ON o.uid = id")]
    [InlineData("SELECT x + 1 AS y FROM dbo.t window RIGHT JOIN dbo.u AS o ON o.uid = id")]
    [InlineData("SELECT CAST(a AS bigint) AS c FROM dbo.w GROUP BY b, window, ROLLUP (a)")]
    [InlineData("SELECT a FROM t window")]
    [InlineData("SELECT a FROM t WHERE a = window")]
    [InlineData("SELECT a FROM t GROUP BY window HAVING COUNT(*) > 1")]
    [InlineData("SELECT a FROM t ORDER BY window")]
    [InlineData("SELECT window FROM t")]
    [InlineData("SELECT a FROM t WINDOW w (ORDER BY a)")]
    [InlineData("SELECT a FROM t WINDOW w AS ORDER BY a")]
    [InlineData("SELECT a FROM t WINDOW w AS (ORDER BY a) x")]
    [InlineData("SELECT a FROM t WINDOW w AS (ORDER BY a),")]
    [InlineData("SELECT a FROM t WINDOW w AS (ORDER BY a) v AS (w)")]
    [InlineData("SELECT a FROM t WINDOW @w AS (ORDER BY a)")]
    [InlineData("SELECT a FROM t WINDOW")]
    public void Read_WindowWordNotAWindowClause_IsNull(string sql) => Read(sql).Statement.ShouldBeNull();

    [Theory]
    [InlineData("SELECT a FROM t WINDOW w AS (ORDER BY a)")]
    [InlineData("SELECT a FROM t window w AS (PARTITION BY a), [v] AS (w ORDER BY a) ORDER BY a")]
    [InlineData("SELECT a FROM t WINDOW w AS ()")]
    public void Read_WindowClause_IsRead(string sql) =>
        Text(Read(sql).Tokens, Read(sql).Statement.ShouldNotBeNull().Body.FromThroughWhere.ShouldNotBeNull())
            .ShouldBe("FROM t");

    // A quoted [window] is a name, never the clause.
    [Fact]
    public void Read_QuotedWindowAlias_IsPartOfTheFrom()
    {
        var (tokens, statement) = Read("SELECT c2 FROM dbo.t [window] RIGHT JOIN dbo.u AS o ON o.uid = id");

        var body = statement.ShouldNotBeNull().Body;
        Text(tokens, body.FromThroughWhere.ShouldNotBeNull())
            .ShouldBe("FROM dbo . t [window] RIGHT JOIN dbo . u AS o ON o . uid = id");
        body.IsOneTable.ShouldBeFalse();
    }

    // OFFSET is no reserved word either, but it never starts a clause here: it is looked for only inside the ORDER BY,
    // where a column named so makes HasOffset true, which proves less.  An "offset" anywhere else is a name.
    [Fact]
    public void Read_OffsetAlias_IsPartOfTheFrom()
    {
        var (tokens, statement) = Read("SELECT c2 FROM dbo.t offset RIGHT JOIN dbo.u AS o ON o.uid = id");

        var body = statement.ShouldNotBeNull().Body;
        Text(tokens, body.FromThroughWhere.ShouldNotBeNull())
            .ShouldBe("FROM dbo . t offset RIGHT JOIN dbo . u AS o ON o . uid = id");
        body.IsOneTable.ShouldBeFalse();
        body.HasOffset.ShouldBeFalse();
    }

    [Fact]
    public void Read_GroupByOffsetColumn_RollupSeen() =>
        Read("SELECT a FROM t GROUP BY b, offset, ROLLUP (a)")
            .Statement.ShouldNotBeNull()
            .Body.Grouping.ShouldBe(SelectGrouping.Sets);

    [Fact]
    public void Read_OrderByOffsetColumn_HasOffset() =>
        Read("SELECT a FROM t ORDER BY offset").Statement.ShouldNotBeNull().Body.HasOffset.ShouldBeTrue();

    [Fact]
    public void Read_TrailingSemicolon_IsOneStatement() =>
        Read("SELECT a FROM t;").Statement.ShouldNotBeNull().Body.Items.ShouldHaveSingleItem().Name.ShouldBe("a");

    [Theory]
    [InlineData("SELECT a + 1 FROM t")]
    [InlineData("SELECT CASE WHEN a = 1 THEN 1 END FROM t")]
    [InlineData("SELECT name COLLATE Latin1_General_BIN FROM t")]
    [InlineData("SELECT a AT TIME ZONE 'UTC' FROM t")]
    [InlineData("SELECT COUNT(*) OVER w FROM t WINDOW w AS (ORDER BY a)")]
    [InlineData("SELECT NEXT VALUE FOR s")]
    [InlineData("SELECT - a FROM t")]
    [InlineData("SELECT @@ROWCOUNT")]
    [InlineData("SELECT NULL")]
    [InlineData("SELECT N'x'")]
    [InlineData("SELECT @p = a FROM t")]
    [InlineData("SELECT @a#b")]
    [InlineData("SELECT @a#@c")]
    [InlineData("SELECT $IDENTITY FROM t")]
    public void Read_ItemNameWhereItCannotBeRead_IsNull(string sql) =>
        Read(sql).Statement.ShouldNotBeNull().Body.Items.ShouldHaveSingleItem().Name.ShouldBeNull();

    [Theory]
    [InlineData("SELECT a.b FROM t", "b")]
    [InlineData("SELECT [s].[t].[c]]d] FROM t", "c]d")]
    [InlineData("SELECT \"a\"\"b\" FROM t", "a\"b")]
    [InlineData("SELECT 1 AS 'a''b'", "a'b")]
    [InlineData("SELECT N'a' = 1", "a")]
    [InlineData("SELECT CAST(a AS int) b FROM t", "b")]
    [InlineData("SELECT CASE WHEN a = 1 THEN 1 END k FROM t", "k")]
    [InlineData("SELECT name COLLATE Latin1_General_BIN x FROM t", "x")]
    [InlineData("SELECT 1x", "x")]
    [InlineData("SELECT a [END] FROM t", "END")]
    [InlineData("SELECT 1 AS [FROM]", "FROM")]
    [InlineData("SELECT SUM(a) OVER (PARTITION BY b) s FROM t", "s")]
    public void Read_ItemName_IsReadWithoutQuotes(string sql, string name) =>
        Read(sql).Statement.ShouldNotBeNull().Body.Items.ShouldHaveSingleItem().Name.ShouldBe(name);

    [Fact]
    public void Read_Subquery_BodyOfAParenthesizedSelect()
    {
        var tokens = TsqlTokens.Read("SELECT (SELECT COUNT(*) FROM o WHERE o.c = c.id) AS n FROM c");
        var open = tokens.ToList().FindIndex(static token => token.IsSymbol("("));
        var close = tokens.ToList().FindIndex(static token => token.Is("AS")) - 1;

        var body = SelectBody.Read(tokens, new TokenRange(open + 1, close), []).ShouldNotBeNull();

        body.Items.ShouldHaveSingleItem().Name.ShouldBeNull();
        body.HasHaving.ShouldBeFalse();
        body.HasTop.ShouldBeFalse();
        body.HasOffset.ShouldBeFalse();
        body.Grouping.ShouldBe(SelectGrouping.None);
        body.IsOneTable.ShouldBeTrue();
    }

    [Fact]
    public void Read_SubqueryRangeNotStartingWithSelect_IsNull()
    {
        var tokens = TsqlTokens.Read("(1)");

        SelectBody.Read(tokens, new TokenRange(1, 2), []).ShouldBeNull();
    }

    [Fact]
    public void Count_OfARange_IsItsLength() => new TokenRange(2, 5).Count.ShouldBe(3);

    [Fact]
    public void IsEmpty_OfARangeOfTokens_IsFalse() => new TokenRange(2, 5).IsEmpty.ShouldBeFalse();

    [Fact]
    public void IsEmpty_OfARangeOfNoToken_IsTrue() => new TokenRange(4, 4).IsEmpty.ShouldBeTrue();

    private static (IReadOnlyList<TsqlToken> Tokens, SelectStatement? Statement) Read(string sql)
    {
        var tokens = TsqlTokens.Read(sql);
        return (tokens, SelectStatement.Read(tokens));
    }

    private static string Text(IReadOnlyList<TsqlToken> tokens, TokenRange range) =>
        string.Join(' ', tokens.Skip(range.Start).Take(range.Count).Select(static token => token.Text));
}
