using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;
using static SqlSource.Tool.Tests.SqlServerScript;

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
    // COUNT is never null, whatever the window it names inside its parentheses.
    [InlineData(
        "COUNT(*) OVER (w)",
        "WINDOW w AS (ORDER BY t.id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)",
        true,
        true,
        ""
    )]
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
    [InlineData("(SELECT COUNT(*) FROM o FOR XML PATH(''))", "", false, false, "")]
    // A subquery of another form is a leaf, asked whole unless it holds an aggregate, OVER or a user's function.  The
    // leaves below each stand inside COALESCE(..., NULL), since an item that is a leaf alone asks nothing.
    [InlineData("COALESCE((SELECT TOP (1) o.x FROM o), NULL)", "", true, false, "(SELECT TOP (1) o.x FROM o)")]
    [InlineData("COALESCE((SELECT MAX(o.x) FROM o), NULL)", "", false, false, "")]
    [InlineData("COALESCE((SELECT COUNT(*) FROM o WHERE o.c = t.id), 0)", "", true, true, "")]
    [InlineData(
        "COALESCE(CASE WHEN EXISTS (SELECT 1 FROM o WHERE o.c = t.id) THEN 1 ELSE 0 END, NULL)",
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
    [InlineData("NTILE((4)) OVER (ORDER BY t.a)", "", false, false, "")]
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
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS 0 PRECEDING)", "", true, false, "t.a")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id RANGE CURRENT ROW)", "", true, false, "t.a")]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN CURRENT ROW AND CURRENT ROW)", "", true, false, "t.a")]
    [InlineData(
        "SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING)",
        "",
        true,
        false,
        "t.a"
    )]
    [InlineData("SUM(t.a) OVER (ORDER BY t.id ROWS BETWEEN 0 FOLLOWING AND 1 FOLLOWING)", "", false, false, "")]
    [InlineData("MAX(t.a) OVER w", "WINDOW w AS (ORDER BY t.id)", false, false, "")]
    // "partition" is no reserved word, so a window may be named so.
    [InlineData(
        "SUM(t.a) OVER (partition)",
        "WINDOW partition AS (ORDER BY t.id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)",
        false,
        false,
        ""
    )]
    [InlineData("SUM(t.a) OVER (partition ORDER BY t.id)", "WINDOW partition AS (PARTITION BY t.b)", false, false, "")]
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
    // COALESCE, GREATEST and LEAST: one argument proved, and every argument of a type that is no CLR type, which is
    // asked of each that is not a literal, in any context, a parameter's and a grouping set's included.
    [InlineData("COALESCE(t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("COALESCE(t.a, 0)", "", true, true, "t.a")]
    [InlineData("COALESCE(t.a, NULL, @p)", "", true, false, "t.a|@p")]
    [InlineData("COALESCE(t.a, t.b)", "GROUP BY ROLLUP (t.a, t.b)", false, false, "")]
    [InlineData("COALESCE(t.a, 0)", "GROUP BY ROLLUP (t.a)", true, true, "t.a")]
    [InlineData("COALESCE(MAX(t.a), 0)", "", true, true, "t.a")]
    [InlineData("COALESCE(NULL, @p)", "", false, false, "")]
    [InlineData("GREATEST(t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("LEAST(t.a, 1)", "", true, true, "t.a")]
    [InlineData("GREATEST(t.a, @p)", "", true, false, "t.a|@p")]
    [InlineData("GREATEST(@p, NULL)", "", false, false, "")]
    // An argument whose type is known from its form asks nothing; one whose type cannot be asked proves nothing.
    [InlineData("COALESCE(CAST(t.a AS int), 0)", "", true, true, "")]
    [InlineData("COALESCE(SUM(t.a), 0)", "GROUP BY t.b", true, true, "")]
    [InlineData("COALESCE(JSON_OBJECT('a': t.a), N'')", "", true, true, "")]
    [InlineData("COALESCE(t.a * 2, 0)", "", true, true, "t.a")]
    [InlineData("COALESCE(dbo.f(t.a), 0)", "", false, false, "")]
    [InlineData("COALESCE(MAX(dbo.f(t.a)), 0)", "GROUP BY t.b", false, false, "")]
    [InlineData("COALESCE(LAG(t.a) OVER (ORDER BY t.id), 0)", "", false, false, "")]
    [InlineData("COALESCE(CAST(t.a AS geometry), 0)", "", false, false, "")]
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
    [InlineData("CONVERT(varchar(10), t.d, -1)", "", false, false, "")]
    [InlineData("CONVERT(varchar(10), t.d, (23))", "", false, false, "")]
    // TRY_CAST is no form: it is asked whole inside a form, and the server answers it.
    [InlineData("CAST(TRY_CAST(t.a AS int) AS bigint)", "", true, false, "TRY_CAST(t.a AS int)")]
    // JSON_OBJECT and JSON_ARRAY.
    [InlineData("JSON_OBJECT('a': t.a)", "", true, true, "")]
    [InlineData("JSON_ARRAY(t.a)", "", true, true, "")]
    // Arithmetic: every operand proved.
    [InlineData("t.a * t.b", "", true, false, "t.a|t.b")]
    [InlineData("(t.a + t.b) % 2", "", true, false, "t.a|t.b")]
    [InlineData("COALESCE(p.name + N' > ', N'') + t.name", "", true, false, "p.name|t.name")]
    [InlineData("-t.a", "", true, false, "t.a")]
    [InlineData("~t.a - -1", "", true, false, "t.a")]
    [InlineData("t.a + NULL", "", false, false, "")]
    [InlineData("t.a / @p", "", false, false, "")]
    [InlineData("SUM(t.q * t.p)", "GROUP BY t.b", true, false, "t.q|t.p")]
    [InlineData("SUM(t.a) + MAX(t.b)", "GROUP BY ROLLUP (t.c)", true, false, "t.a|t.b")]
    [InlineData("t.b + SUM(t.a)", "GROUP BY ROLLUP (t.b)", false, false, "")]
    [InlineData("(SELECT COUNT(*) FROM o) + t.a", "", true, false, "t.a")]
    // The forms nest, each operand in its own context.
    [InlineData("COALESCE(CAST(SUM(t.a) AS int), t.b)", "GROUP BY ROLLUP (t.b)", true, false, "t.a|t.b")]
    [InlineData("SUM(t.a)", "GROUP BY t.b HAVING SUM(t.a) > 0", true, false, "t.a")]
    [InlineData("NTILE(4) OVER (ORDER BY t.b)", "GROUP BY ROLLUP (t.b)", true, true, "")]
    [InlineData("COUNT(*) WITHIN GROUP (ORDER BY t.a)", "", false, false, "")]
    // Anything else inside a form is asked whole.
    [InlineData("COALESCE(t.a & t.b, NULL)", "", true, false, "t.a & t.b")]
    [InlineData("COALESCE(t.a COLLATE Latin1_General_BIN, NULL)", "", true, false, "t.a COLLATE Latin1_General_BIN")]
    [InlineData("COALESCE(t.a AT TIME ZONE 'UTC', NULL)", "", true, false, "t.a AT TIME ZONE 'UTC'")]
    [InlineData("COALESCE(ISNULL(t.a, 0), NULL)", "", true, false, "ISNULL(t.a, 0)")]
    [InlineData("COALESCE(NULLIF(t.a, 0), NULL)", "", true, false, "NULLIF(t.a, 0)")]
    [InlineData("COALESCE(IIF(t.a > 0, 1, 0), NULL)", "", true, false, "IIF(t.a > 0, 1, 0)")]
    [InlineData("COALESCE(TRY_CONVERT(int, t.a), NULL)", "", true, false, "TRY_CONVERT(int, t.a)")]
    [InlineData("COALESCE(PARSE(t.s AS int USING 'en-US'), NULL)", "", true, false, "PARSE(t.s AS int USING 'en-US')")]
    // A ":" of JSON_OBJECT is no "::".
    [InlineData(
        "COALESCE(ISNULL(JSON_OBJECT('a': UPPER(t.x)), ''), NULL)",
        "",
        true,
        false,
        "ISNULL(JSON_OBJECT('a': UPPER(t.x)), '')"
    )]
    // An item that is one leaf alone, its outer parentheses aside, is what the server answered already: it proves
    // nothing and asks nothing.  An operator around the leaf makes a form, which asks it, as a sign does above.
    [InlineData("t.a", "", false, false, "")]
    [InlineData("((t.a))", "", false, false, "")]
    [InlineData("(SELECT TOP (1) o.x FROM o)", "", false, false, "")]
    [InlineData("CASE WHEN t.a > 0 THEN 1 ELSE 0 END", "", false, false, "")]
    [InlineData("TRY_CAST(t.a AS int)", "", false, false, "")]
    [InlineData("t.a AT TIME ZONE 'UTC'", "", false, false, "")]
    [InlineData("ISNULL(t.a, 0)", "", false, false, "")]
    [InlineData("t.a + 1", "", true, false, "t.a")]
    // A user's function, or anything that holds one, is never asked.
    [InlineData("dbo.COUNT(t.a)", "", false, false, "")]
    [InlineData("dbo.f(t.a) + 1", "", false, false, "")]
    [InlineData("ISNULL(dbo.f(t.a), 0)", "", false, false, "")]
    [InlineData("t.x.value('(/a)[1]', 'int')", "", false, false, "")]
    [InlineData("geography::Point(1, 2, 4326)", "", false, false, "")]
    [InlineData("CAST(t.a AS int).ToString()", "", false, false, "")]
    [InlineData("x.SUM(t.a)", "GROUP BY t.b", false, false, "")]
    // An old table hint after a schema's table reads as a call of a schema's name.
    [InlineData("ISNULL((SELECT o.x FROM dbo.o (NOLOCK)), 0)", "", false, false, "")]
    // Nor is anything that holds an aggregate or OVER that is not a form.
    [InlineData("AVG(t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("[COUNT](t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("JSON_ARRAYAGG(t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("JSON_OBJECTAGG(t.k : t.v)", "GROUP BY t.b", false, false, "")]
    [InlineData("ISNULL(MAX(t.a), 0)", "GROUP BY t.b", false, false, "")]
    [InlineData("CASE WHEN SUM(t.a) > 0 THEN 1 ELSE 0 END", "GROUP BY t.b", false, false, "")]
    [InlineData("LAG(t.a, 1, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    // Columns, in the select's context.
    [InlineData("t.a", "GROUP BY ROLLUP (t.a)", false, false, "")]
    [InlineData("COALESCE(t.a, NULL)", "GROUP BY t.a", true, false, "t.a")]
    [InlineData("COALESCE([t].[a], NULL)", "", true, false, "[t].[a]")]
    [InlineData("COALESCE((t.a), NULL)", "", true, false, "t.a")]
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

    // A string converted to a CLR type may be its Null: with every operand answered not null but of a CLR type, none
    // of these is proved, and each is with every operand of no CLR type.
    [Theory]
    [InlineData("COALESCE(t.s, 'NULL')", "")]
    [InlineData("COALESCE(t.a, t.b)", "")]
    [InlineData("GREATEST(t.h, 'NULL')", "")]
    [InlineData("LEAST(t.a, 1)", "")]
    [InlineData("COALESCE(@p, 'NULL')", "")]
    [InlineData("COALESCE(t.a, 0)", "GROUP BY ROLLUP (t.a)")]
    [InlineData("COALESCE(MAX(t.a), 0)", "GROUP BY t.b")]
    [InlineData("COALESCE(MIN(t.s) OVER (), 'NULL')", "")]
    [InlineData("COALESCE((t.s), N'NULL')", "")]
    [InlineData("COALESCE(-t.a, 0)", "")]
    [InlineData("COALESCE(t.a + t.b, 0)", "")]
    [InlineData("COALESCE(CASE WHEN t.a > 0 THEN t.s END, 'NULL')", "")]
    [InlineData("COALESCE(ISNULL(t.s, 'x'), 'NULL')", "")]
    [InlineData("COALESCE((SELECT TOP (1) o.s FROM o), 'NULL')", "")]
    [InlineData("CAST(COALESCE(t.s, 'NULL') AS varbinary(max))", "")]
    [InlineData("COALESCE(COALESCE(t.s, 'NULL'), 'x')", "")]
    // A CLR type's conversion to a string is its ToString, which a user's type may answer null.
    [InlineData("CAST(t.s AS nvarchar(100))", "")]
    [InlineData("CONVERT(varbinary(max), t.s)", "")]
    public void Of_OperandOfAClrType_IsNotProved(string item, string tail)
    {
        var (sql, tokens, proofs, body) = Read($"SELECT {item} AS c FROM t AS t {tail}");
        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        var count = proofs.Operands.Count;
        count.ShouldBeGreaterThan(0, Texts(sql, tokens, proofs.Operands));
        proof.Holds([.. Enumerable.Repeat(new OperandFacts(NotNull: true, NotClr: true), count)]).ShouldBeTrue();
        proof.Holds([.. Enumerable.Repeat(new OperandFacts(NotNull: true, NotClr: false), count)]).ShouldBeFalse();
    }

    // CAST and CONVERT prove only to a built-in type that is no CLR type, written as one word, with or without its
    // facets: a CLR type's Parse may answer its Null for a string such as 'NULL'.
    [Theory]
    [InlineData("int", true)]
    [InlineData("BIGINT", true)]
    [InlineData("sysname", true)]
    [InlineData("nvarchar(max)", true)]
    [InlineData("nvarchar(40)", true)]
    [InlineData("decimal(10, 2)", true)]
    [InlineData("float(53)", true)]
    [InlineData("datetime2(7)", true)]
    [InlineData("date", true)]
    [InlineData("varbinary(MAX)", true)]
    [InlineData("uniqueidentifier", true)]
    [InlineData("xml", true)]
    [InlineData("sql_variant", true)]
    [InlineData("geometry", false)]
    [InlineData("geography", false)]
    [InlineData("hierarchyid", false)]
    [InlineData("dbo.Point", false)]
    [InlineData("sys.geometry", false)]
    [InlineData("[int]", false)]
    [InlineData("Point", false)]
    [InlineData("json", false)]
    [InlineData("vector(3)", false)]
    [InlineData("integer", false)]
    [InlineData("double precision", false)]
    [InlineData("varchar(10, 2, 3)", false)]
    [InlineData("varchar(@n)", false)]
    [InlineData("varchar(-1)", false)]
    [InlineData("varchar(1.5)", false)]
    [InlineData("varchar 10", false)]
    [InlineData("xml(CONTENT dbo.s)", false)]
    public void Of_CastTarget_ProvedOnlyForABuiltInType(string target, bool proved)
    {
        foreach (var item in (string[])[$"CAST(t.a AS {target})", $"CONVERT({target}, t.a)", $"CAST(1 AS {target})"])
        {
            var (_, _, proofs, body) = Read($"SELECT {item} AS c FROM t AS t");
            var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

            proof.Holds(Answers([.. Enumerable.Repeat(true, proofs.Operands.Count)])).ShouldBe(proved, item);
        }
    }

    [Theory]
    [InlineData("CAST(N'NULL' AS geometry)")]
    [InlineData("CONVERT(geography, N'POINT (1 2)')")]
    [InlineData("CAST(0x00 AS dbo.Point)")]
    public void Of_LiteralCastToAClrType_IsNotProved(string item)
    {
        var (_, _, proofs, body) = Read($"SELECT {item} AS c FROM t AS t");

        proofs.Of(body.Items.ShouldHaveSingleItem().Expression).Holds(null).ShouldBeFalse();
        proofs.Operands.ShouldBeEmpty();
    }

    [Fact]
    public void Operands_SameRangeTwice_AskedOnce()
    {
        var (sql, tokens, proofs, body) = Read("SELECT COALESCE(t.a, t.a) AS c FROM t AS t");

        var proof = proofs.Of(body.Items[0].Expression);

        Texts(sql, tokens, proofs.Operands).ShouldBe("t.a");
        proof.Holds(Answers(true)).ShouldBeTrue();
        proof.Holds(Answers(false)).ShouldBeFalse();
    }

    // The operands of every item of a select are one list, in order of first use, and an item's proof indexes it.
    [Fact]
    public void Operands_TwoItems_OneListInOrder()
    {
        var (sql, tokens, proofs, body) = Read("SELECT COALESCE(t.b, t.a) AS c, CAST(t.a AS int) AS d FROM t AS t");

        var first = proofs.Of(body.Items[0].Expression);
        var second = proofs.Of(body.Items[1].Expression);

        Texts(sql, tokens, proofs.Operands).ShouldBe("t.b|t.a");
        second.Holds(Answers(false, true)).ShouldBeTrue();
        second.Holds(Answers(true, false)).ShouldBeFalse();
        first.Holds(Answers(false, true)).ShouldBeTrue();
    }

    [Fact]
    public void Of_FormOfOperands_AsksEachOperand()
    {
        var (sql, tokens, proofs, body) = Read("SELECT COALESCE(t.a, t.b) AS c FROM t AS t");

        var proof = proofs.Of(body.Items[0].Expression);

        Texts(sql, tokens, proofs.Operands).ShouldBe("t.a|t.b");
        proof.Holds(null).ShouldBeFalse();
        proof.Holds(Answers(false, true)).ShouldBeTrue();
    }

    [Fact]
    public void Of_ProvedFromTheTextAlone_AsksNothing()
    {
        var (_, _, proofs, body) = Read("SELECT COUNT(*) AS c FROM t AS t");

        var proof = proofs.Of(body.Items[0].Expression);

        proofs.Operands.ShouldBeEmpty();
        proof.Holds(null).ShouldBeTrue();
    }

    private static (bool Proved, bool Unproved, string Operands) Prove(string item, string tail)
    {
        var (sql, tokens, proofs, body) = Read($"SELECT {item} AS c FROM t AS t {tail}");
        body.Items.Count.ShouldBe(1);

        var proof = proofs.Of(body.Items[0].Expression);

        var count = proofs.Operands.Count;
        return (
            proof.Holds(Answers([.. Enumerable.Repeat(true, count)])),
            proof.Holds(Answers([.. Enumerable.Repeat(false, count)])),
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
