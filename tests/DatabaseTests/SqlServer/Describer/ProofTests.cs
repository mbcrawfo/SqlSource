using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The proofs of the SQL Server describer that a column cannot be NULL, held against a server and its rows, each query
// described in a session of its own over tables the test makes and drops.  A proved form must be nullable false with
// the nullableSource expression and give no NULL over rows chosen to tempt one: empty groups' neighbours, the totals
// of a grouping set, NULLs beside the values.  A query that tries to break a proof must stay the server's nullable
// and give at least one NULL over the same rows, which shows the server was right.  Every column is named c, which the
// count of NULL rows reads.  The facts the proofs stand on, each "design fact N" below, are numbered as in the design
// that docs/architecture/sqlserver-describer.md links, and are pinned beside them.
[Collection(UsesSqlServer.Name)]
public sealed class ProofTests(SqlServerDatabases sqlServer)
{
    // GREATEST, LEAST, JSON_OBJECT, JSON_ARRAY and the WINDOW clause came with SQL Server 2022.
    private const int NewFunctions = 16;

    private const string NewFunctionsReason =
        "GREATEST, LEAST, JSON_OBJECT, JSON_ARRAY and WINDOW are new in SQL Server 2022";

    // A UNION ALL of proof_shapes' solid, a geometry never NULL, and its wkt, a string, which geometry parses as its
    // Null where it is 'NULL' or 'Null'.
    private const string ShapesMix =
        "(SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT wkt FROM dbo.proof_shapes)";

    // NULL or 1 at random, each time it is evaluated.
    private const string Flip = "CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END";

    // NULL or the row's date at random, each time it is evaluated.
    private const string FlipDate = "CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN d END";

    // One query of each proved form, with the major it needs.  The arithmetic, GREATEST and LEAST are proved because
    // a is not null in every row, which "operands" asks the server; b and g are NULL in some.  No divisor is ever 0,
    // since a division by 0 is an error under ANSI_WARNINGS on, and not a NULL.
    public static TheoryData<int, string> ProvedForms =>
        new()
        {
            { 0, "SELECT COUNT(b) AS c FROM dbo.proof_rows WHERE 1 = 0" },
            { 0, "SELECT COUNT(b) OVER (PARTITION BY g) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COUNT_BIG(b) AS c FROM dbo.proof_rows GROUP BY g" },
            {
                0,
                "SELECT (SELECT COUNT(*) FROM dbo.proof_other AS o WHERE o.rows_id = r.id) AS c "
                    + "FROM dbo.proof_rows AS r"
            },
            { 0, "SELECT MAX(a) AS c FROM dbo.proof_rows GROUP BY g" },
            { 0, "SELECT SUM(a) AS c FROM dbo.proof_rows GROUP BY ROLLUP (g)" },
            { 0, "SELECT MIN(a) AS c FROM dbo.proof_rows GROUP BY CUBE (g, b)" },
            { 0, "SELECT MAX(a) AS c FROM dbo.proof_rows GROUP BY GROUPING SETS ((g), ())" },
            {
                0,
                "SELECT SUM(a) OVER (PARTITION BY g ORDER BY id ROWS BETWEEN 1 PRECEDING AND CURRENT ROW) AS c "
                    + "FROM dbo.proof_rows"
            },
            { 0, "SELECT MAX(a) OVER (PARTITION BY g) AS c FROM dbo.proof_rows" },
            { 0, "SELECT SUM(a) OVER (PARTITION BY g ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT ROW_NUMBER() OVER (ORDER BY b) AS c FROM dbo.proof_rows" },
            { 0, "SELECT RANK() OVER (PARTITION BY g ORDER BY b) AS c FROM dbo.proof_rows" },
            { 0, "SELECT DENSE_RANK() OVER (PARTITION BY g ORDER BY b) AS c FROM dbo.proof_rows" },
            { 0, "SELECT NTILE(2) OVER (ORDER BY b) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(b, a) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(g, 0) AS c FROM dbo.proof_rows GROUP BY ROLLUP (g)" },
            // COALESCE over arguments that each give one value in a row, and over one that changes after the proved
            // argument, which is never reached.
            { 0, "SELECT COALESCE(b, g, 0) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(b * 2 + 1, 0) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(CONVERT(bigint, b), 0) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(NULLIF(b, 5), 0) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(ISNULL(b, g), 0) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(COALESCE(b, g), 0) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(MAX(b), 0) AS c FROM dbo.proof_rows GROUP BY g" },
            { 0, "SELECT COALESCE(SUM(b), 0) AS c FROM dbo.proof_rows WHERE 1 = 0" },
            { 0, "SELECT COALESCE(CAST(AVG(b) AS int), 0) AS c FROM dbo.proof_rows GROUP BY g" },
            { 0, "SELECT COALESCE(q.b, 0) AS c FROM (SELECT b FROM dbo.proof_rows) AS q" },
            { 0, $"SELECT COALESCE(a, {Flip}) AS c FROM dbo.proof_rows" },
            { 0, "SELECT CAST(a AS bigint) AS c FROM dbo.proof_rows" },
            { 0, "SELECT CONVERT(bigint, a) AS c FROM dbo.proof_rows" },
            { 0, "SELECT CONVERT(varchar(30), CAST(a AS money), 1) AS c FROM dbo.proof_rows" },
            { 0, "SELECT a * 2 - a % 7 + 1 AS c FROM dbo.proof_rows" },
            { 0, "SELECT a / id - a % (id + 1) AS c FROM dbo.proof_rows" },
            { NewFunctions, "SELECT JSON_OBJECT('a': b) AS c FROM dbo.proof_rows" },
            { NewFunctions, "SELECT JSON_ARRAY(b) AS c FROM dbo.proof_rows" },
            { NewFunctions, "SELECT GREATEST(b, a) AS c FROM dbo.proof_rows" },
            { NewFunctions, "SELECT LEAST(g, b, a) AS c FROM dbo.proof_rows" },
            // Conversions to built-in types that are no CLR type, sysname an alias of nvarchar(128) among them.
            { 0, "SELECT CAST(a AS sysname) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(CAST(b AS nvarchar(20)), N'none') AS c FROM dbo.proof_rows" },
            // The date functions and AT TIME ZONE, of operands that are not null, along a chain too.
            { 0, "SELECT DATEADD(DAY, months, at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATEDIFF(DAY, at_utc, COALESCE(maybe, at_utc)) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATEDIFF_BIG(SECOND, at_utc, '2030-01-01') AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATEPART(QUARTER, at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATENAME(WEEKDAY, at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT YEAR(at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT MONTH(at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DAY(at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT EOMONTH(at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT EOMONTH(at_utc, months) AS c FROM dbo.proof_dates" },
            { 0, "SELECT EOMONTH(COALESCE(maybe, at_utc)) AS c FROM dbo.proof_dates" },
            { 0, "SELECT at_utc AT TIME ZONE 'UTC' AS c FROM dbo.proof_dates" },
            { 0, "SELECT at_utc AT TIME ZONE 'UTC' AT TIME ZONE zone AS c FROM dbo.proof_dates" },
            { 0, "SELECT at_utc AT TIME ZONE COALESCE(zone_or_null, 'UTC') AS c FROM dbo.proof_dates" },
            // LAG and LEAD with a default: the rows at the ends of a partition take it.
            { 0, "SELECT LAG(a, 1, 0) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LAG(a, 0, 0) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LEAD(a, 2, id) OVER (PARTITION BY g ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LAG(SUM(a), 1, 0) OVER (ORDER BY g) AS c FROM dbo.proof_rows GROUP BY g" },
            // A CASE that replaces the NULL of a stored column, through a derived table and an outer join too, and in
            // the total of a ROLLUP, whose NULL it replaces.
            { 0, "SELECT CASE WHEN b IS NULL THEN 0 ELSE b END AS c FROM dbo.proof_rows" },
            { 0, "SELECT CASE WHEN b IS NULL THEN a ELSE b END AS c FROM dbo.proof_rows" },
            { 0, "SELECT CASE WHEN q.b IS NULL THEN 0 ELSE q.b END AS c FROM (SELECT b FROM dbo.proof_rows) AS q" },
            {
                0,
                "SELECT CASE WHEN o.rows_id IS NULL THEN 0 ELSE o.rows_id END AS c FROM dbo.proof_rows AS r "
                    + "LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id"
            },
            { 0, "SELECT CASE WHEN g IS NULL THEN -1 ELSE g END AS c FROM dbo.proof_rows GROUP BY ROLLUP (g)" },
            // A conjunct of the WHERE that rejects NULL, of the column itself or of an expression over it.
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b IS NOT NULL" },
            { 0, "SELECT r.b AS c FROM dbo.proof_rows AS r WHERE r.b = 5" },
            { 0, "SELECT [b] AS c FROM dbo.proof_rows WHERE b > 0" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE 5 = b" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b <> 5" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b != 5" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b !< 1" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b = g + 4" },
            { 0, "SELECT zone_or_null AS c FROM dbo.proof_dates WHERE zone_or_null LIKE N'U%'" },
            { 0, "SELECT zone_or_null AS c FROM dbo.proof_dates WHERE zone_or_null NOT LIKE N'X%'" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE a >= 0 AND (b > 0)" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE id BETWEEN 1 AND 5 AND b > 0" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE (a > 100 OR id > 0) AND b > 0" },
            { 0, "SELECT b + 1 AS c FROM dbo.proof_rows WHERE b > 0" },
            { 0, "SELECT CAST(b AS bigint) AS c FROM dbo.proof_rows WHERE b IS NOT NULL" },
            { 0, "SELECT COALESCE(b, g) AS c FROM dbo.proof_rows WHERE b > 0" },
            { 0, "SELECT SUM(b) AS c FROM dbo.proof_rows WHERE b > 0 GROUP BY g" },
            { 0, "SELECT MAX(b) AS c FROM dbo.proof_rows WHERE b > 0 GROUP BY ROLLUP (g)" },
            { 0, "SELECT g AS c FROM dbo.proof_rows WHERE g > 0 GROUP BY g" },
            { 0, "SELECT DISTINCT b AS c FROM dbo.proof_rows WHERE b > 0" },
            { 0, "SELECT TOP (1) b AS c FROM dbo.proof_rows WHERE b > 0 ORDER BY b" },
            // The optional side of a LEFT JOIN that such a conjunct makes inner, a NOT NULL column of it included.
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE o.rows_id > 0"
            },
            {
                0,
                "SELECT o.rows_id AS c FROM dbo.proof_rows AS r LEFT OUTER JOIN dbo.proof_other AS o "
                    + "ON o.rows_id = r.id WHERE o.rows_id IS NOT NULL"
            },
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "JOIN dbo.proof_computed AS p ON p.id = r.id WHERE o.rows_id = 1"
            },
            {
                0,
                "SELECT q.id AS c FROM dbo.proof_rows AS r "
                    + "LEFT JOIN (SELECT id, rows_id FROM dbo.proof_other) AS q ON q.rows_id = r.id WHERE q.rows_id > 0"
            },
            // An alias that is also the name of a CLR column, with a real column of the joined table in the filter.
            {
                0,
                "SELECT solid.id AS c FROM dbo.proof_shapes AS p LEFT JOIN dbo.proof_other AS solid "
                    + "ON solid.id = p.id WHERE solid.id > 0"
            },
            // A set operator whose every branch proves the column, each over its own rows.
            { 0, "SELECT a AS c FROM dbo.proof_rows UNION SELECT b FROM dbo.proof_rows WHERE b > 0" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 UNION ALL SELECT a FROM dbo.proof_rows" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 EXCEPT SELECT a FROM dbo.proof_rows" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 INTERSECT SELECT b FROM dbo.proof_rows WHERE b = 5" },
            { 0, "SELECT COUNT(*) AS c FROM dbo.proof_rows UNION ALL SELECT COUNT(b) FROM dbo.proof_rows" },
            { 0, "(SELECT a AS c FROM dbo.proof_rows) UNION ALL (SELECT b FROM dbo.proof_rows WHERE b > 0)" },
            { 0, "SELECT a + 1 AS c FROM dbo.proof_rows UNION ALL SELECT COALESCE(b, 0) FROM dbo.proof_rows" },
        };

    // The queries that try to break a proof, with the major they need, each NULL in some row: the list of that design's
    // Tests section, then the same forms wrapped or written another way.  Under ROLLUP the grouping column is NULL in
    // the total's row even where the table holds none: the rows of a, which the table never makes NULL, show it, and
    // those of g, NULL in the table too, do not.  The scalar subqueries of COUNT that a set operator, an OFFSET, a TOP
    // or a grouping can leave with no row are inside arithmetic, so that the item is not one leaf alone, which would
    // ask nothing.
    public static TheoryData<int, string> BreakingAttempts =>
        new()
        {
            { 0, "SELECT MAX(a) AS c FROM dbo.proof_rows WHERE 1 = 0" },
            { 0, "SELECT MAX(a) AS c FROM dbo.proof_rows WHERE g = 1 GROUP BY ALL g" },
            {
                0,
                "SELECT SUM(o.id) AS c FROM dbo.proof_rows AS r "
                    + "LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id GROUP BY r.id"
            },
            {
                0,
                "SELECT (SELECT COUNT(*) FROM dbo.proof_other GROUP BY rows_id HAVING COUNT(*) > 5) AS c "
                    + "FROM dbo.proof_rows"
            },
            { 0, "SELECT SUM(a) OVER (ORDER BY id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(g, b) AS c FROM dbo.proof_rows GROUP BY ROLLUP (g, b)" },
            { 0, "SELECT COALESCE(a, b) AS c FROM dbo.proof_rows GROUP BY ROLLUP (a, b)" },
            { 0, "SELECT CAST(g AS bigint) AS c FROM dbo.proof_rows GROUP BY ROLLUP (g)" },
            { 0, "SELECT CAST(a AS bigint) AS c FROM dbo.proof_rows GROUP BY ROLLUP (a)" },
            { 0, "SELECT CONVERT(varchar(30), SYSDATETIME(), @style) AS c FROM dbo.proof_rows" },
            { 0, "SELECT TRY_CAST(N'x' + CAST(a AS nvarchar(10)) AS int) AS c FROM dbo.proof_rows" },
            { 0, "SELECT COALESCE(b, NULL) AS c FROM dbo.proof_rows" },
            { 0, "SELECT dbo.COUNT(a) AS c FROM dbo.proof_rows" },
            { 0, "SELECT [dbo].[COUNT](a) AS c FROM dbo.proof_rows" },
            {
                NewFunctions,
                "SELECT (SELECT TOP (1) JSON_OBJECT('a': a) FROM dbo.proof_rows WHERE 1 = 0) AS c FROM dbo.proof_rows"
            },
            { 0, "SELECT a + b AS c FROM dbo.proof_rows" },
            { NewFunctions, "SELECT GREATEST(b, NULL) AS c FROM dbo.proof_rows" },
            { 0, "SELECT CAST(a AS bigint) AS c FROM dbo.proof_rows GROUP BY a WITH ROLLUP" },
            { 0, "SELECT CAST(MAX(a) AS bigint) AS c FROM dbo.proof_rows WHERE 1 = 0" },
            { 0, "SELECT -MAX(a) + 1 AS c FROM dbo.proof_rows WHERE 1 = 0" },
            { 0, "SELECT MAX(a) OVER (ORDER BY id ROWS BETWEEN 2 PRECEDING AND 1 PRECEDING) AS c FROM dbo.proof_rows" },
            { 0, "SELECT (SELECT COUNT(*) FROM dbo.proof_other EXCEPT SELECT 4) + 0 AS c FROM dbo.proof_rows" },
            {
                0,
                "SELECT (SELECT COUNT(*) FROM dbo.proof_other ORDER BY 1 OFFSET 1 ROWS) + 0 AS c FROM dbo.proof_rows"
            },
            { 0, "SELECT (SELECT TOP (0) COUNT(*) FROM dbo.proof_other) + 0 AS c FROM dbo.proof_rows" },
            { 0, "SELECT (SELECT COUNT(*) FROM dbo.proof_other WHERE 1 = 0 GROUP BY ()) + 0 AS c FROM dbo.proof_rows" },
            { 0, "SELECT (SELECT MAX(a) FROM dbo.proof_rows WHERE 1 = 0) + 1 AS c FROM dbo.proof_rows" },
            { 0, "SELECT CAST(x.a AS bigint) AS c FROM (SELECT a FROM dbo.proof_rows GROUP BY ROLLUP (a)) AS x" },
            { 0, "SELECT CAST(NULLIF(a, 10) AS bigint) AS c FROM dbo.proof_rows" },
            { 0, "SELECT CAST(CASE WHEN a > 10 THEN a END AS bigint) AS c FROM dbo.proof_rows" },
            {
                NewFunctions,
                "SELECT SUM(a) OVER w AS c FROM dbo.proof_rows "
                    + "WINDOW w AS (ORDER BY id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)"
            },
            {
                NewFunctions,
                "SELECT SUM(a) OVER (w ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING) AS c FROM dbo.proof_rows "
                    + "WINDOW w AS (ORDER BY id)"
            },
            // WINDOW and OFFSET are no reserved words: an alias or a column so named before an outer join or a
            // ROLLUP must not cut the FROM or the GROUP BY short.
            {
                0,
                "SELECT a + 1 AS c FROM dbo.proof_rows window "
                    + "RIGHT JOIN dbo.proof_other AS o ON o.rows_id = a / 10"
            },
            {
                0,
                "SELECT CAST(a AS bigint) AS c FROM (SELECT a, b, g AS window FROM dbo.proof_rows) AS r "
                    + "GROUP BY b, window, ROLLUP (a)"
            },
            {
                0,
                "SELECT a + 1 AS c FROM dbo.proof_rows offset "
                    + "RIGHT JOIN dbo.proof_other AS o ON o.rows_id = a / 10"
            },
            // A CLR type's Parse answers its Null for the string 'NULL', whatever its case, so a conversion to
            // geometry or geography of a value that is not null, and a COALESCE over a geometry, whose literal is
            // converted to it, can be NULL.
            { 0, "SELECT CAST(wkt AS geometry) AS c FROM dbo.proof_shapes" },
            { 0, "SELECT CONVERT(geography, wkt) AS c FROM dbo.proof_shapes" },
            { 0, "SELECT CAST(N'NULL' AS geometry) AS c FROM dbo.proof_shapes" },
            { 0, "SELECT COALESCE(shape, 'NULL') AS c FROM dbo.proof_shapes" },
            { 0, "SELECT COALESCE(CONVERT(geometry, wkt), 'NULL') AS c FROM dbo.proof_shapes" },
            { 0, "SELECT CAST(COALESCE(shape, 'NULL') AS varbinary(max)) AS c FROM dbo.proof_shapes" },
            // LAG converts its default to its value's type, and a CASE both branches to the higher one.
            {
                0,
                "SELECT CAST(LAG(solid, 1, 'NULL') OVER (ORDER BY id) AS varbinary(max)) AS c FROM dbo.proof_shapes"
            },
            {
                0,
                "SELECT CAST(CASE WHEN wkt IS NULL THEN solid ELSE wkt END AS varbinary(max)) AS c "
                    + "FROM dbo.proof_shapes"
            },
            // A date function or AT TIME ZONE of an operand that can be NULL, a grouping set's total included.
            { 0, "SELECT DATEADD(DAY, 1, maybe) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATEADD(DAY, months_or_null, at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATEDIFF(DAY, at_utc, maybe) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATEDIFF_BIG(DAY, maybe, at_utc) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATEPART(DAY, maybe) AS c FROM dbo.proof_dates" },
            { 0, "SELECT DATENAME(DAY, maybe) AS c FROM dbo.proof_dates" },
            { 0, "SELECT YEAR(maybe) AS c FROM dbo.proof_dates" },
            { 0, "SELECT EOMONTH(maybe) AS c FROM dbo.proof_dates" },
            { 0, "SELECT EOMONTH(maybe, 1) AS c FROM dbo.proof_dates" },
            { 0, "SELECT YEAR(at_utc) AS c FROM dbo.proof_dates GROUP BY ROLLUP (at_utc)" },
            { 0, "SELECT maybe AT TIME ZONE 'UTC' AS c FROM dbo.proof_dates" },
            { 0, "SELECT at_utc AT TIME ZONE zone_or_null AS c FROM dbo.proof_dates" },
            { 0, "SELECT at_utc AT TIME ZONE 'UTC' AT TIME ZONE zone_or_null AS c FROM dbo.proof_dates" },
            { 0, "SELECT at_utc AT TIME ZONE 'UTC' AS c FROM dbo.proof_dates GROUP BY ROLLUP (at_utc)" },
            // LAG and LEAD with no default, or one that can be NULL, or of a value that can be.
            { 0, "SELECT LAG(a) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LEAD(a, 1) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LAG(a, 1, NULL) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LAG(a, 1, b) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LAG(a, 1, @style) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LAG(b, 1, 0) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LEAD(b, 1, a) OVER (ORDER BY id) AS c FROM dbo.proof_rows" },
            { 0, "SELECT LAG(a, 1, 0) OVER (ORDER BY a) AS c FROM dbo.proof_rows GROUP BY ROLLUP (a)" },
            // One token from the CASE that replaces a NULL.
            { 0, "SELECT CASE WHEN b IS NULL THEN NULL ELSE b END AS c FROM dbo.proof_rows" },
            { 0, "SELECT CASE WHEN b IS NULL THEN g ELSE b END AS c FROM dbo.proof_rows" },
            { 0, "SELECT CASE WHEN a IS NULL THEN 0 ELSE b END AS c FROM dbo.proof_rows" },
            { 0, "SELECT CASE WHEN b IS NOT NULL THEN 0 ELSE b END AS c FROM dbo.proof_rows" },
            { 0, "SELECT CASE WHEN b IS NULL THEN 0 WHEN g = 1 THEN NULL ELSE b END AS c FROM dbo.proof_rows" },
            { 0, "SELECT CASE WHEN g IS NULL THEN b ELSE g END AS c FROM dbo.proof_rows GROUP BY ROLLUP (g, b)" },
            // A WHERE that lets a NULL through: no conjunct, or one that does not reject it.
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b IS NULL" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 OR a > 0" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE a > 0 OR b > 0" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 AND a > 0 OR g = 1" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE NOT (b IS NOT NULL)" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE g > 0" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > ALL (SELECT id FROM dbo.proof_other WHERE 1 = 0)" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE ISNULL(b, 1) = 1" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE COALESCE(b, 0) = 0" },
            {
                0,
                "SELECT o.rows_id AS c FROM dbo.proof_other AS o JOIN dbo.proof_other AS p ON p.id = o.id - 1 "
                    + "WHERE p.rows_id > 0"
            },
            // A grouping set's total, and GROUP BY ALL's group that no row of the WHERE holds.
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 GROUP BY ROLLUP (b)" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 GROUP BY CUBE (b)" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 GROUP BY GROUPING SETS ((b), ())" },
            { 0, "SELECT CAST(b AS bigint) AS c FROM dbo.proof_rows WHERE b > 0 GROUP BY ROLLUP (b)" },
            { 0, "SELECT g AS c FROM dbo.proof_rows WHERE g > 1 GROUP BY ALL g" },
            // A join the WHERE does not make inner, or one the reader cannot follow.
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE r.a >= 0"
            },
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE o.rows_id IS NULL"
            },
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE o.rows_id > 0 OR r.a >= 0"
            },
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE ISNULL(o.rows_id, 1) = 1"
            },
            {
                0,
                "SELECT r.a AS c FROM dbo.proof_rows AS r RIGHT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE o.id > 0"
            },
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r FULL JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE r.a >= 0"
            },
            {
                0,
                "SELECT p.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "LEFT JOIN dbo.proof_computed AS p ON p.id = o.id WHERE o.rows_id > 0"
            },
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_other AS o "
                    + "JOIN dbo.proof_computed AS p ON p.id = o.id ON o.rows_id = r.id WHERE r.a >= 0"
            },
            {
                0,
                "SELECT o.id AS c FROM dbo.proof_rows AS r LEFT HASH JOIN dbo.proof_other AS o ON o.rows_id = r.id "
                    + "WHERE r.a >= 0"
            },
            {
                0,
                "SELECT q.x AS c FROM dbo.proof_rows AS r OUTER APPLY "
                    + "(SELECT TOP (1) o.id AS x FROM dbo.proof_other AS o WHERE o.rows_id = r.id) AS q WHERE r.a >= 0"
            },
            // A name under a join's alias that SQL Server reads as a property of a CLR column of another source: the
            // filter holds of the rows with no match, so it does not make the join inner.
            {
                0,
                "SELECT solid.id AS c FROM dbo.proof_shapes AS p LEFT JOIN dbo.proof_other AS solid "
                    + "ON solid.id = p.id + 100 WHERE solid.STSrid = 0"
            },
            {
                0,
                "SELECT solid.id AS c FROM dbo.proof_shapes AS p LEFT JOIN dbo.proof_other AS solid "
                    + "ON solid.id = p.id + 100 WHERE solid.STX >= 0"
            },
            {
                0,
                "SELECT solid.id AS c FROM dbo.proof_shapes AS p LEFT JOIN dbo.proof_other AS solid "
                    + "ON solid.id = p.id + 100 WHERE solid.STSrid IS NOT NULL"
            },
            {
                0,
                "SELECT solid.id AS c FROM dbo.proof_shapes AS p LEFT JOIN dbo.proof_other AS solid "
                    + "ON solid.id = p.id + 100 WHERE [solid].[STSrid] = 0"
            },
            {
                0,
                "SELECT solid.id + 1 AS c FROM dbo.proof_shapes AS p LEFT JOIN dbo.proof_other AS solid "
                    + "ON solid.id = p.id + 100 WHERE solid.STSrid = 0"
            },
            {
                0,
                "SELECT solid.id AS c FROM dbo.proof_shapes AS p LEFT JOIN dbo.proof_other AS solid "
                    + "ON solid.id = p.id + 100 WHERE solid.STSrid = 0 UNION ALL SELECT a FROM dbo.proof_rows"
            },
            // A name of three parts names a table's column, here of the preserved side, and no column of the alias.
            {
                0,
                "SELECT dbo.id AS c FROM dbo.proof_rows LEFT JOIN dbo.proof_other AS dbo "
                    + "ON dbo.id = proof_rows.id + 100 WHERE dbo.proof_rows.a > 0"
            },
            // A set operator with a branch that can be NULL, wherever it stands.  A CLR type in one branch is the
            // column's, and the string of another converts to its Null.
            { 0, "SELECT a AS c FROM dbo.proof_rows UNION ALL SELECT b FROM dbo.proof_rows" },
            { 0, "SELECT b AS c FROM dbo.proof_rows WHERE b > 0 UNION ALL SELECT b FROM dbo.proof_rows WHERE g > 0" },
            { 0, "SELECT a AS c FROM dbo.proof_rows UNION SELECT NULL" },
            { 0, "SELECT NULL AS c UNION SELECT a FROM dbo.proof_rows" },
            { 0, "SELECT b AS c FROM dbo.proof_rows EXCEPT SELECT a FROM dbo.proof_rows" },
            { 0, "SELECT b AS c FROM dbo.proof_rows INTERSECT SELECT b FROM dbo.proof_rows" },
            { 0, "SELECT COUNT(*) AS c FROM dbo.proof_rows UNION ALL SELECT MAX(a) FROM dbo.proof_rows WHERE 1 = 0" },
            { 0, "SELECT shape AS c FROM dbo.proof_shapes WHERE shape IS NOT NULL UNION ALL SELECT 'NULL'" },
            // A proved first branch, then one that can be NULL, each kind of proof reading its own branch's answers.
            { 0, "SELECT a + 1 AS c FROM dbo.proof_rows UNION ALL SELECT COALESCE(g, b) FROM dbo.proof_rows" },
            { 0, "SELECT a + id AS c FROM dbo.proof_rows UNION ALL SELECT g + b FROM dbo.proof_rows" },
            {
                0,
                "SELECT a AS c FROM dbo.proof_rows UNION ALL SELECT solid.id FROM dbo.proof_shapes AS p "
                    + "LEFT JOIN dbo.proof_other AS solid ON solid.id = p.id + 100 WHERE solid.STSrid = 0"
            },
            {
                0,
                "SELECT CAST(a AS varbinary(max)) AS c FROM dbo.proof_rows "
                    + "UNION ALL SELECT CAST(COALESCE(shape, 'NULL') AS varbinary(max)) FROM dbo.proof_shapes"
            },
        };

    // A form that reads a value twice proves nothing unless the value gives one value in a row.  Each of these can
    // differ between its two reads, though no plan measured from 2017 to 2025 evaluates the computed column, the view's
    // or the derived table's twice, and the window ordered by NEWID() gives a NULL only in some rows.  So the describe
    // alone is held; ValuesThatChange runs the shapes that do break.
    public static TheoryData<string> ValuesReadTwice =>
        [
            "SELECT COALESCE(flip, 0) AS c FROM dbo.proof_volatile",
            "SELECT COALESCE(flip, 0) AS c FROM dbo.proof_volatile_view",
            $"SELECT COALESCE(q.flip, 0) AS c FROM (SELECT {Flip} AS flip FROM dbo.proof_rows) AS q",
            $"WITH q AS (SELECT {Flip} AS flip FROM dbo.proof_rows) SELECT COALESCE(flip, 0) AS c FROM q",
            $"SELECT COALESCE(MAX({Flip}), 0) AS c FROM dbo.proof_rows GROUP BY id",
            // The server names no source column for a column that a set operator or an UNPIVOT combines, or that a
            // derived table computes, so none is shown to be stored.
            "SELECT COALESCE(q.x, 0) AS c FROM (SELECT b AS x FROM dbo.proof_rows UNION ALL "
                + "SELECT g FROM dbo.proof_rows) AS q",
            "SELECT COALESCE(q.x, 0) AS c FROM (SELECT b AS x FROM dbo.proof_rows UNION "
                + "SELECT g FROM dbo.proof_rows) AS q",
            "WITH q AS (SELECT b AS x FROM dbo.proof_rows UNION ALL SELECT g FROM dbo.proof_rows) "
                + "SELECT COALESCE(q.x, 0) AS c FROM q",
            "SELECT COALESCE(u.x, 0) AS c FROM (SELECT id, b, g FROM dbo.proof_rows) AS p "
                + "UNPIVOT (x FOR w IN (b, g)) AS u",
            "SELECT COALESCE(q.total, 0) AS c FROM (SELECT SUM(b) AS total FROM dbo.proof_rows GROUP BY g) AS q",
            "SELECT COALESCE(q.x, 0) AS c FROM (SELECT b * 2 AS x FROM dbo.proof_rows) AS q",
            "SELECT CASE WHEN flip IS NULL THEN 0 ELSE flip END AS c FROM dbo.proof_volatile",
            "SELECT CASE WHEN flip IS NULL THEN 0 ELSE flip END AS c FROM dbo.proof_volatile_view",
            "SELECT CASE WHEN q.flip IS NULL THEN 0 ELSE q.flip END AS c "
                + "FROM (SELECT CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END AS flip FROM dbo.proof_rows) AS q",
            "WITH q AS (SELECT CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END AS flip FROM dbo.proof_rows) "
                + "SELECT CASE WHEN flip IS NULL THEN 0 ELSE flip END AS c FROM q",
            "SELECT CASE WHEN q.flip IS NULL THEN 0 ELSE q.flip END AS c FROM dbo.proof_rows "
                + "CROSS APPLY (SELECT CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END AS flip) AS q",
            "SELECT COALESCE(LAG(b) OVER (ORDER BY NEWID()), 0) AS c FROM dbo.proof_rows",
            "SELECT COALESCE(LEAD(b) OVER (ORDER BY NEWID()), 0) AS c FROM dbo.proof_rows",
            "SELECT COALESCE(LAG(b) OVER (ORDER BY NEWID()) + 1, 0) AS c FROM dbo.proof_rows",
            "SELECT COALESCE(LAG(b, 1, 5) OVER (ORDER BY NEWID()), 0) AS c FROM dbo.proof_rows",
            "SELECT COALESCE(YEAR(LAG(maybe) OVER (ORDER BY NEWID())), 0) AS c FROM dbo.proof_dates",
            "SELECT COALESCE(DATEADD(DAY, 1, LAG(maybe) OVER (ORDER BY NEWID())), at_utc) AS c FROM dbo.proof_dates",
            "SELECT COALESCE(LAG(maybe) OVER (ORDER BY NEWID()) AT TIME ZONE 'UTC', SYSDATETIMEOFFSET()) AS c "
                + "FROM dbo.proof_dates",
            // A filter tests a value and the result reads it again, so it proves only a stored column.
            "SELECT flip AS c FROM dbo.proof_volatile WHERE flip IS NOT NULL",
            "SELECT flip AS c FROM dbo.proof_volatile_view WHERE flip = 1",
            $"SELECT q.flip AS c FROM (SELECT {Flip} AS flip FROM dbo.proof_rows) AS q WHERE q.flip IS NOT NULL",
            $"WITH q AS (SELECT {Flip} AS flip FROM dbo.proof_rows) SELECT flip AS c FROM q WHERE flip > 0",
            $"WITH q AS (SELECT flip FROM (SELECT {Flip} AS flip FROM dbo.proof_rows) AS i WHERE flip = 1) "
                + "SELECT flip AS c FROM q",
            "SELECT flip AS c FROM dbo.proof_volatile WHERE flip IS NOT NULL UNION ALL SELECT a FROM dbo.proof_rows",
        ];

    // The queries in which SQL Server evaluates a value twice in one row and returns NULL, measured from 2017 to 2025:
    // COALESCE over a value that changes, however it is wrapped, and the other forms that expand to a CASE.  About a
    // quarter of the 2,000 rows of dbo.proof_many are NULL, so that a run with none is out of the question.
    public static TheoryData<string> ValuesThatChange =>
        [
            $"SELECT COALESCE({Flip}, 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE({Flip}, {Flip}, 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(b, {Flip}, 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(CAST({Flip} AS int), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(CONVERT(int, {Flip}), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE({Flip} + 1, 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(NULLIF({Flip}, 5), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(ISNULL({Flip}, NULL), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(COALESCE({Flip}, NULL), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(ISNULL(b, {Flip}), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(COALESCE(b, {Flip}), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(NULLIF(1, {Flip}), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(IIF({Flip} IS NULL, NULL, 1), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE((SELECT TOP (1) {Flip} FROM dbo.proof_many AS o WHERE o.id = r.id), 0) AS c "
                + "FROM dbo.proof_many AS r",
            "SELECT COALESCE(CAST(LAG(b) OVER (ORDER BY NEWID()) AS int), 0) AS c FROM dbo.proof_many",
            "SELECT COALESCE(CONVERT(int, LEAD(b) OVER (ORDER BY NEWID())), 0) AS c FROM dbo.proof_many",
            "SELECT COALESCE(CAST(YEAR(LAG(d) OVER (ORDER BY NEWID())) AS int), 0) AS c FROM dbo.proof_many",
            "SELECT COALESCE(CONVERT(date, DATEADD(DAY, 1, LEAD(d) OVER (ORDER BY NEWID()))), '2020-01-01') AS c "
                + "FROM dbo.proof_many",
            "SELECT COALESCE(CAST(LAG(d) OVER (ORDER BY NEWID()) AT TIME ZONE 'UTC' AS datetime2(0)), '2020-01-01') "
                + "AS c FROM dbo.proof_many",
            $"SELECT COALESCE(CAST(YEAR({FlipDate}) AS int), 0) AS c FROM dbo.proof_many",
            $"SELECT COALESCE(CONVERT(date, DATEADD(DAY, 1, {FlipDate})), '2020-01-01') AS c FROM dbo.proof_many",
            $"SELECT COALESCE(CAST({FlipDate} AT TIME ZONE 'UTC' AS datetime2(0)), '2020-01-01') AS c "
                + "FROM dbo.proof_many",
            "SELECT COALESCE(SUM(b) OVER (ORDER BY NEWID() ROWS BETWEEN 1 FOLLOWING AND 1 FOLLOWING), 0) AS c "
                + "FROM dbo.proof_many",
            "SELECT COALESCE(MAX(b) OVER (ORDER BY NEWID() ROWS BETWEEN 1 FOLLOWING AND 1 FOLLOWING), 0) AS c "
                + "FROM dbo.proof_many",
            $"SELECT COALESCE(SUM({Flip}) OVER (PARTITION BY id), 0) AS c FROM dbo.proof_many",
            "SELECT COALESCE(SUM(dbo.proof_flip(id)) OVER (PARTITION BY id), 0) AS c FROM dbo.proof_many",
            "SELECT CHOOSE(ABS(CHECKSUM(NEWID())) % 2 + 1, 1, 2) AS c FROM dbo.proof_many",
            $"SELECT CASE WHEN {Flip} IS NULL THEN 0 ELSE {Flip} END AS c FROM dbo.proof_many",
            $"SELECT IIF({Flip} IS NULL, 0, {Flip}) AS c FROM dbo.proof_many",
        ];

    [Theory]
    [MemberData(nameof(ValuesThatChange))]
    public Task DescribeAsync_ValueThatChanges_NullableAndNullRow(string sql) =>
        WithManyRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(sql)).ShouldBeGreaterThan(0, sql);
        });

    // An aggregate over a named window is no aggregate of its group, and a CAST or CONVERT around it hides it from the
    // type proof, so only the walk of what gives one value keeps these nullable.  WINDOW came with SQL Server 2022.
    [Theory]
    [InlineData("COALESCE(CAST(MAX(b) OVER w AS int), 0)")]
    [InlineData("COALESCE(CONVERT(int, SUM(b) OVER w), 0)")]
    [InlineData("COALESCE(CAST(MIN(b) OVER w AS bigint) + 1, 0)")]
    [InlineData("COALESCE(CAST(AVG(b) OVER w AS int), 0)")]
    public async Task DescribeAsync_NamedWindowThatChanges_NullableAndNullRow(string item)
    {
        sqlServer.SkipBelow(NewFunctions, NewFunctionsReason);
        var sql =
            $"SELECT {item} AS c FROM dbo.proof_many "
            + "WINDOW w AS (ORDER BY NEWID() ROWS BETWEEN 1 FOLLOWING AND 1 FOLLOWING)";
        await WithManyRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(sql)).ShouldBeGreaterThan(0, sql);
        });
    }

    // GREATEST and LEAST evaluate an argument once, so one proved argument holds beside a value that changes.
    [Theory]
    [InlineData($"SELECT GREATEST({Flip}, 0) AS c FROM dbo.proof_many")]
    [InlineData($"SELECT LEAST({Flip}, 0) AS c FROM dbo.proof_many")]
    [InlineData($"SELECT GREATEST(CAST({Flip} AS int), 0) AS c FROM dbo.proof_many")]
    public async Task DescribeAsync_GreatestOfAValueThatChanges_NotNullAndNoNullRow(string sql)
    {
        sqlServer.SkipBelow(NewFunctions, NewFunctionsReason);
        await WithManyRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync(sql, false, SidecarValues.NullableSource.Expression);
            (await NullRowsAsync(sql)).ShouldBe(0, sql);
        });
    }

    [Theory]
    [MemberData(nameof(ProvedForms))]
    public async Task DescribeAsync_ProvedForm_NotNullAndNoNullRow(int major, string sql)
    {
        sqlServer.SkipBelow(major, NewFunctionsReason);
        await WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync(sql, false, SidecarValues.NullableSource.Expression);
            (await RowsAsync(sql)).ShouldBeGreaterThan(0, sql);
            (await NullRowsAsync(sql)).ShouldBe(0, sql);
        });
    }

    [Theory]
    [MemberData(nameof(BreakingAttempts))]
    public async Task DescribeAsync_BreakingAttempt_NullableAndNullRow(int major, string sql)
    {
        sqlServer.SkipBelow(major, NewFunctionsReason);
        await WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(sql)).ShouldBeGreaterThan(0, sql);
        });
    }

    // A column read through a CTE whose own WHERE keeps it from NULL, where no join of the query can make it NULL.
    [Theory]
    [InlineData("WITH q AS (SELECT b FROM dbo.proof_rows WHERE b > 0)", "SELECT b AS c FROM q")]
    [InlineData("WITH q AS (SELECT b FROM dbo.proof_rows WHERE b IS NOT NULL)", "SELECT x.b AS c FROM q AS x")]
    [InlineData("WITH q (y) AS (SELECT b FROM dbo.proof_rows WHERE b > 0)", "SELECT y AS c FROM q")]
    [InlineData("WITH p AS (SELECT b FROM dbo.proof_rows WHERE b > 0), q AS (SELECT b FROM p)", "SELECT b AS c FROM q")]
    [InlineData("WITH q AS (SELECT b FROM dbo.proof_rows WHERE b > 0 GROUP BY b)", "SELECT b AS c FROM q")]
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT q.b AS c FROM q JOIN dbo.proof_rows AS r ON r.id = q.id"
    )]
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT q.b AS c FROM q LEFT JOIN dbo.proof_other AS o ON o.rows_id = q.id"
    )]
    // The filter of the outer query makes the LEFT JOIN of the CTE inner, so the CTE's own filter holds of its rows.
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT q.b AS c FROM dbo.proof_other AS p LEFT JOIN q ON q.id = p.id WHERE q.id > 0"
    )]
    public Task DescribeAsync_FilterOfACte_NotNullAndNoNullRow(string with, string select) =>
        WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync($"{with} {select}", false, SidecarValues.NullableSource.Expression);
            (await RowsAsync(select, with)).ShouldBeGreaterThan(0, select);
            (await NullRowsAsync(select, with)).ShouldBe(0, select);
        });

    [Theory]
    [InlineData("WITH q AS (SELECT b FROM dbo.proof_rows)", "SELECT b AS c FROM q")]
    [InlineData("WITH q AS (SELECT b FROM dbo.proof_rows WHERE b > 0 OR a > 0)", "SELECT b AS c FROM q")]
    [InlineData("WITH q AS (SELECT b FROM dbo.proof_rows WHERE b > 0 GROUP BY ROLLUP (b))", "SELECT b AS c FROM q")]
    [InlineData("WITH q AS (SELECT g FROM dbo.proof_rows WHERE g > 1 GROUP BY ALL g)", "SELECT g AS c FROM q")]
    [InlineData("WITH q AS (SELECT b FROM dbo.proof_rows WHERE b > 0 UNION ALL SELECT NULL)", "SELECT b AS c FROM q")]
    [InlineData("WITH q AS (SELECT a AS x, b AS a FROM dbo.proof_rows WHERE a > 0)", "SELECT a AS c FROM q")]
    [InlineData("WITH q (x, y) AS (SELECT a, b FROM dbo.proof_rows WHERE a > 0)", "SELECT y AS c FROM q")]
    [InlineData("WITH p AS (SELECT b FROM dbo.proof_rows), q AS (SELECT b FROM p WHERE 1 = 1)", "SELECT b AS c FROM q")]
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT q.b AS c FROM dbo.proof_rows AS r LEFT JOIN q ON q.id = r.id"
    )]
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT q.b AS c FROM q RIGHT JOIN dbo.proof_rows AS r ON q.id = r.id"
    )]
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT q.b AS c FROM q FULL JOIN dbo.proof_rows AS r ON q.id = r.id"
    )]
    [InlineData(
        "WITH q AS (SELECT CAST(NULL AS int) AS b, 1 AS n UNION ALL SELECT b, n + 1 FROM q WHERE n < 2)",
        "SELECT b AS c FROM q"
    )]
    // Inside q, proof_rows is the table: a CTE sees only the CTEs before it.
    [InlineData(
        "WITH q AS (SELECT b FROM proof_rows), proof_rows AS (SELECT b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT b AS c FROM q"
    )]
    // A name of three parts is the table's column, though the CTE's alias is its schema's name.
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT dbo.proof_rows.b AS c FROM q AS dbo JOIN dbo.proof_rows ON proof_rows.id = dbo.id + 1"
    )]
    // A name under the alias of a LEFT JOIN of a CTE that is a property of a CLR column of the other source.
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0)",
        "SELECT q.b AS c FROM (SELECT id, solid AS q FROM dbo.proof_shapes) AS p LEFT JOIN q ON q.id = p.id + 100 "
            + "WHERE q.STSrid = 0"
    )]
    [InlineData(
        "WITH q AS (SELECT id, b FROM dbo.proof_rows WHERE b > 0), r AS (SELECT q.b "
            + "FROM (SELECT id, solid AS q FROM dbo.proof_shapes) AS p LEFT JOIN q ON q.id = p.id + 100 "
            + "WHERE q.STSrid = 0)",
        "SELECT b AS c FROM r"
    )]
    public Task DescribeAsync_CteThatLetsANullThrough_NullableAndNullRow(string with, string select) =>
        WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync($"{with} {select}", true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(select, with)).ShouldBeGreaterThan(0, select);
        });

    // A column of a CTE of several branches, a recursive one among them, that every branch proves, a recursive member
    // with the CTE's own columns taken as not null, though the server answers every column of a recursive CTE nullable.
    [Theory]
    [InlineData("WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5)", "SELECT n AS c FROM t")]
    [InlineData(
        "WITH t (id, x, depth) AS (SELECT id, a, 1 FROM dbo.proof_rows UNION ALL SELECT r.id, t.x + r.a, t.depth + 1 "
            + "FROM dbo.proof_rows AS r JOIN t ON r.id = t.id + 1 WHERE t.depth < 3)",
        "SELECT x AS c FROM t"
    )]
    // A stored column that the anchor's WHERE keeps from NULL, carried by the recursion.
    [InlineData(
        "WITH t (b, depth) AS (SELECT b, 1 FROM dbo.proof_rows WHERE b > 0 "
            + "UNION ALL SELECT t.b, t.depth + 1 FROM t WHERE t.depth < 3)",
        "SELECT b AS c FROM t"
    )]
    // Two columns that the recursion swaps, each proved only with the other.
    [InlineData(
        "WITH t (x, y, n) AS (SELECT a, id, 1 FROM dbo.proof_rows UNION ALL SELECT y, x, n + 1 FROM t WHERE n < 3)",
        "SELECT x AS c FROM t"
    )]
    [InlineData(
        "WITH t (s, n) AS (SELECT CAST(a AS varchar(100)), 1 FROM dbo.proof_rows UNION ALL "
            + "SELECT CAST(t.s + '>' + CAST(t.n AS varchar(10)) AS varchar(100)), n + 1 FROM t WHERE n < 3)",
        "SELECT s AS c FROM t"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 5)",
        "SELECT t.n AS c FROM t LEFT JOIN dbo.proof_other AS o ON o.id = t.n"
    )]
    // A CTE of several branches that is not recursive.
    [InlineData(
        "WITH q (x) AS (SELECT a FROM dbo.proof_rows UNION ALL SELECT COUNT(b) FROM dbo.proof_rows)",
        "SELECT q.x AS c FROM q"
    )]
    public Task DescribeAsync_CteOfSeveralBranches_NotNullAndNoNullRow(string with, string select) =>
        WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync($"{with} {select}", false, SidecarValues.NullableSource.Expression);
            (await RowsAsync(select, with)).ShouldBeGreaterThan(0, select);
            (await NullRowsAsync(select, with)).ShouldBe(0, select);
        });

    // A CTE of several branches with a branch that gives NULL in that place, or read through an outer join.
    [Theory]
    [InlineData(
        "WITH t (b, n) AS (SELECT a, 1 FROM dbo.proof_rows UNION ALL "
            + "SELECT r.b, t.n + 1 FROM dbo.proof_rows AS r JOIN t ON r.id = t.n WHERE t.n < 3)",
        "SELECT b AS c FROM t"
    )]
    // The anchor's NULL of y reaches x through the swap.
    [InlineData(
        "WITH t (x, y, n) AS (SELECT a, b, 1 FROM dbo.proof_rows UNION ALL SELECT y, x, n + 1 FROM t WHERE n < 3)",
        "SELECT x AS c FROM t"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT NULL UNION ALL SELECT n + 1 FROM t WHERE n < 3)",
        "SELECT n AS c FROM t"
    )]
    [InlineData(
        "WITH t (n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM t WHERE n < 2)",
        "SELECT t.n AS c FROM dbo.proof_rows AS r LEFT JOIN t ON t.n = r.id"
    )]
    [InlineData(
        "WITH q (x) AS (SELECT a FROM dbo.proof_rows UNION ALL SELECT b FROM dbo.proof_rows)",
        "SELECT q.x AS c FROM q"
    )]
    public Task DescribeAsync_CteOfSeveralBranchesThatLetsANullThrough_NullableAndNullRow(string with, string select) =>
        WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync($"{with} {select}", true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(select, with)).ShouldBeGreaterThan(0, select);
        });

    // Read from its one table, c is a row of that table, which the constraint keeps not null, though the server calls
    // its expression nullable (it answers "(1) PERSISTED NOT NULL" not null itself, design fact 5); through an outer
    // join that finds no row, or in the total of a ROLLUP, it is NULL, and the server still names it as the source.
    [Fact]
    public Task DescribeAsync_PersistedNotNull_OnlyFromOneTable() =>
        WithRowsAsync(async () =>
        {
            var alone = "SELECT c FROM dbo.proof_computed";
            await ShouldDescribeColumnAsync(alone, false, SidecarValues.NullableSource.Catalog);
            (await RowsAsync(alone)).ShouldBeGreaterThan(0);
            (await NullRowsAsync(alone)).ShouldBe(0);

            var joined = "SELECT pc.c FROM dbo.proof_rows AS r LEFT JOIN dbo.proof_computed AS pc ON pc.id = r.id";
            await ShouldDescribeColumnAsync(joined, true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(joined)).ShouldBeGreaterThan(0);

            var rolledUp = "SELECT c FROM dbo.proof_computed GROUP BY ROLLUP (c)";
            await ShouldDescribeColumnAsync(rolledUp, true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(rolledUp)).ShouldBeGreaterThan(0);

            // WINDOW and OFFSET are no reserved words, so an alias so named is no clause that ends the FROM.  The join
            // names the alias no second time, which the reader would refuse as a clause that comes twice.
            foreach (var alias in (string[])["window", "offset"])
            {
                var aliased =
                    $"SELECT c FROM dbo.proof_computed {alias} RIGHT JOIN dbo.proof_rows AS r ON r.id = c / 2";
                await ShouldDescribeColumnAsync(aliased, true, SidecarValues.NullableSource.Server);
                (await NullRowsAsync(aliased)).ShouldBeGreaterThan(0);
            }
        });

    // NTILE of a parameter proves nothing: run with @n NULL it raises error 4116 (design fact 7), so there is no NULL
    // row to show, and the describe alone is held.
    [Fact]
    public Task DescribeAsync_NtileOfAParameter_StaysNullable() =>
        WithRowsAsync(() =>
            ShouldDescribeColumnAsync(
                "SELECT NTILE(@n) OVER (ORDER BY id) AS c FROM dbo.proof_rows",
                true,
                SidecarValues.NullableSource.Server
            )
        );

    [Theory]
    [MemberData(nameof(ValuesReadTwice))]
    public Task DescribeAsync_ValueReadTwice_StaysNullable(string sql) =>
        WithRowsAsync(() => ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server));

    // The server's source type is read only in the session's database, so a column of another database is not shown
    // to be stored, by its name, a synonym, a derived table or a CTE, and an argument before the proved one stays
    // nullable.  Its rows are not read: the limit is what is described.
    [Theory]
    [InlineData(
        $"SELECT COALESCE(o.company, N'x') AS c FROM {SqlServerDatabases.RealisticDatabase}.dbo.customers AS o"
    )]
    [InlineData("SELECT COALESCE(o.company, N'x') AS c FROM dbo.proof_far AS o")]
    [InlineData(
        $"SELECT COALESCE(q.company, N'x') AS c FROM (SELECT company FROM {SqlServerDatabases.RealisticDatabase}"
            + ".dbo.customers) AS q"
    )]
    [InlineData(
        $"WITH q AS (SELECT company FROM {SqlServerDatabases.RealisticDatabase}.dbo.customers) "
            + "SELECT COALESCE(company, N'x') AS c FROM q"
    )]
    public Task DescribeAsync_ColumnOfAnotherDatabase_StaysNullable(string sql) =>
        WithRowsAsync(async () =>
        {
            await sqlServer.ExecuteAsync(
                $"CREATE SYNONYM dbo.proof_far FOR {SqlServerDatabases.RealisticDatabase}.dbo.customers"
            );
            await ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server);
        });

    // A set operator converts every branch to a CLR type that one branch has, and geometry and geography parse wkt's
    // 'NULL' and 'Null' as their Null, but the server answers the column not null, through every shape that hides the
    // set operator from the query's text and through the expressions over it (measured from 2017 to 2025).  Each is
    // nullable with the nullableSource heuristic, and gives a NULL row.
    public static TheoryData<string, string> ClrConversions =>
        new()
        {
            { "", "SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT 'NULL'" },
            { "", "SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT wkt FROM dbo.proof_shapes" },
            { "", "SELECT wkt AS c FROM dbo.proof_shapes UNION ALL SELECT solid FROM dbo.proof_shapes" },
            { "", "SELECT place AS c FROM dbo.proof_shapes UNION ALL SELECT wkt FROM dbo.proof_shapes" },
            {
                "",
                "SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT solid2 FROM dbo.proof_shapes "
                    + "UNION ALL SELECT wkt FROM dbo.proof_shapes"
            },
            {
                "",
                "(SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT solid2 FROM dbo.proof_shapes) "
                    + "UNION ALL SELECT wkt FROM dbo.proof_shapes"
            },
            { "", $"SELECT d.c FROM {ShapesMix} AS d" },
            { "", $"SELECT e.c FROM (SELECT d.c FROM {ShapesMix} AS d) AS e" },
            { $"WITH d AS {ShapesMix}", "SELECT c FROM d" },
            { $"WITH d AS {ShapesMix}, e AS (SELECT c FROM d)", "SELECT c FROM e" },
            { "", "SELECT c FROM dbo.proof_shapes_mix" },
            { "", "SELECT c FROM dbo.proof_shapes_mix_of" },
            { "", "SELECT c FROM dbo.proof_shapes_mix_fn()" },
            { "", "SELECT v.c FROM dbo.proof_shapes AS s CROSS APPLY (VALUES (s.solid), (s.wkt)) AS v (c)" },
            { "", "SELECT v.c FROM dbo.proof_shapes AS s CROSS APPLY (VALUES (s.solid), ('NULL')) AS v (c)" },
            { "", $"SELECT (d.c) AS c FROM {ShapesMix} AS d" },
            { "", $"SELECT IIF(1 = 1, d.c, d.c) AS c FROM {ShapesMix} AS d" },
            { "", $"SELECT COALESCE(d.c, d.c) AS c FROM {ShapesMix} AS d" },
            { "", $"SELECT ISNULL(d.c, s.wkt) AS c FROM {ShapesMix} AS d CROSS JOIN dbo.proof_shapes AS s" },
        };

    // A column of a CLR type that the server names as a stored column of a table holds that column's values, through a
    // derived table and a CTE too, and so does a set operator whose every branch is such a column, and a column of a
    // view or an inline function that passes one on, which the server names in browse mode 1.
    public static TheoryData<string, string> ClrColumnsUnconverted =>
        new()
        {
            { "", "SELECT solid AS c FROM dbo.proof_shapes" },
            { "", "SELECT c FROM dbo.proof_shapes_plain" },
            { "", "SELECT * FROM dbo.proof_shapes_plain" },
            { "", "SELECT c FROM dbo.proof_shapes_plain_of" },
            { "", "SELECT c FROM dbo.proof_shapes_plain_fn()" },
            { "", "SELECT d.c FROM (SELECT c FROM dbo.proof_shapes_plain) AS d" },
            { "", "SELECT p.c FROM dbo.proof_shapes_plain AS p JOIN dbo.proof_shapes AS s ON s.id = 1" },
            { "", "SELECT d.c FROM (SELECT solid AS c FROM dbo.proof_shapes) AS d" },
            { "WITH d AS (SELECT solid AS c FROM dbo.proof_shapes)", "SELECT c FROM d" },
            { "", "SELECT TOP (2) solid AS c FROM dbo.proof_shapes ORDER BY id" },
            { "", "SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT solid FROM dbo.proof_shapes" },
            { "", "SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT solid2 FROM dbo.proof_shapes" },
            { "", "SELECT place AS c FROM dbo.proof_shapes UNION ALL SELECT place FROM dbo.proof_shapes" },
            { "", "SELECT node AS c FROM dbo.proof_shapes UNION SELECT node FROM dbo.proof_shapes" },
            {
                "",
                "SELECT s.solid AS c FROM dbo.proof_shapes AS s "
                    + "UNION ALL SELECT d.c FROM (SELECT solid2 AS c FROM dbo.proof_shapes) AS d"
            },
        };

    // What the rule cannot clear is nullable, though it holds no NULL: a set operator of one type that the query's text
    // does not show, in a derived table or a view, a VALUES list, an expression, and hierarchyid, whose Parse gave no
    // Null for any string tried.
    public static TheoryData<string, string> ClrColumnsNotCleared =>
        new()
        {
            { "", "SELECT c FROM dbo.proof_shapes_same" },
            {
                "",
                "SELECT d.c FROM (SELECT solid AS c FROM dbo.proof_shapes UNION ALL "
                    + "SELECT solid2 FROM dbo.proof_shapes) AS d"
            },
            { "", "SELECT v.c FROM dbo.proof_shapes AS s CROSS APPLY (VALUES (s.solid), (s.solid2)) AS v (c)" },
            { "", "SELECT ISNULL(shape, solid) AS c FROM dbo.proof_shapes" },
            { "", "SELECT node AS c FROM dbo.proof_shapes UNION ALL SELECT '/1/'" },
        };

    [Theory]
    [MemberData(nameof(ClrConversions))]
    public Task DescribeAsync_ClrConversion_NullableAndNullRow(string with, string select) =>
        WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync($"{with} {select}".Trim(), true, SidecarValues.NullableSource.Heuristic);
            (await NullRowsAsync(select, with)).ShouldBeGreaterThan(0, select);
        });

    [Theory]
    [MemberData(nameof(ClrColumnsUnconverted))]
    public Task DescribeAsync_ClrColumnUnconverted_NotNullAndNoNullRow(string with, string select) =>
        WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync($"{with} {select}".Trim(), false, SidecarValues.NullableSource.Server);
            (await RowsAsync(select, with)).ShouldBeGreaterThan(0, select);
            (await NullRowsAsync(select, with)).ShouldBe(0, select);
        });

    [Theory]
    [MemberData(nameof(ClrColumnsNotCleared))]
    public Task DescribeAsync_ClrColumnNotCleared_NullableWithNoNullRow(string with, string select) =>
        WithRowsAsync(async () =>
        {
            await ShouldDescribeColumnAsync($"{with} {select}".Trim(), true, SidecarValues.NullableSource.Heuristic);
            (await RowsAsync(select, with)).ShouldBeGreaterThan(0, select);
            (await NullRowsAsync(select, with)).ShouldBe(0, select);
        });

    // EOMONTH ignores an offset that is NULL, so no NULL row can be shown, and the form still does not rely on that.
    [Fact]
    public Task DescribeAsync_EndOfMonthOfANullableOffset_StaysNullable() =>
        WithRowsAsync(async () =>
        {
            var sql = "SELECT EOMONTH(at_utc, months_or_null) AS c FROM dbo.proof_dates";
            await ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server);
            (await NullRowsAsync(sql)).ShouldBe(0);
        });

    // Design fact 3: no grouping returns a row for an empty input, so every row of a query with a GROUP BY is of a
    // group with at least one row, a total's included.  Without a GROUP BY the same input gives one row.
    [Theory]
    [InlineData("GROUP BY ROLLUP (g)")]
    [InlineData("GROUP BY CUBE (g)")]
    [InlineData("GROUP BY GROUPING SETS ((g), ())")]
    [InlineData("GROUP BY ()")]
    public Task GroupBy_EmptyInput_ReturnsNoRow(string groupBy) =>
        WithRowsAsync(async () =>
        {
            (await RowsAsync($"SELECT MAX(a) AS c FROM dbo.proof_rows WHERE 1 = 0 {groupBy}")).ShouldBe(0);
            (await RowsAsync("SELECT MAX(a) AS c FROM dbo.proof_rows WHERE 1 = 0")).ShouldBe(1);
        });

    // Design fact 1, the assumption of decision 5: with ANSI_WARNINGS and ARITHABORT off, a conversion overflow, a
    // SUM of two ints over one group and an int's arithmetic that overflow are NULL, and so are COALESCE, GREATEST
    // and LEAST whose one argument that is not NULL overflows the conversion to their type, a date function whose
    // operand overflows the conversion to a date, LAG whose default overflows its value's type, and a CASE that
    // replaces a NULL with a value that overflows the column's type; with ANSI_WARNINGS on, whatever ARITHABORT says,
    // and as SqlClient connects, each is an error.  The settings are made on a connection of the test's own, outside
    // the pool, so that no other test reads them.
    [Theory]
    [InlineData(0, "SELECT CAST(300 AS tinyint)", 220)]
    [InlineData(0, "SELECT SUM(x) FROM (VALUES (1, 2147483647), (1, 2147483647)) AS v (g, x) GROUP BY g", 8115)]
    [InlineData(0, "SELECT x + 1 FROM (VALUES (2147483647)) AS v (x)", 8115)]
    [InlineData(0, "SELECT COALESCE(CAST(NULL AS datetime), 1e10)", 8115)]
    [InlineData(NewFunctions, "SELECT GREATEST(CAST(NULL AS datetime), 1e10)", 8115)]
    [InlineData(NewFunctions, "SELECT LEAST(CAST(NULL AS datetime), 1e10)", 8115)]
    [InlineData(0, "SELECT DATEADD(DAY, 1, 1e10)", 8115)]
    [InlineData(0, "SELECT DATEDIFF(DAY, 1e10, 0)", 8115)]
    [InlineData(0, "SELECT DATEPART(YEAR, 1e10)", 8115)]
    [InlineData(0, "SELECT DATENAME(YEAR, 1e10)", 8115)]
    [InlineData(0, "SELECT YEAR(1e10)", 8115)]
    [InlineData(0, "SELECT LAG(x, 1, 300) OVER (ORDER BY x) FROM (VALUES (CAST(1 AS tinyint))) AS v (x)", 220)]
    [InlineData(
        0,
        "SELECT CASE WHEN x IS NULL THEN 1e10 ELSE x END FROM (VALUES (CAST(NULL AS datetime))) AS v (x)",
        8115
    )]
    public async Task Set_AnsiWarningsOff_OverflowIsNull(int major, string sql, int error)
    {
        sqlServer.SkipBelow(major, NewFunctionsReason);
        (await UnpooledScalarAsync($"SET ANSI_WARNINGS OFF; SET ARITHABORT OFF; {sql}")).ShouldBe(DBNull.Value);

        (
            await Should.ThrowAsync<SqlException>(() =>
                UnpooledScalarAsync($"SET ANSI_WARNINGS ON; SET ARITHABORT OFF; {sql}")
            )
        ).Number.ShouldBe(error);
        (await Should.ThrowAsync<SqlException>(() => sqlServer.ScalarAsync(sql))).Number.ShouldBe(error);
    }

    // The assumption of a comparison in a WHERE: with ANSI_NULLS off, = and <> against a variable keep a row whose
    // column is NULL, and with it on, as SqlClient connects, neither does.  LIKE and an ordering comparison keep none
    // either way.  The settings are made on a connection of the test's own, outside the pool.
    [Theory]
    [InlineData("x = @none", 1)]
    [InlineData("@none = x", 1)]
    [InlineData("x <> @some", 1)]
    [InlineData("x != @some", 1)]
    [InlineData("x > @some", 0)]
    [InlineData("x >= @none", 0)]
    [InlineData("x <> 'a'", 0)]
    [InlineData("x LIKE @none", 0)]
    [InlineData("x NOT LIKE @some", 0)]
    public async Task Set_AnsiNulls_DecidesWhatAComparisonKeeps(string filter, int keptWithItOff)
    {
        var batch =
            "DECLARE @none varchar(9) = NULL, @some varchar(9) = 'a'; "
            + $"SELECT COUNT(*) FROM (VALUES (CAST(NULL AS varchar(9)))) AS v (x) WHERE {filter}";

        (await UnpooledScalarAsync($"SET ANSI_NULLS OFF; {batch}")).ShouldBe(keptWithItOff);
        (await UnpooledScalarAsync($"SET ANSI_NULLS ON; {batch}")).ShouldBe(0);
    }

    // A text that turns ANSI_NULLS off is several statements, which the reader does not read, so its comparison proves
    // nothing.
    [Theory]
    [InlineData("SET ANSI_NULLS OFF; SELECT b AS c FROM dbo.proof_rows WHERE b <> 5")]
    [InlineData("SET ANSI_NULLS OFF SELECT b AS c FROM dbo.proof_rows WHERE b = 5")]
    public Task DescribeAsync_TextThatTurnsAnsiNullsOff_StaysNullable(string sql) =>
        WithRowsAsync(() => ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server));

    // The tables of the proofs and their rows, made for the body and dropped after it.  g and b are NULL in some rows
    // and a in none; proof_other and proof_computed find no row for the rows 3 to 5 of proof_rows.  dbo.COUNT is a
    // user's function that is NULL for every row, which a bare COUNT never calls.  proof_shapes holds a point and
    // two rows whose wkt, never NULL, is the text that geometry and geography parse as their Null, beside solid and
    // solid2, geometries never NULL, place, a geography never NULL, and node, a hierarchyid never NULL;
    // proof_shapes_mix is a view of solid and wkt under UNION ALL, proof_shapes_mix_of a view of that view,
    // proof_shapes_mix_fn an inline function of the same, proof_shapes_plain a view of solid alone,
    // proof_shapes_plain_of a view of that view, proof_shapes_plain_fn an inline function of solid alone, and
    // proof_shapes_same a view of solid and solid2 under UNION ALL.  proof_dates holds dates and zones never NULL
    // beside ones NULL in some rows.  flip, of proof_volatile and its view, is NULL or not at random each time it is
    // evaluated.
    private async Task WithRowsAsync(Func<Task> body)
    {
        await DropAsync();
        try
        {
            await sqlServer.ExecuteAsync(
                "CREATE TABLE dbo.proof_rows (id int NOT NULL PRIMARY KEY, g int NULL, a int NOT NULL, b int NULL)"
            );
            await sqlServer.ExecuteAsync(
                "INSERT INTO dbo.proof_rows (id, g, a, b) "
                    + "VALUES (1, 1, 10, NULL), (2, 1, 20, 5), (3, 2, 30, NULL), (4, NULL, 40, NULL), (5, 3, 0, 7)"
            );
            await sqlServer.ExecuteAsync("CREATE TABLE dbo.proof_other (id int NOT NULL, rows_id int NULL)");
            await sqlServer.ExecuteAsync(
                "INSERT INTO dbo.proof_other (id, rows_id) VALUES (1, 1), (2, 1), (3, 2), (4, NULL)"
            );
            await sqlServer.ExecuteAsync(
                "CREATE TABLE dbo.proof_computed "
                    + "(id int NOT NULL PRIMARY KEY, c AS CAST(id * 2 AS int) PERSISTED NOT NULL)"
            );
            await sqlServer.ExecuteAsync("INSERT INTO dbo.proof_computed (id) VALUES (1), (2)");
            await sqlServer.ExecuteAsync(
                "CREATE TABLE dbo.proof_shapes "
                    + "(id int NOT NULL PRIMARY KEY, wkt nvarchar(100) NOT NULL, shape geometry NULL, "
                    + "solid geometry NOT NULL, solid2 geometry NOT NULL, place geography NOT NULL, "
                    + "node hierarchyid NOT NULL)"
            );
            await sqlServer.ExecuteAsync(
                "INSERT INTO dbo.proof_shapes (id, wkt, shape, solid, solid2, place, node) "
                    + "VALUES (1, N'POINT (1 2)', geometry::Parse('POINT (1 2)'), geometry::Parse('POINT (0 0)'), "
                    + "geometry::Parse('POINT (1 1)'), geography::Parse('POINT (1 2)'), hierarchyid::Parse('/1/')), "
                    + "(2, N'NULL', NULL, geometry::Parse('POINT (0 0)'), geometry::Parse('POINT (1 1)'), "
                    + "geography::Parse('POINT (1 2)'), hierarchyid::Parse('/2/')), "
                    + "(3, N'Null', NULL, geometry::Parse('POINT (0 0)'), geometry::Parse('POINT (1 1)'), "
                    + "geography::Parse('POINT (1 2)'), hierarchyid::Parse('/3/'))"
            );
            await sqlServer.ExecuteAsync($"CREATE VIEW dbo.proof_shapes_mix AS SELECT c FROM {ShapesMix} AS d");
            await sqlServer.ExecuteAsync("CREATE VIEW dbo.proof_shapes_mix_of AS SELECT c FROM dbo.proof_shapes_mix");
            await sqlServer.ExecuteAsync(
                "CREATE VIEW dbo.proof_shapes_plain AS SELECT solid AS c FROM dbo.proof_shapes"
            );
            await sqlServer.ExecuteAsync(
                "CREATE VIEW dbo.proof_shapes_plain_of AS SELECT c FROM dbo.proof_shapes_plain"
            );
            await sqlServer.ExecuteAsync(
                "CREATE FUNCTION dbo.proof_shapes_plain_fn () RETURNS TABLE AS RETURN "
                    + "SELECT solid AS c FROM dbo.proof_shapes"
            );
            await sqlServer.ExecuteAsync(
                "CREATE VIEW dbo.proof_shapes_same AS "
                    + "SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT solid2 FROM dbo.proof_shapes"
            );
            await sqlServer.ExecuteAsync(
                "CREATE FUNCTION dbo.proof_shapes_mix_fn () RETURNS TABLE AS RETURN "
                    + "SELECT solid AS c FROM dbo.proof_shapes UNION ALL SELECT wkt FROM dbo.proof_shapes"
            );
            await sqlServer.ExecuteAsync(
                "CREATE TABLE dbo.proof_dates (id int NOT NULL PRIMARY KEY, at_utc datetime2(0) NOT NULL, "
                    + "maybe datetime2(0) NULL, months int NOT NULL, months_or_null int NULL, "
                    + "zone sysname NOT NULL, zone_or_null sysname NULL)"
            );
            await sqlServer.ExecuteAsync(
                "INSERT INTO dbo.proof_dates (id, at_utc, maybe, months, months_or_null, zone, zone_or_null) "
                    + "VALUES (1, '2024-01-31', NULL, 1, NULL, N'Pacific Standard Time', NULL), "
                    + "(2, '2024-03-10 10:00', '2024-04-01', -1, 2, N'UTC', N'UTC'), "
                    + "(3, '2024-11-03 09:30', NULL, 0, NULL, N'Tokyo Standard Time', NULL)"
            );
            await sqlServer.ExecuteAsync(
                "CREATE TABLE dbo.proof_volatile "
                    + "(id int NOT NULL PRIMARY KEY, flip AS CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END)"
            );
            await sqlServer.ExecuteAsync("INSERT INTO dbo.proof_volatile (id) VALUES (1), (2)");
            await sqlServer.ExecuteAsync(
                "CREATE VIEW dbo.proof_volatile_view AS "
                    + "SELECT id, CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END AS flip FROM dbo.proof_rows"
            );
            await sqlServer.ExecuteAsync(
                "CREATE FUNCTION dbo.[COUNT] (@x int) RETURNS int AS BEGIN RETURN NULLIF(@x, @x) END"
            );
            await body();
        }
        finally
        {
            await DropAsync();
        }
    }

    // The tables of the proofs, and dbo.proof_many, whose 2,000 rows hold b and d NULL in every other row, beside
    // dbo.proof_flip, a user's function that gives its argument or NULL at random through a view over NEWID().
    private Task WithManyRowsAsync(Func<Task> body) =>
        WithRowsAsync(async () =>
        {
            await sqlServer.ExecuteAsync(
                "CREATE TABLE dbo.proof_many (id int NOT NULL PRIMARY KEY, b int NULL, d datetime2(0) NULL)"
            );
            await sqlServer.ExecuteAsync(
                "INSERT INTO dbo.proof_many (id, b, d) "
                    + "SELECT n, CASE WHEN n % 2 = 0 THEN n END, "
                    + "CASE WHEN n % 2 = 0 THEN DATEADD(DAY, n, CAST('2020-01-01' AS datetime2(0))) END "
                    + "FROM (SELECT TOP (2000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n "
                    + "FROM sys.all_columns AS x CROSS JOIN sys.all_columns AS y) AS numbers"
            );
            await sqlServer.ExecuteAsync("CREATE VIEW dbo.proof_random AS SELECT NEWID() AS v");
            await sqlServer.ExecuteAsync(
                "CREATE FUNCTION dbo.proof_flip (@x int) RETURNS int AS BEGIN "
                    + "RETURN (SELECT CASE WHEN ABS(CHECKSUM(v)) % 2 = 0 THEN @x END FROM dbo.proof_random) END"
            );
            await body();
        });

    private Task DropAsync() =>
        sqlServer.ExecuteAsync(
            "DROP SYNONYM IF EXISTS dbo.proof_far; "
                + "DROP FUNCTION IF EXISTS dbo.proof_flip, dbo.[COUNT], dbo.proof_shapes_mix_fn, "
                + "dbo.proof_shapes_plain_fn; "
                + "DROP VIEW IF EXISTS dbo.proof_random, dbo.proof_volatile_view, dbo.proof_shapes_mix_of, "
                + "dbo.proof_shapes_mix, dbo.proof_shapes_plain_of, dbo.proof_shapes_plain, dbo.proof_shapes_same; "
                + "DROP TABLE IF EXISTS dbo.proof_many, dbo.proof_volatile, dbo.proof_dates, dbo.proof_shapes, "
                + "dbo.proof_computed, dbo.proof_other, dbo.proof_rows"
        );

    // The query's one column, described in a session of its own, must be as expected.
    private async Task ShouldDescribeColumnAsync(string sql, bool nullable, string source)
    {
        var description = await Sessions.ShouldDescribeAsync(
            sqlServer.ConnectionString,
            Of(SqlDialectRules.SqlServer, sql)
        );

        var column = description.Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        (column.Nullable, column.NullableSource).ShouldBe((nullable, source), sql);
    }

    // The rows of the query in which c is NULL.
    private Task<int> NullRowsAsync(string sql, string with = "") =>
        RowsAsync($"SELECT * FROM ({sql}) AS q WHERE q.c IS NULL", with);

    // The number of rows of the query, run through SqlClient's own connection, whose ANSI_WARNINGS are on, @style bound
    // to NULL where the query names it.
    private async Task<int> RowsAsync(string sql, string with = "")
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = sqlServer.SourceOf(SqlServerDatabases.Database);
        await using var command = source.CreateCommand($"{with} SELECT COUNT(*) FROM ({sql}) AS counted");
        if (sql.Contains("@style", StringComparison.Ordinal))
        {
            _ = command.Parameters.Add(new SqlParameter("@style", SqlDbType.Int) { Value = DBNull.Value });
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    // One value from a connection of the test's own that no pool keeps, so a SET in the batch dies with it.
    private async Task<object?> UnpooledScalarAsync(string sql)
    {
        var token = TestContext.Current.CancellationToken;
        var unpooled = new SqlConnectionStringBuilder(sqlServer.ConnectionString) { Pooling = false };
        await using var source = SqlClientFactory.Instance.CreateDataSource(unpooled.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(token);
    }
}
