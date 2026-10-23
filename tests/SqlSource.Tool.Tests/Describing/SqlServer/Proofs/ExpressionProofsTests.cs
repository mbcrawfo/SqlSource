using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The forms that prove a T-SQL column's expression not null, and the operands they leave to ask the server.  Each
// case reads "SELECT <item> AS c FROM t AS t <tail>" and evaluates the item's proof with every operand answered not
// null, then with every operand answered nullable.  An operand whose answer cannot change the proof is not asked.
public sealed class ExpressionProofsTests
{
    [Theory]
    // COUNT, alone, over a group, as a window function and as a scalar subquery of one row.
    [InlineData("COUNT(*)", "", true, true, "")]
    [InlineData("count(*)", "", true, true, "")]
    [InlineData("COUNT(DISTINCT t.a)", "GROUP BY t.b", true, true, "")]
    [InlineData("COUNT(*)", "GROUP BY ROLLUP (t.b)", true, true, "")]
    [InlineData("COUNT_BIG(t.a) OVER (PARTITION BY t.b)", "", true, true, "")]
    [InlineData("-COUNT(*)", "", true, true, "")]
    [InlineData("COUNT(*) OVER w", "WINDOW w AS (ORDER BY t.id)", false, false, "")]
    [InlineData("(SELECT COUNT(*) FROM o WHERE o.c = t.id)", "", true, true, "")]
    [InlineData("((SELECT COUNT_BIG(*) AS n FROM o))", "", true, true, "")]
    [InlineData("(SELECT COUNT(*) FROM o WHERE o.c = t.a)", "GROUP BY ROLLUP (t.a)", true, true, "")]
    [InlineData("(SELECT COUNT(*) FROM o GROUP BY o.c)", "", false, false, "")]
    [InlineData("(SELECT COUNT(*) FROM o GROUP BY ())", "", false, false, "")]
    [InlineData("(SELECT COUNT(*) FROM o HAVING COUNT(*) > 1)", "", false, false, "")]
    [InlineData("(SELECT TOP (1) COUNT(*) FROM o)", "", false, false, "")]
    [InlineData("(SELECT COUNT(*) FROM o ORDER BY 1 OFFSET 1 ROWS)", "", false, false, "")]
    [InlineData("(SELECT COUNT(*) OVER () FROM o)", "", false, false, "")]
    [InlineData("(SELECT COUNT(*) FROM o UNION ALL SELECT COUNT(*) FROM p)", "", false, false, "")]
    // A subquery of another form is a leaf, asked whole unless it holds an aggregate, OVER or a user's function.
    [InlineData("(SELECT TOP (1) o.x FROM o)", "", true, false, "(SELECT TOP (1) o.x FROM o)")]
    [InlineData("(SELECT MAX(o.x) FROM o)", "", false, false, "")]
    [InlineData(
        "CASE WHEN EXISTS (SELECT 1 FROM o WHERE o.c = t.id) THEN 1 ELSE 0 END",
        "",
        true,
        false,
        "CASE WHEN EXISTS (SELECT 1 FROM o WHERE o.c = t.id) THEN 1 ELSE 0 END"
    )]
    [InlineData("CASE WHEN EXISTS (SELECT 1 FROM o HAVING MAX(t.a) > 0) THEN 1 ELSE 0 END", "", false, false, "")]
    [InlineData("(SELECT dbo.f(o.x) FROM o)", "", false, false, "")]
    // A COUNT under GROUP BY ALL, which proves nothing.
    [InlineData("COUNT(*)", "GROUP BY ALL t.b", false, false, "")]
    // The ranking functions.
    [InlineData("ROW_NUMBER() OVER (ORDER BY t.a)", "", true, true, "")]
    [InlineData("ROW_NUMBER() OVER (ORDER BY (SELECT NULL))", "", true, true, "")]
    [InlineData("RANK() OVER (ORDER BY t.a)", "", true, true, "")]
    [InlineData("DENSE_RANK() OVER (ORDER BY t.a)", "", true, true, "")]
    [InlineData("NTILE(4) OVER (ORDER BY t.a)", "", true, true, "")]
    [InlineData("NTILE(@n) OVER (ORDER BY t.a)", "", false, false, "")]
    [InlineData("NTILE(4.5) OVER (ORDER BY t.a)", "", false, false, "")]
    [InlineData("ROW_NUMBER()", "", false, false, "")]
    [InlineData("ROW_NUMBER() OVER w", "WINDOW w AS (ORDER BY t.a)", false, false, "")]
    // SUM, MAX and MIN over a group: the operand is asked over the rows.
    [InlineData("SUM(t.a)", "GROUP BY t.b", true, false, "t.a")]
    [InlineData("SUM(ALL t.a)", "GROUP BY t.b", true, false, "t.a")]
    [InlineData("MAX(DISTINCT t.a)", "GROUP BY ROLLUP (t.b)", true, false, "t.a")]
    [InlineData("MIN(t.a)", "GROUP BY t.b WITH CUBE", true, false, "t.a")]
    [InlineData("SUM(t.a)", "GROUP BY ()", true, false, "t.a")]
    [InlineData("MAX(t.a)", "GROUP BY ROLLUP (t.a)", true, false, "t.a")]
    [InlineData("MIN(t.a)", "", false, false, "")]
    [InlineData("MIN(t.a)", "HAVING COUNT(*) > 0", false, false, "")]
    [InlineData("MAX(t.a)", "GROUP BY ALL t.b", false, false, "")]
    [InlineData("SUM(@p)", "GROUP BY t.b", false, false, "")]
    // SUM, MAX and MIN as window aggregates: the frame must hold the current row.
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS UNBOUNDED PRECEDING)", "", true, false, "t.a")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS 2 PRECEDING)", "", true, false, "t.a")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS CURRENT ROW)", "", true, false, "t.a")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN 2 PRECEDING AND 1 FOLLOWING)", "", true, false, "t.a")]
    [InlineData(
        "MAX(t.a) OVER (ORDER BY t.id RANGE BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING)",
        "",
        true,
        false,
        "t.a"
    )]
    [InlineData("MIN(t.a) OVER (PARTITION BY t.b)", "", true, false, "t.a")]
    [InlineData("MAX(t.a) OVER (ORDER BY t.id)", "", true, false, "t.a")]
    [InlineData("SUM(t.a) OVER ()", "", true, false, "t.a")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)", "", false, false, "")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN 2 PRECEDING AND 1 PRECEDING)", "", false, false, "")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN CURRENT ROW AND 1 PRECEDING)", "", false, false, "")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN @n PRECEDING AND CURRENT ROW)", "", false, false, "")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS UNBOUNDED FOLLOWING)", "", false, false, "")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS 1 FOLLOWING)", "", false, false, "")]
    [InlineData("MAX(t.a) OVER w", "WINDOW w AS (ORDER BY t.id)", false, false, "")]
    // A column named like a frame's word is no frame; one that comes before the frame makes it unread.
    [InlineData("MAX(t.a) OVER (ORDER BY [rows])", "", true, false, "t.a")]
    [InlineData("MAX(t.a) OVER (ORDER BY range ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)", "", false, false, "")]
    // A window named inside the parentheses brings its own frame, which is not read.
    [InlineData(
        "SUM(t.a) OVER (w)",
        "WINDOW w AS (ORDER BY t.id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)",
        false,
        false,
        ""
    )]
    [InlineData("SUM(t.a) OVER (w ORDER BY t.id)", "WINDOW w AS (PARTITION BY t.b)", false, false, "")]
    // A window aggregate's operand keeps the select's context, which a ROLLUP forbids asking.
    [InlineData("SUM(t.b) OVER ()", "GROUP BY t.b", true, false, "t.b")]
    [InlineData("SUM(t.b) OVER ()", "GROUP BY ROLLUP (t.b)", false, false, "")]
    [InlineData("SUM(SUM(t.a)) OVER ()", "GROUP BY t.b", true, false, "t.a")]
    [InlineData("SUM(SUM(t.a)) OVER ()", "GROUP BY ROLLUP (t.b)", true, false, "t.a")]
    [InlineData("SUM(COUNT(*)) OVER (PARTITION BY t.b)", "GROUP BY t.b", true, true, "")]
    // COALESCE, GREATEST and LEAST: one argument proved.
    [InlineData("COALESCE(t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("COALESCE(t.a, 0)", "", true, true, "")]
    [InlineData("COALESCE(t.a, NULL, @p)", "", true, false, "t.a")]
    [InlineData("COALESCE(t.a, t.b)", "GROUP BY ROLLUP (t.a, t.b)", false, false, "")]
    [InlineData("COALESCE(t.a, 0)", "GROUP BY ROLLUP (t.a)", true, true, "")]
    [InlineData("COALESCE(MAX(t.a), 0)", "", true, true, "")]
    [InlineData("COALESCE(NULL, @p)", "", false, false, "")]
    [InlineData("GREATEST(t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("LEAST(t.a, 1)", "", true, true, "")]
    [InlineData("GREATEST(@p, NULL)", "", false, false, "")]
    // CAST and CONVERT: the operand proved, and CONVERT's style an integer literal.
    [InlineData(
        "CAST(CASE WHEN t.a > 1 THEN 1 ELSE 0 END AS bit)",
        "",
        true,
        false,
        "CASE WHEN t.a > 1 THEN 1 ELSE 0 END"
    )]
    [InlineData("CAST(CAST(t.a AS int) AS bigint)", "", true, false, "t.a")]
    [InlineData("CAST(t.a AS decimal(10, 2))", "", true, false, "t.a")]
    [InlineData("CAST(t.a AS int)", "GROUP BY ROLLUP (t.a)", false, false, "")]
    [InlineData("CAST(NULL AS int)", "", false, false, "")]
    [InlineData("CAST(@p AS int)", "", false, false, "")]
    [InlineData("CAST(SUM(t.a) AS bigint)", "GROUP BY t.b", true, false, "t.a")]
    [InlineData("CONVERT(varchar(10), t.d, 23)", "", true, false, "t.d")]
    [InlineData("CONVERT(varchar(10), t.d)", "", true, false, "t.d")]
    [InlineData("CONVERT(varchar(10), t.d, @style)", "", false, false, "")]
    [InlineData("CONVERT(varchar(10), t.d, NULL)", "", false, false, "")]
    // TRY_CAST is no form: it is asked whole, and the server answers it.
    [InlineData("TRY_CAST(t.a AS int)", "", true, false, "TRY_CAST(t.a AS int)")]
    // JSON_OBJECT and JSON_ARRAY.
    [InlineData("JSON_OBJECT('a': t.a)", "", true, true, "")]
    [InlineData("JSON_ARRAY(t.a)", "", true, true, "")]
    // Arithmetic: every operand proved.
    [InlineData("t.a * t.b", "", true, false, "t.a|t.b")]
    [InlineData("(t.a + t.b) % 2", "", true, false, "t.a|t.b")]
    [InlineData("COALESCE(p.name + N' > ', N'') + t.name", "", true, false, "t.name")]
    [InlineData("-t.a", "", true, false, "t.a")]
    [InlineData("~t.a - -1", "", true, false, "t.a")]
    [InlineData("t.a + NULL", "", false, false, "")]
    [InlineData("t.a / @p", "", false, false, "")]
    [InlineData("SUM(t.q * t.p)", "GROUP BY t.b", true, false, "t.q|t.p")]
    [InlineData("SUM(t.a) + MAX(t.b)", "GROUP BY ROLLUP (t.c)", true, false, "t.a|t.b")]
    [InlineData("t.b + SUM(t.a)", "GROUP BY ROLLUP (t.b)", false, false, "")]
    [InlineData("(SELECT COUNT(*) FROM o) + t.a", "", true, false, "t.a")]
    // The forms nest, each operand in its own context.
    [InlineData("COALESCE(CAST(SUM(t.a) AS int), t.b)", "GROUP BY ROLLUP (t.b)", true, false, "t.a")]
    [InlineData("SUM(t.a)", "GROUP BY t.b HAVING SUM(t.a) > 0", true, false, "t.a")]
    [InlineData("NTILE(4) OVER (ORDER BY t.b)", "GROUP BY ROLLUP (t.b)", true, true, "")]
    [InlineData("COUNT(*) WITHIN GROUP (ORDER BY t.a)", "", false, false, "")]
    // Anything else at the top is asked whole.
    [InlineData("t.a & t.b", "", true, false, "t.a & t.b")]
    [InlineData("t.a COLLATE Latin1_General_BIN", "", true, false, "t.a COLLATE Latin1_General_BIN")]
    [InlineData("t.a AT TIME ZONE 'UTC'", "", true, false, "t.a AT TIME ZONE 'UTC'")]
    [InlineData("ISNULL(t.a, 0)", "", true, false, "ISNULL(t.a, 0)")]
    // A user's function, or anything that holds one, is never asked.
    [InlineData("dbo.COUNT(t.a)", "", false, false, "")]
    [InlineData("dbo.f(t.a) + 1", "", false, false, "")]
    [InlineData("ISNULL(dbo.f(t.a), 0)", "", false, false, "")]
    [InlineData("t.x.value('(/a)[1]', 'int')", "", false, false, "")]
    [InlineData("geography::Point(1, 2, 4326)", "", false, false, "")]
    // Nor is anything that holds an aggregate or OVER that is not a form.
    [InlineData("AVG(t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("[COUNT](t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("JSON_ARRAYAGG(t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("ISNULL(MAX(t.a), 0)", "GROUP BY t.b", false, false, "")]
    [InlineData("CASE WHEN SUM(t.a) > 0 THEN 1 ELSE 0 END", "GROUP BY t.b", false, false, "")]
    [InlineData("LAG(t.a, 1, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    // Columns, in the select's context.
    [InlineData("t.a", "GROUP BY ROLLUP (t.a)", false, false, "")]
    [InlineData("t.a", "GROUP BY t.a", true, false, "t.a")]
    [InlineData("[t].[a]", "", true, false, "[t].[a]")]
    [InlineData("(t.a)", "", true, false, "t.a")]
    // Literals, NULL and parameters.
    [InlineData("'x'", "", true, true, "")]
    [InlineData("N'x'", "", true, true, "")]
    [InlineData("1", "", true, true, "")]
    [InlineData("0x00", "", true, true, "")]
    [InlineData("NULL", "", false, false, "")]
    [InlineData("@p", "", false, false, "")]
    [InlineData("(@p)", "", false, false, "")]
    public void Of_Form_IsProvedAsExpected(
        string item,
        string tail,
        bool provedWhenOperandsNotNull,
        bool provedWhenOperandsNull,
        string operands
    )
    {
        var (proved, unproved, asked) = Prove(item, tail);

        proved.ShouldBe(provedWhenOperandsNotNull);
        unproved.ShouldBe(provedWhenOperandsNull);
        asked.ShouldBe(operands);
    }

    [Fact]
    public void Operands_SameRangeTwice_AskedOnce()
    {
        var (sql, tokens, proofs, body) = Read("SELECT COALESCE(t.a, t.a) AS c FROM t AS t");

        var proof = proofs.Of(body.Items[0].Expression);

        Texts(sql, tokens, proofs.Operands).ShouldBe("t.a");
        proof.Holds([true]).ShouldBeTrue();
        proof.Holds([false]).ShouldBeFalse();
    }

    // The operands of every item of a select are one list, in order of first use, and an item's proof indexes it.
    [Fact]
    public void Operands_TwoItems_OneListInOrder()
    {
        var (sql, tokens, proofs, body) = Read("SELECT COALESCE(t.b, t.a) AS c, CAST(t.a AS int) AS d FROM t AS t");

        var first = proofs.Of(body.Items[0].Expression);
        var second = proofs.Of(body.Items[1].Expression);

        Texts(sql, tokens, proofs.Operands).ShouldBe("t.b|t.a");
        second.Holds([false, true]).ShouldBeTrue();
        second.Holds([true, false]).ShouldBeFalse();
        first.Holds([false, true]).ShouldBeTrue();
    }

    [Fact]
    public void NeedsOperands_FormOfOperands_IsTrue()
    {
        var (_, _, proofs, body) = Read("SELECT COALESCE(t.a, t.b) AS c FROM t AS t");

        proofs.Of(body.Items[0].Expression).NeedsOperands.ShouldBeTrue();
    }

    [Fact]
    public void NeedsOperands_ProvedFromTheTextAlone_IsFalse()
    {
        var (_, _, proofs, body) = Read("SELECT COUNT(*) AS c FROM t AS t");

        proofs.Of(body.Items[0].Expression).NeedsOperands.ShouldBeFalse();
        proofs.Operands.ShouldBeEmpty();
    }

    private static (bool Proved, bool Unproved, string Operands) Prove(string item, string tail)
    {
        var (sql, tokens, proofs, body) = Read($"SELECT {item} AS c FROM t AS t {tail}");
        body.Items.Count.ShouldBe(1);

        var proof = proofs.Of(body.Items[0].Expression);

        var count = proofs.Operands.Count;
        return (
            proof.Holds([.. Enumerable.Repeat(true, count)]),
            proof.Holds([.. Enumerable.Repeat(false, count)]),
            Texts(sql, tokens, proofs.Operands)
        );
    }

    private static (string Sql, IReadOnlyList<TsqlToken> Tokens, ExpressionProofs Proofs, SelectBody Body) Read(
        string sql
    )
    {
        var tokens = TsqlTokens.Read(sql);
        var body = SelectStatement.Read(tokens).ShouldNotBeNull().Body;
        return (sql, tokens, new ExpressionProofs(tokens, body), body);
    }

    // Each operand's text as written, joined by "|".
    private static string Texts(string sql, IReadOnlyList<TsqlToken> tokens, IReadOnlyList<TokenRange> operands) =>
        string.Join('|', operands.Select(range => sql[tokens[range.Start].Start..tokens[range.End - 1].End]));
}
