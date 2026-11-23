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
        };

    // A CASE that reads a value twice proves nothing unless the value is a stored column: each of these can differ
    // between its two reads, though no plan measured on 2017 or 2025 evaluates them twice, so no NULL row can be shown.
    public static TheoryData<string> ValuesReadTwice =>
        [
            "SELECT CASE WHEN flip IS NULL THEN 0 ELSE flip END AS c FROM dbo.proof_volatile",
            "SELECT CASE WHEN flip IS NULL THEN 0 ELSE flip END AS c FROM dbo.proof_volatile_view",
            "SELECT CASE WHEN q.flip IS NULL THEN 0 ELSE q.flip END AS c "
                + "FROM (SELECT CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END AS flip FROM dbo.proof_rows) AS q",
            "WITH q AS (SELECT CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END AS flip FROM dbo.proof_rows) "
                + "SELECT CASE WHEN flip IS NULL THEN 0 ELSE flip END AS c FROM q",
            "SELECT CASE WHEN q.flip IS NULL THEN 0 ELSE q.flip END AS c FROM dbo.proof_rows "
                + "CROSS APPLY (SELECT CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN 1 END AS flip) AS q",
        ];

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
    public Task DescribeAsync_CaseOfAValueReadTwice_StaysNullable(string sql) =>
        WithRowsAsync(() => ShouldDescribeColumnAsync(sql, true, SidecarValues.NullableSource.Server));

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
    // operand overflows the conversion to a date, and LAG whose default overflows its value's type; with ANSI_WARNINGS
    // on, whatever ARITHABORT says, and as SqlClient connects, each is an error.  The settings are made on a connection
    // of the test's own, outside the pool, so that no other test reads them.
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

    // The tables of the proofs and their rows, made for the body and dropped after it.  g and b are NULL in some rows
    // and a in none; proof_other and proof_computed find no row for the rows 3 to 5 of proof_rows.  dbo.COUNT is a
    // user's function that is NULL for every row, which a bare COUNT never calls.  proof_shapes holds a point and
    // two rows whose wkt, never NULL, is the text that geometry and geography parse as their Null, beside solid, a
    // geometry never NULL.  proof_dates holds dates and zones never NULL beside ones NULL in some rows.  flip, of
    // proof_volatile and its view, is NULL or not at random each time it is evaluated.
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
                    + "solid geometry NOT NULL)"
            );
            await sqlServer.ExecuteAsync(
                "INSERT INTO dbo.proof_shapes (id, wkt, shape, solid) "
                    + "VALUES (1, N'POINT (1 2)', geometry::Parse('POINT (1 2)'), geometry::Parse('POINT (0 0)')), "
                    + "(2, N'NULL', NULL, geometry::Parse('POINT (0 0)')), "
                    + "(3, N'Null', NULL, geometry::Parse('POINT (0 0)'))"
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

    private Task DropAsync() =>
        sqlServer.ExecuteAsync(
            "DROP FUNCTION IF EXISTS dbo.[COUNT]; DROP VIEW IF EXISTS dbo.proof_volatile_view; "
                + "DROP TABLE IF EXISTS dbo.proof_volatile, dbo.proof_dates, dbo.proof_shapes, dbo.proof_computed, "
                + "dbo.proof_other, dbo.proof_rows"
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
    private Task<int> NullRowsAsync(string sql) => RowsAsync($"SELECT * FROM ({sql}) AS q WHERE q.c IS NULL");

    // The number of rows of the query, run through SqlClient's own connection, whose ANSI_WARNINGS are on, @style bound
    // to NULL where the query names it.
    private async Task<int> RowsAsync(string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = sqlServer.SourceOf(SqlServerDatabases.Database);
        await using var command = source.CreateCommand($"SELECT COUNT(*) FROM ({sql}) AS counted");
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
