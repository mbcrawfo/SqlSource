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
    [InlineData(
        "COALESCE(CASE WHEN EXISTS (SELECT 1 FROM o HAVING MAX(t.a) > 0) THEN 1 ELSE 0 END, NULL)",
        "",
        false,
        false,
        ""
    )]
    [InlineData("COALESCE((SELECT dbo.f(o.x) FROM o), NULL)", "", false, false, "")]
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
    // asked of each that is not a literal, in any context, a parameter's and a grouping set's included.  An argument
    // of COALESCE before the proved one must also give one value, which these answers, none of them stored, do not say.
    [InlineData("COALESCE(t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("COALESCE(t.a, 0)", "", true, false, "t.a")]
    [InlineData("COALESCE(t.a, NULL, @p)", "", true, false, "t.a|@p")]
    [InlineData("COALESCE(t.a, t.b)", "GROUP BY ROLLUP (t.a, t.b)", false, false, "")]
    [InlineData("COALESCE(t.a, 0)", "GROUP BY ROLLUP (t.a)", false, false, "t.a")]
    [InlineData("COALESCE(MAX(t.a), 0)", "", false, false, "t.a")]
    [InlineData("COALESCE(NULL, @p)", "", false, false, "")]
    [InlineData("GREATEST(t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("LEAST(t.a, 1)", "", true, true, "t.a")]
    [InlineData("GREATEST(t.a, @p)", "", true, false, "t.a|@p")]
    [InlineData("GREATEST(@p, NULL)", "", false, false, "")]
    // An argument whose type is known from its form asks nothing; one whose type cannot be asked proves nothing.
    [InlineData("COALESCE(CAST(t.a AS int), 0)", "", true, false, "t.a")]
    [InlineData("COALESCE(SUM(t.a), 0)", "GROUP BY t.b", true, false, "t.a")]
    [InlineData("COALESCE(JSON_OBJECT('a': t.a), N'')", "", true, true, "")]
    [InlineData("COALESCE(t.a * 2, 0)", "", true, false, "t.a")]
    [InlineData("COALESCE(dbo.f(t.a), 0)", "", false, false, "")]
    [InlineData("COALESCE(MAX(dbo.f(t.a)), 0)", "GROUP BY t.b", false, false, "")]
    [InlineData("COALESCE(LAG(t.a) OVER (ORDER BY t.id), 0)", "", false, false, "")]
    [InlineData("COALESCE(CAST(t.a AS geometry), 0)", "", false, false, "")]
    [InlineData("COALESCE(CONVERT(geometry, t.a), 0)", "", false, false, "")]
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
    [InlineData("CONVERT(varchar(10), t.d, 23 + @p)", "", false, false, "")]
    // TRY_CAST is no form: it is asked whole inside a form, and the server answers it.
    [InlineData("CAST(TRY_CAST(t.a AS int) AS bigint)", "", true, false, "TRY_CAST(t.a AS int)")]
    // JSON_OBJECT and JSON_ARRAY.
    [InlineData("JSON_OBJECT('a': t.a)", "", true, true, "")]
    [InlineData("JSON_ARRAY(t.a)", "", true, true, "")]
    // The date functions: NULL only when an operand is.  The date part is a word and is never asked.
    [InlineData("DATEADD(DAY, 1, t.d)", "", true, false, "t.d")]
    [InlineData("dateadd(day, 1, t.d)", "", true, false, "t.d")]
    [InlineData("DATEADD(DAY, t.n, t.d)", "", true, false, "t.n|t.d")]
    [InlineData("DATEADD(DAY, @n, t.d)", "", false, false, "")]
    [InlineData("DATEADD(DAY, NULL, t.d)", "", false, false, "")]
    [InlineData("DATEADD(DAY, 1, NULL)", "", false, false, "")]
    [InlineData("DATEADD(DAY, 1, MAX(t.d))", "GROUP BY ROLLUP (t.b)", true, false, "t.d")]
    [InlineData("DATEDIFF(DAY, t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("DATEDIFF_BIG(DAY, t.a, '2020-01-01')", "", true, false, "t.a")]
    [InlineData("DATEDIFF(DAY, t.a, @p)", "", false, false, "")]
    [InlineData("DATEPART(YEAR, t.d)", "", true, false, "t.d")]
    [InlineData("DATENAME(WEEKDAY, t.d)", "", true, false, "t.d")]
    [InlineData("YEAR(t.d)", "", true, false, "t.d")]
    [InlineData("MONTH(t.d)", "", true, false, "t.d")]
    [InlineData("DAY(t.d)", "", true, false, "t.d")]
    [InlineData("YEAR(t.d)", "GROUP BY ROLLUP (t.d)", false, false, "")]
    [InlineData("EOMONTH(t.d)", "", true, false, "t.d")]
    [InlineData("EOMONTH(t.d, 1)", "", true, false, "t.d")]
    [InlineData("EOMONTH(t.d, t.n)", "", true, false, "t.d|t.n")]
    [InlineData("EOMONTH(t.d, @n)", "", false, false, "")]
    [InlineData("EOMONTH(COALESCE(t.a, t.b))", "", true, false, "t.a|t.b")]
    // A date function of the wrong shape, or that is not the built-in one, proves nothing.
    [InlineData("DATEADD(DAY, 1)", "", false, false, "")]
    [InlineData("DATEADD('DAY', 1, t.d)", "", false, false, "")]
    [InlineData("DATEADD((DAY), 1, t.d)", "", false, false, "")]
    [InlineData("DATEPART(DAY)", "", false, false, "")]
    [InlineData("DATEPART(DAY, t.a, t.b)", "", false, false, "")]
    [InlineData("YEAR(t.a, t.b)", "", false, false, "")]
    [InlineData("EOMONTH(t.d, 1, 2)", "", false, false, "")]
    [InlineData("YEAR(t.d) OVER ()", "", false, false, "")]
    [InlineData("[YEAR](t.d)", "", false, false, "")]
    [InlineData("dbo.YEAR(t.d)", "", false, false, "")]
    // AT TIME ZONE: NULL only when its value or its zone is, each a primary, along a chain.
    [InlineData("t.a AT TIME ZONE 'UTC'", "", true, false, "t.a")]
    [InlineData("t.a at time zone 'UTC'", "", true, false, "t.a")]
    [InlineData("t.a AT TIME ZONE t.z", "", true, false, "t.a|t.z")]
    [InlineData("t.a AT TIME ZONE 'UTC' AT TIME ZONE t.z", "", true, false, "t.a|t.z")]
    [InlineData("t.a AT TIME ZONE COALESCE(t.z, 'UTC')", "", true, false, "t.a|t.z")]
    [InlineData("(t.a) AT TIME ZONE 'UTC'", "", true, false, "t.a")]
    [InlineData("CAST(t.a AS datetime2) AT TIME ZONE 'UTC'", "", true, false, "t.a")]
    [InlineData("COALESCE(t.a AT TIME ZONE 'UTC', NULL)", "", true, false, "t.a")]
    [InlineData("t.a AT TIME ZONE @z", "", false, false, "")]
    [InlineData("t.a AT TIME ZONE NULL", "", false, false, "")]
    [InlineData("NULL AT TIME ZONE 'UTC'", "", false, false, "")]
    [InlineData("t.a AT TIME ZONE 'UTC' AT TIME ZONE @z", "", false, false, "")]
    [InlineData("t.a AT TIME ZONE 'UTC'", "GROUP BY ROLLUP (t.a)", false, false, "")]
    // AT is no reserved word: only the three words are the operator, and a side that is no primary is a leaf.
    [InlineData("COALESCE(t.at, 0)", "", true, false, "t.at")]
    [InlineData("t.a AT TIME 'UTC'", "", false, false, "")]
    [InlineData("t.a COLLATE c AT TIME ZONE 'UTC'", "", false, false, "")]
    [InlineData("t.a AT TIME ZONE 'UTC' COLLATE c", "", false, false, "")]
    // LAG and LEAD: the value and the default proved, and the offset an integer literal.
    [InlineData("LAG(t.a, 1, 0) OVER (ORDER BY t.id)", "", true, false, "t.a")]
    [InlineData("lag(t.a, 0, 'x') OVER (ORDER BY t.id)", "", true, false, "t.a")]
    [InlineData("LEAD(t.a, 2, t.b) OVER (PARTITION BY t.c ORDER BY t.id)", "", true, false, "t.a|t.b")]
    // A LAG or LEAD has no type proof, and a date function or AT TIME ZONE none over what is never asked: COALESCE
    // reads an argument twice, and a window ordered by NEWID() gives two values.
    [InlineData("COALESCE(LAG(t.a, 1, 0) OVER (ORDER BY t.id), NULL)", "", false, false, "")]
    [InlineData("COALESCE(LEAD(t.a, 1, 0) OVER (ORDER BY t.id), 0)", "", false, false, "")]
    [InlineData("COALESCE(LAG(t.a) OVER (ORDER BY t.id) + 1, 0)", "", false, false, "")]
    [InlineData("CAST(LAG(t.a, 1, 0) OVER (ORDER BY t.id) AS bigint)", "", false, false, "")]
    [InlineData("COALESCE(DATEADD(DAY, 1, LAG(t.d) OVER (ORDER BY t.id)), 0)", "", false, false, "")]
    [InlineData("COALESCE(YEAR(LAG(t.d) OVER (ORDER BY t.id)), 0)", "", false, false, "")]
    [InlineData("COALESCE(EOMONTH(dbo.f(t.d)), 0)", "", false, false, "")]
    [InlineData("COALESCE(YEAR(MAX(t.d)), 0)", "GROUP BY t.b", false, false, "")]
    [InlineData("COALESCE(LAG(t.d) OVER (ORDER BY t.id) AT TIME ZONE 'UTC', 0)", "", false, false, "")]
    [InlineData("COALESCE(dbo.f(t.d) AT TIME ZONE 'UTC', 0)", "", false, false, "")]
    [InlineData("COALESCE(DATEADD(DAY, 1, t.d), 0)", "", true, false, "t.d")]
    [InlineData("COALESCE(t.d AT TIME ZONE 'UTC', 0)", "", true, false, "t.d")]
    [InlineData("LAG(SUM(t.a), 1, 0) OVER (ORDER BY t.b)", "GROUP BY t.b", true, false, "t.a")]
    [InlineData("LAG(t.a) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1, NULL) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1, @d) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, @n, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, NULL, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, (1), 0) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1 + 1, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1.5, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, -1, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1, 0, 9) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1, 0)", "", false, false, "")]
    [InlineData("LAG(t.a, 1, 0) OVER w", "WINDOW w AS (ORDER BY t.id)", false, false, "")]
    [InlineData("LAG(t.a, 1, 0) IGNORE NULLS OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("LAG(t.a, 1, 0) OVER (ORDER BY t.a)", "GROUP BY ROLLUP (t.a)", false, false, "")]
    [InlineData("[LAG](t.a, 1, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    // A CASE that replaces a column's NULL.  The column is asked, and the fallback proved.
    [InlineData("CASE WHEN t.a IS NULL THEN 0 ELSE t.a END", "", true, false, "t.a")]
    [InlineData("case when t.a is null then 'x' else t.a end", "", true, false, "t.a")]
    [InlineData("CASE WHEN a IS NULL THEN 0 ELSE a END", "", true, false, "a")]
    [InlineData("CASE WHEN [t].[a] IS NULL THEN 0 ELSE [t].[a] END", "", true, false, "[t].[a]")]
    [InlineData("CASE WHEN t.a IS NULL THEN t.b ELSE t.a END", "", true, false, "t.b|t.a")]
    [InlineData("CASE WHEN t.a IS NULL THEN COALESCE(t.b, 0) ELSE t.a END", "", true, false, "t.b|t.a")]
    [InlineData(
        "CASE WHEN t.a IS NULL THEN CASE WHEN t.b > 0 THEN 1 ELSE 0 END ELSE t.a END",
        "",
        true,
        false,
        "CASE WHEN t.b > 0 THEN 1 ELSE 0 END|t.a"
    )]
    [InlineData("CASE WHEN t.a IS NULL THEN NULL ELSE t.a END", "", false, false, "")]
    [InlineData("CASE WHEN t.a IS NULL THEN @p ELSE t.a END", "", false, false, "")]
    // Under a grouping set the column is asked its source and its type, but not whether it is null.
    [InlineData("CASE WHEN t.a IS NULL THEN 0 ELSE t.a END", "GROUP BY ROLLUP (t.a)", false, false, "t.a")]
    // One token from the form: each is a CASE alone, a leaf that asks nothing.
    [InlineData("CASE WHEN t.a IS NOT NULL THEN 0 ELSE t.a END", "", false, false, "")]
    [InlineData("CASE WHEN t.a IS NULL THEN 0 ELSE t.b END", "", false, false, "")]
    [InlineData("CASE WHEN t.a IS NULL THEN 0 ELSE [t].[a] END", "", false, false, "")]
    [InlineData("CASE WHEN t.a IS NULL THEN 0 ELSE T.A END", "", false, false, "")]
    [InlineData("CASE WHEN t.a IS NULL THEN 0 ELSE t.a + 1 END", "", false, false, "")]
    [InlineData("CASE WHEN t.a IS NULL THEN 0 END", "", false, false, "")]
    [InlineData("CASE WHEN t.a IS NULL THEN 0 WHEN t.b = 1 THEN 1 ELSE t.a END", "", false, false, "")]
    [InlineData("CASE t.a WHEN NULL THEN 0 ELSE t.a END", "", false, false, "")]
    [InlineData("CASE WHEN (t.a) IS NULL THEN 0 ELSE (t.a) END", "", false, false, "")]
    [InlineData("CASE WHEN t.a + 1 IS NULL THEN 0 ELSE t.a + 1 END", "", false, false, "")]
    [InlineData("CASE WHEN ABS(t.a) IS NULL THEN 0 ELSE ABS(t.a) END", "", false, false, "")]
    [InlineData("CASE WHEN NULL IS NULL THEN 0 ELSE NULL END", "", false, false, "")]
    [InlineData("CASE WHEN @p IS NULL THEN 0 ELSE @p END", "", false, false, "")]
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
    // A form with a clause it does not take, or the wrong arguments, proves nothing and is not asked.
    [InlineData("COALESCE(COALESCE(1, 2) WITHIN GROUP (ORDER BY t.a), NULL)", "", false, false, "")]
    [InlineData("COALESCE(1, 2) OVER (ORDER BY t.a)", "", false, false, "")]
    [InlineData("CAST(1 AS int) OVER (ORDER BY t.a)", "", false, false, "")]
    [InlineData("CONVERT(int, 1) OVER (ORDER BY t.a)", "", false, false, "")]
    [InlineData("JSON_ARRAY(1) OVER (ORDER BY t.a)", "", false, false, "")]
    [InlineData("NTILE(4)", "", false, false, "")]
    [InlineData("NTILE(2, 3) OVER (ORDER BY t.a)", "", false, false, "")]
    [InlineData("SUM(1, 2)", "GROUP BY t.b", false, false, "")]
    // A frame that may leave out the current row, of either word, whatever its first bound looks like.
    [InlineData("SUM(1) OVER (ORDER BY t.id RANGE BETWEEN 1 FOLLOWING AND 2 FOLLOWING)", "", false, false, "")]
    [InlineData(
        "SUM(1) OVER (ORDER BY t.id ROWS BETWEEN CURRENT FOLLOWING AND UNBOUNDED FOLLOWING)",
        "",
        false,
        false,
        ""
    )]
    // Anything else inside a form is asked whole.
    [InlineData("COALESCE(t.a & t.b, NULL)", "", true, false, "t.a & t.b")]
    [InlineData("COALESCE(t.a COLLATE Latin1_General_BIN, NULL)", "", true, false, "t.a COLLATE Latin1_General_BIN")]
    [InlineData("COALESCE(NULLIF(t.a, 0), NULL)", "", true, false, "NULLIF(t.a, 0)")]
    [InlineData("COALESCE(IIF(t.a > 0, 1, 0), NULL)", "", true, false, "IIF(t.a > 0, 1, 0)")]
    [InlineData("COALESCE(TRY_CONVERT(int, t.a), NULL)", "", true, false, "TRY_CONVERT(int, t.a)")]
    [InlineData("COALESCE(PARSE(t.s AS int USING 'en-US'), NULL)", "", true, false, "PARSE(t.s AS int USING 'en-US')")]
    // A ":" of JSON_OBJECT is no "::".
    [InlineData(
        "COALESCE(NULLIF(JSON_OBJECT('a': UPPER(t.x)), ''), NULL)",
        "",
        true,
        false,
        "NULLIF(JSON_OBJECT('a': UPPER(t.x)), '')"
    )]
    // An item that is one leaf alone, its outer parentheses aside, is what the server answered already: it proves
    // nothing and asks nothing.  An operator around the leaf makes a form, which asks it, as a sign does above.
    [InlineData("t.a", "", false, false, "")]
    [InlineData("((t.a))", "", false, false, "")]
    [InlineData("(SELECT TOP (1) o.x FROM o)", "", false, false, "")]
    [InlineData("CASE WHEN t.a > 0 THEN 1 ELSE 0 END", "", false, false, "")]
    [InlineData("TRY_CAST(t.a AS int)", "", false, false, "")]
    [InlineData("t.a + 1", "", true, false, "t.a")]
    // A user's function, or anything that holds one, is never asked.  Each item that is no form stands inside
    // COALESCE(..., NULL), since a leaf alone asks nothing.
    [InlineData("dbo.COUNT(t.a)", "", false, false, "")]
    [InlineData("dbo.f(t.a) + 1", "", false, false, "")]
    [InlineData("COALESCE(ISNULL(dbo.f(t.a), 0), NULL)", "", false, false, "")]
    [InlineData("COALESCE(t.x.value('(/a)[1]', 'int'), NULL)", "", false, false, "")]
    [InlineData("COALESCE(geography::Point(1, 2, 4326), NULL)", "", false, false, "")]
    [InlineData("CAST(t.a AS int).ToString()", "", false, false, "")]
    [InlineData("x.SUM(t.a)", "GROUP BY t.b", false, false, "")]
    // An old table hint after a schema's table reads as a call of a schema's name.
    [InlineData("ISNULL((SELECT o.x FROM dbo.o (NOLOCK)), 0)", "", false, false, "")]
    // Nor is anything that holds an aggregate or OVER that is not a form, each item that is no form again inside
    // COALESCE(..., NULL).
    [InlineData("COALESCE(AVG(t.a), NULL)", "GROUP BY t.b", false, false, "")]
    [InlineData("[COUNT](t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("JSON_ARRAYAGG(t.a)", "GROUP BY t.b", false, false, "")]
    [InlineData("JSON_OBJECTAGG(t.k : t.v)", "GROUP BY t.b", false, false, "")]
    [InlineData("COALESCE(NULLIF(MAX(t.a), 0), NULL)", "GROUP BY t.b", false, false, "")]
    [InlineData("COALESCE(CASE WHEN SUM(t.a) > 0 THEN 1 ELSE 0 END, NULL)", "GROUP BY t.b", false, false, "")]
    [InlineData("COALESCE(LAG(t.a, @n, 0) OVER (ORDER BY t.id), NULL)", "", false, false, "")]
    // ISNULL(a, b) is a, or b converted to a's type, so one proved argument proves it, with both of no CLR type.  A
    // literal, or an argument a user's function or something never asked holds, has its own proof.
    [InlineData("ISNULL(t.a, 0)", "", true, true, "t.a")]
    [InlineData("isnull(0, t.a)", "", true, true, "t.a")]
    [InlineData("ISNULL(t.a, t.b)", "", true, false, "t.a|t.b")]
    [InlineData("ISNULL(t.a, NULL)", "", true, false, "t.a")]
    [InlineData("ISNULL(NULL, t.a)", "", true, false, "t.a")]
    [InlineData("ISNULL(@p, 0)", "", true, true, "@p")]
    [InlineData("ISNULL(NULL, NULL)", "", false, false, "")]
    [InlineData("COALESCE(ISNULL(t.a, 0), NULL)", "", true, true, "t.a")]
    [InlineData("ISNULL(MAX(t.a), 0)", "GROUP BY t.b", true, true, "t.a")]
    [InlineData("ISNULL(t.a, 0)", "GROUP BY ROLLUP (t.a)", true, true, "t.a")]
    [InlineData("ISNULL(t.a, t.b)", "GROUP BY ROLLUP (t.a, t.b)", false, false, "")]
    [InlineData("ISNULL(dbo.f(t.a), 0)", "", false, false, "")]
    [InlineData("ISNULL(t.a, 0, 1)", "", false, false, "")]
    [InlineData("ISNULL(t.a)", "", false, false, "")]
    [InlineData("ISNULL(t.a, 0) OVER (ORDER BY t.id)", "", false, false, "")]
    [InlineData("[ISNULL](t.a, 0)", "", false, false, "")]
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

    // Every form, one of each of its names, through both readings: alone, the proof of the form, and inside
    // COALESCE(..., 0), the proof of its type.  With every operand answered not null and of no CLR type, each proves
    // both ways; with every operand of a CLR type, the form inside COALESCE proves only when its type is fixed.
    [Theory]
    [InlineData("COUNT(t.a)", "", true)]
    [InlineData("COUNT_BIG(t.a)", "", true)]
    [InlineData("ROW_NUMBER() OVER ()", "", true)]
    [InlineData("RANK() OVER ()", "", true)]
    [InlineData("DENSE_RANK() OVER ()", "", true)]
    [InlineData("NTILE(2) OVER ()", "", true)]
    [InlineData("SUM(t.a)", "GROUP BY t.b", true)]
    [InlineData("MAX(t.a)", "GROUP BY t.b", false)]
    [InlineData("MIN(t.a)", "GROUP BY t.b", false)]
    [InlineData("COALESCE(t.a, 0)", "", false)]
    [InlineData("ISNULL(t.a, 0)", "", false)]
    [InlineData("GREATEST(t.a, 0)", "", false)]
    [InlineData("LEAST(t.a, 0)", "", false)]
    [InlineData("CAST(t.a AS int)", "", true)]
    [InlineData("CONVERT(int, t.a)", "", true)]
    [InlineData("JSON_OBJECT('a': t.a)", "", true)]
    [InlineData("JSON_ARRAY(t.a)", "", true)]
    [InlineData("DATEADD(DAY, t.n, t.a)", "", true)]
    [InlineData("DATEDIFF(DAY, t.a, t.b)", "", true)]
    [InlineData("DATEDIFF_BIG(DAY, t.a, t.b)", "", true)]
    [InlineData("DATEPART(DAY, t.a)", "", true)]
    [InlineData("DATENAME(DAY, t.a)", "", true)]
    [InlineData("YEAR(t.a)", "", true)]
    [InlineData("MONTH(t.a)", "", true)]
    [InlineData("DAY(t.a)", "", true)]
    [InlineData("EOMONTH(t.a)", "", true)]
    public void Of_EveryForm_ProvedByBothReadings(string form, string tail, bool typeFixed)
    {
        foreach (var item in (string[])[form, $"COALESCE({form}, 0)"])
        {
            var (_, _, proofs, body) = Read($"SELECT {item} AS c FROM t AS t {tail}");
            var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

            var count = proofs.Operands.Count;
            proof
                .Holds([.. Enumerable.Repeat(new OperandFacts(NotNull: true, NotClr: true), count)])
                .ShouldBeTrue(item);
        }

        var (_, _, typed, typedBody) = Read($"SELECT COALESCE({form}, 0) AS c FROM t AS t {tail}");
        var typeProof = typed.Of(typedBody.Items.ShouldHaveSingleItem().Expression);

        typeProof
            .Holds([
                .. Enumerable.Repeat(
                    new OperandFacts(NotNull: true, NotClr: false, Stored: true),
                    typed.Operands.Count
                ),
            ])
            .ShouldBe(typeFixed);
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
    [InlineData("ISNULL(t.s, 'NULL')", "")]
    [InlineData("ISNULL(t.a, t.s)", "")]
    // ISNULL(NULL, s) is of s's type, so a CLR type in either argument is ISNULL's.
    [InlineData("COALESCE(ISNULL(NULL, t.s), 'NULL')", "")]
    [InlineData("COALESCE(ISNULL(t.a, t.s), 'NULL')", "")]
    [InlineData("COALESCE((SELECT TOP (1) o.s FROM o), 'NULL')", "")]
    [InlineData("CAST(COALESCE(t.s, 'NULL') AS varbinary(max))", "")]
    [InlineData("COALESCE(COALESCE(t.s, 'NULL'), 'x')", "")]
    // LAG, LEAD and the CASE that replaces a NULL convert their default to the type of the value.
    [InlineData("LAG(t.s, 1, 'NULL') OVER (ORDER BY t.id)", "")]
    [InlineData("LEAD(t.a, 1, t.s) OVER (ORDER BY t.id)", "")]
    [InlineData("CASE WHEN t.s IS NULL THEN 'NULL' ELSE t.s END", "")]
    [InlineData("CASE WHEN t.a IS NULL THEN t.s ELSE t.a END", "")]
    // A CLR type's conversion to a string is its ToString, which a user's type may answer null.
    [InlineData("CAST(t.s AS nvarchar(100))", "")]
    [InlineData("CONVERT(varbinary(max), t.s)", "")]
    public void Of_OperandOfAClrType_IsNotProved(string item, string tail)
    {
        var (sql, tokens, proofs, body) = Read($"SELECT {item} AS c FROM t AS t {tail}");
        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        var count = proofs.Operands.Count;
        count.ShouldBeGreaterThan(0, Texts(sql, tokens, proofs.Operands));
        proof
            .Holds([.. Enumerable.Repeat(new OperandFacts(NotNull: true, NotClr: true, Stored: true), count)])
            .ShouldBeTrue();
        proof
            .Holds([.. Enumerable.Repeat(new OperandFacts(NotNull: true, NotClr: false, Stored: true), count)])
            .ShouldBeFalse();
    }

    // The default of LAG and LEAD and the fallback of the CASE are held of no CLR type themselves, whatever the value's
    // type is.
    [Theory]
    [InlineData("LEAD(t.a, 1, t.s) OVER (ORDER BY t.id)")]
    [InlineData("CASE WHEN t.a IS NULL THEN t.s ELSE t.a END")]
    public void Of_DefaultOfAClrType_IsNotProved(string item)
    {
        var (sql, tokens, proofs, body) = Read($"SELECT {item} AS c FROM t AS t");
        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        var texts = Texts(sql, tokens, proofs.Operands).Split('|');
        texts.ShouldBe(["t.a", "t.s"], ignoreOrder: true);
        proof
            .Holds([.. texts.Select(static text => new OperandFacts(true, NotClr: text != "t.s", Stored: true))])
            .ShouldBeFalse();
        proof.Holds([.. texts.Select(static _ => new OperandFacts(true, NotClr: true, Stored: true))]).ShouldBeTrue();
    }

    // The CASE reads its column twice, and a value evaluated twice can differ: the column must be stored, or be
    // answered not null.  That answer cannot be used under a grouping set, whose totals hold the column NULL.
    [Theory]
    [InlineData(false, true, true, "", true)]
    [InlineData(true, true, false, "", true)]
    [InlineData(true, true, true, "", true)]
    [InlineData(false, true, false, "", false)]
    [InlineData(true, false, true, "", false)]
    [InlineData(false, false, true, "", false)]
    [InlineData(false, true, true, "GROUP BY ROLLUP (t.a)", true)]
    [InlineData(true, true, false, "GROUP BY ROLLUP (t.a)", false)]
    [InlineData(false, true, false, "GROUP BY ROLLUP (t.a)", false)]
    public void Of_CaseOfAColumn_NeedsItStoredOrNotNull(bool notNull, bool notClr, bool stored, string tail, bool holds)
    {
        var (_, _, proofs, body) = Read($"SELECT CASE WHEN t.a IS NULL THEN 0 ELSE t.a END AS c FROM t AS t {tail}");

        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        proofs.Operands.Count.ShouldBe(1);
        proof.Holds([new OperandFacts(notNull, notClr, stored)]).ShouldBe(holds);
    }

    // COALESCE reads an argument twice, so an argument before the one that proves it must give one value in a row.
    // Every operand here is stored and answered nullable, so that only what the walk reads can prove the literal's
    // branch.
    [Theory]
    [InlineData("COALESCE(t.a, 0)", "", true)]
    [InlineData("COALESCE(t.a, 0)", "GROUP BY ROLLUP (t.a)", true)]
    [InlineData("COALESCE(@p, 0)", "", true)]
    [InlineData("COALESCE(NULL, 0)", "", true)]
    [InlineData("COALESCE('x', 0)", "", true)]
    [InlineData("COALESCE((t.a), 0)", "", true)]
    [InlineData("COALESCE(-t.a * 2 + 1, 0)", "", true)]
    [InlineData("COALESCE(t.a, t.b, 0)", "", true)]
    [InlineData("COALESCE(CAST(t.a AS bigint), 0)", "", true)]
    [InlineData("COALESCE(CONVERT(varchar(30), t.a, 1), 0)", "", true)]
    [InlineData("COALESCE(NULLIF(t.a, 0), 0)", "", true)]
    [InlineData("COALESCE(ISNULL(t.a, t.b), 0)", "", true)]
    [InlineData("COALESCE(COALESCE(t.a, t.b), 0)", "", true)]
    [InlineData("COALESCE(MAX(t.a), 0)", "", true)]
    [InlineData("COALESCE(MIN(t.a), 0)", "GROUP BY t.b", true)]
    [InlineData("COALESCE(SUM(DISTINCT t.a), 0)", "GROUP BY t.b", true)]
    [InlineData("COALESCE(CAST(AVG(ALL t.a * 2) AS int), 0)", "GROUP BY t.b", true)]
    [InlineData("COALESCE(AVG(t.a), 0)", "GROUP BY t.b", false)]
    // A value that can change, however it is wrapped.
    [InlineData("COALESCE(CASE WHEN t.a > 0 THEN t.a END, 0)", "", false)]
    [InlineData("COALESCE((SELECT TOP (1) o.x FROM o), 0)", "", false)]
    [InlineData("COALESCE(t.a, (SELECT TOP (1) o.x FROM o), 0)", "", false)]
    [InlineData("COALESCE(RAND(), 0)", "", false)]
    [InlineData("COALESCE(t.a, RAND(), 0)", "", false)]
    [InlineData("COALESCE(RAND() + t.a, 0)", "", false)]
    [InlineData("COALESCE(CAST(RAND() AS int), 0)", "", false)]
    [InlineData("COALESCE(CONVERT(int, RAND()), 0)", "", false)]
    [InlineData("COALESCE(CONVERT(varchar(30), t.a, @p), 0)", "", false)]
    [InlineData("COALESCE(CAST(t.a AS geometry), 0)", "", false)]
    [InlineData("COALESCE(NULLIF(RAND(), 0), 0)", "", false)]
    [InlineData("COALESCE(ISNULL(RAND(), NULL), 0)", "", false)]
    [InlineData("COALESCE(COALESCE(RAND(), NULL), 0)", "", false)]
    [InlineData("COALESCE(ISNULL(t.a, RAND()), 0)", "", false)]
    [InlineData("COALESCE(COALESCE(t.a, RAND()), 0)", "", false)]
    [InlineData("COALESCE(NULLIF(t.a, RAND()), 0)", "", false)]
    [InlineData("COALESCE(MAX(RAND()), 0)", "GROUP BY t.b", false)]
    [InlineData("COALESCE(SUM(t.a) OVER (PARTITION BY t.b), 0)", "", false)]
    [InlineData("COALESCE(MAX(t.a) OVER (ORDER BY NEWID()), 0)", "", false)]
    [InlineData("COALESCE(LAG(t.a) OVER (ORDER BY t.id), 0)", "", false)]
    [InlineData("COALESCE(CAST(LAG(t.a) OVER (ORDER BY NEWID()) AS int), 0)", "", false)]
    [InlineData("COALESCE(CONVERT(int, LEAD(t.a) OVER (ORDER BY NEWID())), 0)", "", false)]
    [InlineData("COALESCE(CAST(MAX(t.a) OVER w AS int), 0)", "WINDOW w AS (ORDER BY NEWID())", false)]
    [InlineData("COALESCE(CONVERT(int, SUM(t.a) OVER w), 0)", "WINDOW w AS (ORDER BY NEWID())", false)]
    [InlineData("COALESCE(dbo.f(t.a), 0)", "", false)]
    [InlineData("COALESCE([SUM](t.a), 0)", "", false)]
    [InlineData("COALESCE([ISNULL](t.a, 0), 0)", "", false)]
    [InlineData("COALESCE(YEAR(t.d), 0)", "", false)]
    [InlineData("COALESCE(t.d AT TIME ZONE 'UTC', 0)", "", false)]
    [InlineData("COALESCE(t.a COLLATE Latin1_General_BIN, 0)", "", false)]
    [InlineData("COALESCE(@@ROWCOUNT, 0)", "", false)]
    [InlineData("COALESCE(t.x.value('(/a)[1]', 'int'), 0)", "", false)]
    public void Of_CoalesceBeforeAProvedArgument_NeedsOneValue(string item, string tail, bool holds)
    {
        var (_, _, proofs, body) = Read($"SELECT {item} AS c FROM t AS t {tail}");

        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        proof
            .Holds([.. Enumerable.Repeat(new OperandFacts(false, NotClr: true, Stored: true), proofs.Operands.Count)])
            .ShouldBe(holds, item);
    }

    // An argument after the proved one is never reached, so it may change.  One before it may not, unless it is itself
    // proved.
    [Theory]
    [InlineData("COALESCE(t.a, RAND())", true, false, true)]
    [InlineData("COALESCE(t.a, RAND(), 0)", true, false, true)]
    [InlineData("COALESCE(t.a, t.b)", false, true, false)]
    public void Of_CoalesceByPosition_ReadsOnlyTheArgumentsBefore(
        string item,
        bool firstNotNull,
        bool secondNotNull,
        bool holds
    )
    {
        var (_, _, proofs, body) = Read($"SELECT {item} AS c FROM t AS t");

        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        var facts = Enumerable
            .Range(0, proofs.Operands.Count)
            .Select(index => new OperandFacts(index == 0 ? firstNotNull : secondNotNull, NotClr: true))
            .ToList();
        proof.Holds(facts).ShouldBe(holds, item);
    }

    // GREATEST and LEAST read an argument once, so one proved argument is enough beside one that changes.
    [Theory]
    [InlineData("GREATEST(RAND(), 0)")]
    [InlineData("LEAST(t.a, 1)")]
    public void Of_GreatestOrLeast_NeedsNoOneValue(string item)
    {
        var (_, _, proofs, body) = Read($"SELECT {item} AS c FROM t AS t");

        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        proof
            .Holds([.. Enumerable.Repeat(new OperandFacts(false, NotClr: true), proofs.Operands.Count)])
            .ShouldBeTrue();
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
        first.Holds([new OperandFacts(false, true, Stored: true), new OperandFacts(true, true)]).ShouldBeTrue();
        first.Holds(Answers(false, true)).ShouldBeFalse();
    }

    // COALESCE over a column of each side of a FULL JOIN: every row has its left or its right side, so the COALESCE is
    // not null when each side's column is not null over that side's rows alone, each asked in a call of its own, and
    // the server names each as a stored column, which a side that is absent makes NULL.
    [Theory]
    [InlineData(true, true, true, true, true, true)]
    [InlineData(false, true, true, true, true, false)]
    [InlineData(true, false, true, true, true, false)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(true, true, true, false, true, false)]
    [InlineData(true, true, true, true, false, false)]
    public void Of_CoalesceOfEachSideOfAFullJoin_AsksEachSideAlone(
        bool leftNotNull,
        bool rightNotNull,
        bool firstStored,
        bool secondStored,
        bool notClr,
        bool holds
    )
    {
        var (proof, operands) = ProveOverItsRows(
            "WITH issued AS (SELECT i.id FROM billing.invoices AS i), "
                + "received AS (SELECT p.invoice_id FROM billing.payments AS p) "
                + "SELECT COALESCE(iss.id, rec.invoice_id) AS invoice_id "
                + "FROM issued AS iss FULL JOIN received AS rec ON rec.invoice_id = iss.id"
        );

        operands.ShouldBe([
            ("iss.id", OperandScope.Rows),
            ("rec.invoice_id", OperandScope.Rows),
            ("iss.id", OperandScope.LeftSide),
            ("rec.invoice_id", OperandScope.RightSide),
        ]);
        proof
            .Holds([
                new OperandFacts(false, notClr, firstStored),
                new OperandFacts(false, notClr, secondStored),
                new OperandFacts(leftNotNull, true),
                new OperandFacts(rightNotNull, true),
            ])
            .ShouldBe(holds);
    }

    // Each holds with every operand of the rows answered nullable and every side's not null, every one stored and of no
    // CLR type, and with every side's answered nullable holds no more.
    [Theory]
    [InlineData("COALESCE(a.x, b.y)", FullJoin)]
    [InlineData("COALESCE(b.y, a.x)", FullJoin)]
    [InlineData("COALESCE([a].[x], [b].[y])", FullJoin)]
    [InlineData("COALESCE(a.x, @p, b.y)", FullJoin)]
    [InlineData("COALESCE(a.x, b.y, a.z)", FullJoin)]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a AS a FULL OUTER JOIN dbo.b AS b ON b.id = a.id")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " WHERE a.z > 0 OR b.z > 0")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " JOIN dbo.c AS c ON c.id = 1")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " LEFT JOIN dbo.c AS c ON c.id = b.id")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " CROSS JOIN dbo.c AS c")]
    [InlineData("COALESCE(c.x, b.y)", "FROM dbo.a AS a JOIN dbo.c AS c ON c.id = a.id " + FullJoinOfB)]
    [InlineData(
        "COALESCE(a.x, b.y)",
        "FROM (SELECT id, x FROM dbo.a) AS a FULL JOIN (SELECT id, y FROM dbo.b) AS b ON 1 = 1"
    )]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a FULL JOIN dbo.b WITH (NOLOCK) ON b.id = a.id")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " GROUP BY COALESCE(a.x, b.y)")]
    [InlineData("SUM(COALESCE(a.x, b.y))", FullJoin + " GROUP BY ROLLUP (a.z)")]
    [InlineData("ISNULL(a.x, b.y)", FullJoin)]
    [InlineData("ISNULL(b.y, a.x)", FullJoin + " WHERE a.z > 0 OR b.z > 0")]
    [InlineData("ISNULL(c.x, b.y)", "FROM dbo.a AS a JOIN dbo.c AS c ON c.id = a.id " + FullJoinOfB)]
    [InlineData("COALESCE(ISNULL(a.x, b.y), NULL)", FullJoin)]
    public void Of_CoalesceOverAFullJoin_ProvedByEachSide(string item, string from)
    {
        var (proof, operands) = ProveOverItsRows($"SELECT {item} AS c {from}");

        proof.Holds(SideFacts(operands, sidesNotNull: true)).ShouldBeTrue();
        proof.Holds(SideFacts(operands, sidesNotNull: false)).ShouldBeFalse();
    }

    // Each must stay unproved with every operand of the rows answered nullable and every side's not null, every one
    // stored and of no CLR type.
    [Theory]
    // Both arguments of one side, or a column of no side.
    [InlineData("COALESCE(a.x, a.y)", FullJoin)]
    [InlineData("COALESCE(x, b.y)", FullJoin)]
    [InlineData("COALESCE(a.x, c.y)", FullJoin + " JOIN dbo.c AS c ON c.id = b.id")]
    [InlineData("COALESCE(dbo.a.x, b.y)", "FROM dbo.a FULL JOIN dbo.b AS b ON b.id = a.id")]
    [InlineData("COALESCE(A.x, b.y)", FullJoin)]
    [InlineData("COALESCE(a.x + 0, b.y)", FullJoin)]
    [InlineData("COALESCE(a.x, b.y + 0)", FullJoin)]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a AS a FULL JOIN dbo.b AS b ON b.id = a.id CROSS JOIN other.a")]
    // A later FULL JOIN or RIGHT JOIN, whose rows may have neither, and no FULL JOIN.
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " FULL JOIN dbo.c AS c ON c.id = b.id")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " RIGHT JOIN dbo.c AS c ON c.id = b.id")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " RIGHT OUTER JOIN dbo.c AS c ON c.id = b.id")]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a AS a LEFT JOIN dbo.b AS b ON b.id = a.id")]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a AS a, dbo.b AS b")]
    // A FROM the reader does not follow.
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a AS a FULL JOIN dbo.b AS b JOIN dbo.c AS c ON c.id = b.id ON 1 = 1")]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a AS a FULL HASH JOIN dbo.b AS b ON b.id = a.id")]
    // An argument before the pair that changes, and a grouping set's total, or GROUP BY ALL.
    [InlineData("COALESCE(a.x, RAND(), b.y)", FullJoin)]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " GROUP BY ROLLUP (COALESCE(a.x, b.y))")]
    [InlineData("COALESCE(a.x, b.y)", FullJoin + " GROUP BY ALL COALESCE(a.x, b.y)")]
    // ISNULL the same.
    [InlineData("ISNULL(a.x, a.y)", FullJoin)]
    [InlineData("ISNULL(x, b.y)", FullJoin)]
    [InlineData("ISNULL(a.x + 0, b.y)", FullJoin)]
    [InlineData("ISNULL(a.x, b.y)", FullJoin + " FULL JOIN dbo.c AS c ON c.id = b.id")]
    [InlineData("ISNULL(a.x, b.y)", FullJoin + " RIGHT JOIN dbo.c AS c ON c.id = b.id")]
    [InlineData("ISNULL(a.x, b.y)", "FROM dbo.a AS a LEFT JOIN dbo.b AS b ON b.id = a.id")]
    [InlineData("ISNULL(a.x, b.y)", FullJoin + " GROUP BY ROLLUP (ISNULL(a.x, b.y))")]
    public void Of_CoalesceOverAFullJoin_StaysUnproved(string item, string from)
    {
        var (proof, operands) = ProveOverItsRows($"SELECT {item} AS c {from}");

        proof.Holds(SideFacts(operands, sidesNotNull: true)).ShouldBeFalse();
    }

    // The left side of the last FULL JOIN is the whole chain before it, so a source an earlier FULL, LEFT or RIGHT JOIN
    // makes optional is asked over the rows that make it so, which the server answers nullable.
    [Theory]
    [InlineData("COALESCE(a.x, c.y)", FullJoin + " FULL JOIN dbo.c AS c ON c.id = b.id", "a.x")]
    [InlineData("COALESCE(l.x, b.y)", "FROM dbo.a AS a LEFT JOIN dbo.l AS l ON l.id = a.id " + FullJoinOfB, "l.x")]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.a AS a RIGHT JOIN dbo.c AS c ON c.id = a.id " + FullJoinOfB, "a.x")]
    [InlineData("COALESCE(a.x, b.y)", "FROM dbo.c AS c FULL JOIN dbo.a AS a ON a.id = c.id " + FullJoinOfB, "a.x")]
    public void Of_CoalesceOverAFullJoin_HoldsOnlyBySidesOwnAnswer(string item, string from, string left)
    {
        var (proof, operands) = ProveOverItsRows($"SELECT {item} AS c {from}");

        operands.ShouldContain((left, OperandScope.LeftSide));
        proof.Holds(SideFacts(operands, sidesNotNull: true)).ShouldBeTrue();
        proof
            .Holds([
                .. operands.Select(operand =>
                    operand.Scope == OperandScope.Rows
                        ? new OperandFacts(false, true, Stored: true)
                        : new OperandFacts(operand.Scope != OperandScope.LeftSide, true, Stored: true)
                ),
            ])
            .ShouldBeFalse();
    }

    // ISNULL reads its first argument once, so its pair needs no argument to give one value, but each must be a stored
    // column, which a side that is absent makes NULL, and each side's own answer not null.
    [Theory]
    [InlineData(true, true, true, true, true, true)]
    [InlineData(false, true, true, true, true, false)]
    [InlineData(true, false, true, true, true, false)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(true, true, true, false, true, false)]
    [InlineData(true, true, true, true, false, false)]
    public void Of_IsnullOfEachSideOfAFullJoin_AsksEachSideAlone(
        bool leftNotNull,
        bool rightNotNull,
        bool firstStored,
        bool secondStored,
        bool notClr,
        bool holds
    )
    {
        var (proof, operands) = ProveOverItsRows($"SELECT ISNULL(a.x, b.y) AS c {FullJoin}");

        operands.ShouldBe([
            ("a.x", OperandScope.Rows),
            ("b.y", OperandScope.Rows),
            ("a.x", OperandScope.LeftSide),
            ("b.y", OperandScope.RightSide),
        ]);
        proof
            .Holds([
                new OperandFacts(false, notClr, firstStored),
                new OperandFacts(false, notClr, secondStored),
                new OperandFacts(leftNotNull, true),
                new OperandFacts(rightNotNull, true),
            ])
            .ShouldBe(holds);
    }

    private const string FullJoinOfB = "FULL JOIN dbo.b AS b ON b.id = a.id";

    private const string FullJoin = "FROM dbo.a AS a " + FullJoinOfB;

    // The proof of the one item of a query, read with what its FROM and WHERE keep, and each operand's text and scope.
    private static (Proof Proof, List<(string Text, OperandScope Scope)> Operands) ProveOverItsRows(string sql)
    {
        var tokens = TsqlTokens.Read(sql);
        var statement = SelectStatement.Read(tokens).ShouldNotBeNull();
        var body = statement.Body;
        var proofs = new ExpressionProofs(tokens, body, FilteredRows.Read(tokens, body, statement.Ctes));

        var proof = proofs.Of(body.Items.ShouldHaveSingleItem().Expression);

        return (
            proof,
            [
                .. proofs.Operands.Select(
                    (range, index) => (sql[tokens[range.Start].Start..tokens[range.End - 1].End], proofs.Scopes[index])
                ),
            ]
        );
    }

    // Every operand stored and of no CLR type, those of the rows answered nullable and those of a side as given.
    private static List<OperandFacts> SideFacts(List<(string Text, OperandScope Scope)> operands, bool sidesNotNull) =>
        [
            .. operands.Select(operand => new OperandFacts(
                operand.Scope != OperandScope.Rows && sidesNotNull,
                true,
                Stored: true
            )),
        ];

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
