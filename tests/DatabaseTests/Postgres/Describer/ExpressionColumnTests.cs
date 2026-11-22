using System.Threading.Tasks;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// A column of no origin whose proof reads more of the plan than its own entry: an aggregate whose GROUP BY the WHERE
// fixes, an uncorrelated subquery, a COALESCE over a FULL JOIN and a set operation's expression.  Each query tries to
// make the walk answer not null for a column that holds NULL, and runs over rows with a NULL in every column that may
// hold one.
[Collection(UsesPostgres.Name)]
public sealed class ExpressionColumnTests(PostgresDatabases postgres)
{
    private const string T = "scan_columns.t";

    // A full join of T with itself that keeps a row of each side unmatched.
    private const string Full = $"{T} a FULL JOIN {T} b ON a.id = b.n";

    // The query, and for each column 'n' where the walk proves it not null and '?' where it does not.
    public static TheoryData<string, string> Queries =>
        new()
        {
            // A GROUP BY the WHERE fixes: PostgreSQL 16 and later print no key, and return no row for no input.
            { $"SELECT t.id, max(t.amount) FROM {T} t WHERE t.id = 1 GROUP BY t.id", "nn" },
            { $"SELECT max(t.amount) FROM {T} t WHERE t.id = -5 GROUP BY t.id", "n" },
            { $"SELECT t.big, sum(t.amount) FROM {T} t WHERE t.big = @p GROUP BY t.big", "nn" },
            { $"SELECT max(t.amount) FROM {T} t GROUP BY 'x'::text", "n" },
            { $"SELECT max(t.n) FROM {T} t WHERE t.id = 1 GROUP BY t.id", "?" },
            // An upper aggregate of the same text as a subquery's grouped one is computed over no group at all.
            {
                $"SELECT sum(s.id) AS a, max(s.c) AS b FROM (SELECT t.id, sum(t.id) AS c FROM {T} t WHERE t.id < 0 "
                    + "GROUP BY t.id) s",
                "??"
            },
            {
                $"SELECT sum(s.id) + 1 AS a, max(s.c) AS b FROM (SELECT t.id, sum(t.id) + 1 AS c FROM {T} t "
                    + "WHERE t.id < 0 GROUP BY t.id) s",
                "??"
            },
            // No GROUP BY, or an empty one, returns one row of NULL for no input.
            { $"SELECT max(t.amount) FROM {T} t WHERE t.id = -5", "?" },
            { $"SELECT max(t.amount) FROM {T} t WHERE t.id = -5 GROUP BY ()", "?" },
            { $"SELECT max(t.amount) FROM {T} t WHERE t.id = -5 HAVING count(*) >= 0", "?" },
            // An uncorrelated subquery of one row.
            { $"SELECT (SELECT count(*) FROM {T} x) AS c, t.id FROM {T} t", "nn" },
            { $"SELECT (SELECT coalesce(max(x.n), 0) FROM {T} x) AS m", "n" },
            // One that may return no row, or a NULL.
            { $"SELECT (SELECT max(x.amount) FROM {T} x WHERE x.id < 0) AS m", "?" },
            { $"SELECT (SELECT x.n FROM {T} x ORDER BY x.id LIMIT 1) AS m", "?" },
            { $"SELECT (SELECT count(*) FROM {T} x HAVING count(*) > 100) AS m", "?" },
            { $"SELECT (SELECT count(*) FROM {T} x OFFSET 1) AS m", "?" },
            { $"SELECT (SELECT count(*) FROM {T} x LIMIT 0) AS m", "?" },
            // Inside an expression, of a type in no family, or on the side a join extends.
            { $"SELECT (SELECT count(*) FROM {T} x) + 1 AS m", "?" },
            { $"SELECT ARRAY(SELECT count(*) FROM {T} x) AS m", "?" },
            {
                $"SELECT s.c FROM {T} a LEFT JOIN (SELECT b.id, (SELECT count(*) FROM {T} x) AS c FROM {T} b "
                    + "WHERE b.id = 2) s ON s.id = a.id",
                "?"
            },
            // The word NEW, here an alias, may name the new row, so no subquery is read.
            { $"SELECT (SELECT count(*) FROM {T} x) AS new", "?" },
            // A FULL JOIN's form: a cast within a family, and computed above the join.
            { $"SELECT coalesce(a.big, b.id) AS v FROM {Full}", "n" },
            { $"SELECT coalesce(a.id, b.id) AS v FROM {Full} GROUP BY a.id, b.id", "n" },
            { $"SELECT coalesce(a.id, b.id) + 1 AS v, count(*) AS c FROM {Full} GROUP BY a.id, b.id", "nn" },
            // A LEFT JOIN under a side that extends another relation than the one the form names.
            {
                $"SELECT coalesce(a.id, c.id) AS v FROM ({T} a LEFT JOIN {T} x ON x.n = a.id) "
                    + $"FULL JOIN {T} c ON c.n = a.id",
                "n"
            },
            // A column that may be NULL, one a join under the side extends, and a join above that extends the full
            // join.
            { $"SELECT coalesce(a.n, b.n) AS v FROM {Full}", "?" },
            { $"SELECT coalesce(a.label, b.id::text) AS v FROM {Full}", "?" },
            {
                $"SELECT coalesce(x.id, c.id) AS v FROM ({T} a LEFT JOIN {T} x ON x.n = a.id + 100) "
                    + $"FULL JOIN {T} c ON c.n = a.id",
                "?"
            },
            { $"SELECT coalesce(a.id, b.id) AS v FROM {T} z LEFT JOIN ({Full}) ON a.id = z.id + 100", "?" },
            // A set operation's branch that is an expression.
            { $"SELECT t.id + 1 AS v FROM {T} t UNION ALL SELECT x.big + 1 FROM {T} x", "n" },
            { $"SELECT upper(t.label) AS v FROM {T} t UNION SELECT lower(x.label) FROM {T} x", "n" },
            { "SELECT p.id + 1 AS v FROM scan_columns.p p", "n" },
            { $"SELECT t.id + 1 AS v FROM {T} t UNION ALL SELECT x.n + 1 FROM {T} x", "?" },
            { $"SELECT upper(t.label) AS v FROM {T} t UNION SELECT upper(x.note) FROM {T} x", "?" },
            { "SELECT p.n + 1 AS v FROM scan_columns.p p", "?" },
            // The functions of the table: total, so never NULL over a value that is not.
            {
                "SELECT abs(t.id) AS a, abs(t.big) AS b, abs(t.amount) AS c, ceil(t.amount) AS d, "
                    + "floor(t.amount) AS e, round(t.amount) AS f, round(t.amount, 2) AS g, trunc(t.amount) AS h, "
                    + "trunc(t.amount, 2) AS i, "
                    + "abs(t.amount::float8) AS j, ceil(t.amount::float8) AS k, floor(t.amount::float8) AS l, "
                    + $"round(t.amount::float8) AS m, trunc(t.amount::float8) AS o FROM {T} t",
                "nnnnnnnnnnnnnn"
            },
            { $"SELECT abs(t.n) AS a, round(t.n::numeric, 1) AS b, ceil(t.n::numeric) AS c FROM {T} t", "???" },
            // Under grouping sets: a count and GROUPING are never NULL, in the empty group too.
            {
                "SELECT t.id, count(*) AS a, count(DISTINCT t.label) AS b, count(t.n) AS c, grouping(t.id) AS d, "
                    + $"sum(t.amount) AS e, grouping(t.id) + 1 AS f FROM {T} t GROUP BY ROLLUP (t.id) ORDER BY t.id",
                "?nnnn??"
            },
            { $"SELECT count(*) AS c FROM {T} t WHERE t.id < 0 GROUP BY GROUPING SETS ((t.id), ())", "n" },
            { $"SELECT t.label, grouping(t.label) AS g FROM {T} t GROUP BY CUBE (t.label)", "?n" },
            // A user's function of the name, a value of the group, and grouping sets on the side a join extends.
            { $"SELECT scan_columns.\"grouping\"(t.id) AS g FROM {T} t GROUP BY ROLLUP (t.id)", "?" },
            { $"SELECT max(t.id) AS m, coalesce(t.n, 0) AS c FROM {T} t GROUP BY ROLLUP (t.n)", "??" },
            {
                $"SELECT s.c FROM {T} a LEFT JOIN (SELECT b.id, count(*) AS c FROM {T} b GROUP BY ROLLUP (b.id)) s "
                    + "ON s.id = a.n",
                "?"
            },
            // A VALUES statement's columns of literals, and one a row holds NULL, a parameter or a cast in.
            { "VALUES ('a', 1, NULL), ('b', 2, 3) ORDER BY 2", "nn?" },
            { "VALUES (1, true), (-2, false) LIMIT 1", "nn" },
            { "VALUES (1, 'a'), (2, NULL)", "n?" },
            { "VALUES (@p::bigint, 1), (1, 2)", "?n" },
            { "VALUES (1::bigint, 1), (2, 2)", "?n" },
            { "VALUES (1 + 1, 1), (2, 2)", "?n" },
            { "VALUES (NULLIF(1, 1), 1), (2, 2)", "?n" },
            { "VALUES (1), (2) UNION ALL SELECT NULL::integer", "?" },
            { "SELECT v.column1 FROM (VALUES (1), (NULL)) v", "?" },
        };

    // Only PostgreSQL 18 prints a window's frame, so a query below is read there alone.
    public static TheoryData<string, string> Windows =>
        new()
        {
            { $"SELECT sum(t.amount) OVER (PARTITION BY t.label ORDER BY t.id) AS v FROM {T} t", "n" },
            { $"SELECT sum(t.amount) OVER () AS v FROM {T} t", "n" },
            { $"SELECT max(t.amount) OVER (ORDER BY t.id ROWS UNBOUNDED PRECEDING) AS v FROM {T} t", "n" },
            {
                "SELECT avg(t.id) OVER (ORDER BY t.id ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) AS v "
                    + $"FROM {T} t",
                "n"
            },
            { $"SELECT sum(t.big) OVER w AS a, min(t.label) OVER w AS b FROM {T} t WINDOW w AS (ORDER BY t.id)", "nn" },
            // Several windows of one level, whose values pass up through each other's nodes.
            {
                "SELECT sum(t.amount) OVER (ORDER BY t.id) AS a, sum(t.amount) OVER (PARTITION BY t.label) AS b "
                    + $"FROM {T} t",
                "nn"
            },
            // A window's name is unique within its query level alone, so a subquery's w1 may have another frame.
            {
                "SELECT sum(s.id) OVER (ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING) AS a, s.c "
                    + $"FROM (SELECT t.id, sum(t.id) OVER () AS c FROM {T} t) s",
                "?n"
            },
            {
                "SELECT sum(s.id) OVER () + s.c AS a FROM (SELECT t.id, sum(t.id) OVER (ROWS BETWEEN 1 FOLLOWING AND "
                    + $"2 FOLLOWING) AS c FROM {T} t) s",
                "?"
            },
            {
                "SELECT sum(s.id) OVER w AS a, s.c FROM (SELECT t.id, sum(t.id) OVER w AS c "
                    + $"FROM {T} t WINDOW w AS ()) s WINDOW w AS (ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)",
                "?n"
            },
            // A frame that leaves the current row out, and one with an offset.
            {
                $"SELECT sum(t.amount) OVER (ORDER BY t.id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING) AS v FROM {T} t",
                "?"
            },
            {
                $"SELECT sum(t.amount) OVER (ORDER BY t.id ROWS BETWEEN 2 PRECEDING AND 1 PRECEDING) AS v FROM {T} t",
                "?"
            },
            {
                "SELECT sum(t.amount) OVER (ORDER BY t.id ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING "
                    + $"EXCLUDE CURRENT ROW) AS v FROM {T} t",
                "?"
            },
            {
                $"SELECT sum(t.amount) OVER (ORDER BY t.id ROWS BETWEEN 1 PRECEDING AND CURRENT ROW) AS v FROM {T} t",
                "?"
            },
            // A FILTER, a value that may be NULL, and the side a join extends.
            { $"SELECT sum(t.amount) FILTER (WHERE t.id < 0) OVER () AS v FROM {T} t", "?" },
            { $"SELECT sum(t.n) OVER (ORDER BY t.id) AS v FROM {T} t", "?" },
            { $"SELECT sum(b.amount) OVER () AS v FROM {T} a LEFT JOIN {T} b ON b.id = a.id + 100", "?" },
        };

    [Theory]
    [MemberData(nameof(Windows))]
    public async Task DescribeAsync_WindowAggregate_IsProvedOnlyWhenItsFrameHoldsTheCurrentRow(
        string sql,
        string fromEighteen
    )
    {
        var expected = postgres.Major >= 18 ? fromEighteen : new string('?', fromEighteen.Length);
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);

            await ProvedColumns.ShouldAnswerAsync(postgres, sql, expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task DescribeAsync_ColumnOfAReadForm_IsProvedOnlyWhenItCannotBeNull(string sql, string expected)
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);

            await ProvedColumns.ShouldAnswerAsync(postgres, sql, expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }

    // A plus calls an operator that a schema first on the path may hold, which returns NULL here.  The parser folds a
    // minus into the constant, so it calls none.
    [Theory]
    [InlineData("VALUES (+1), (2)", "?")]
    [InlineData("VALUES (-1), (2)", "n")]
    public async Task DescribeAsync_ValuesWithASignUnderAShadowingPath_ReadsOnlyTheMinus(string sql, string expected)
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ShadowPrefixOperators);

            await ProvedColumns.ShouldAnswerAsync(
                postgres,
                sql,
                expected,
                $"{postgres.ConnectionString};Search Path=shadow,pg_catalog,public"
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropShadow);
        }
    }

    // With standard_conforming_strings off a backslash escapes a quote, so the server reads a NULL in the second column
    // where the tool's lexer reads one string.  No column of a VALUES is read on such a connection.
    [Fact]
    public async Task DescribeAsync_ValuesWithoutStandardConformingStrings_IsNotRead() =>
        await ProvedColumns.ShouldAnswerAsync(
            postgres,
            @"VALUES ('\'', NULL, ', 2, 3), (4, 5, 6) ORDER BY \'x'), ('a', 1, 'b')",
            "???",
            $"{postgres.ConnectionString};Options=-c standard_conforming_strings=off"
        );

    // A count(*) of a schema first on the path returns NULL, under grouping sets as anywhere.
    [Fact]
    public async Task DescribeAsync_CountUnderGroupingSetsShadowedOnThePath_IsNotProved()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);
            await postgres.ExecuteAsync(ScratchSchema.ShadowCount);

            await ProvedColumns.ShouldAnswerAsync(
                postgres,
                $"SELECT count(*) AS c FROM {T} t GROUP BY ROLLUP (t.id)",
                "?",
                $"{postgres.ConnectionString};Search Path=shadow,pg_catalog,public"
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropShadow);
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }

    // Before 17 the second InitPlan's value and the statement's parameter both print $1.  The parameter holds NULL, so
    // no $k up to the statement's parameters is read as an InitPlan's, and from 17 the InitPlan prints its own name.
    [Fact]
    public async Task DescribeAsync_ParameterThatPrintsAsAnInitPlansValue_IsNotProved()
    {
        var expected = postgres.Major >= 17 ? "nn?" : "n??";
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);

            await ProvedColumns.ShouldAnswerAsync(
                postgres,
                $"SELECT (SELECT count(*) FROM {T} x) AS a, (SELECT count(*) FROM {T} y) AS b, @p::bigint AS c",
                expected
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }
}
