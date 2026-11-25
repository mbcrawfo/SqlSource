using System.Threading.Tasks;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// A column of no origin through a scan of another plan, whose place in that plan the plan does not show: the queries
// that try to make the walk answer not null for a column that holds NULL.  Each runs too, over rows with a NULL in
// every column that may hold one, and a column answered not null must hold none.  A column here is an expression,
// since one with an origin is answered by it, and a query filters on the scan where the planner would drop the scan.
[Collection(UsesPostgres.Name)]
public sealed class ScanColumnTests(PostgresDatabases postgres)
{
    private const string T = "scan_columns.t";

    // The query, and for each column 'n' where the walk proves it not null and '?' where it does not.
    public static TheoryData<string, string> Queries =>
        new()
        {
            // Proved: every entry the column can be proves it, or is of a family that cannot reach it.
            { $"SELECT s.c FROM (SELECT t.id, count(*) AS c FROM {T} t GROUP BY t.id OFFSET 0) s", "n" },
            {
                $"SELECT s.total, s.last_day FROM (SELECT t.id, sum(t.amount) AS total, max(t.day) AS last_day "
                    + $"FROM {T} t GROUP BY t.id OFFSET 0) s",
                "n?"
            },
            {
                $"WITH c AS MATERIALIZED (SELECT upper(t.label) AS u, count(*) AS n FROM {T} t GROUP BY t.label) "
                    + "SELECT c.n, c.u FROM c",
                "n?"
            },
            {
                "SELECT o.c FROM (SELECT s.c, s.id FROM "
                    + $"(SELECT t.id, count(*) AS c FROM {T} t GROUP BY t.id OFFSET 0) s OFFSET 0) o WHERE o.id > 0",
                "n"
            },
            { $"SELECT s.v FROM (SELECT coalesce(t.n, 0) AS v FROM {T} t OFFSET 0) s WHERE s.v >= 0", "n" },
            { $"SELECT s.a FROM (SELECT t.id + 1 AS a, t.day AS b FROM {T} t OFFSET 0) s WHERE s.a > 0", "n" },
            {
                $"SELECT t.id, t.at FROM {T} t UNION ALL (SELECT t2.id, t2.at FROM {T} t2 ORDER BY t2.id LIMIT 1)",
                "nn"
            },
            { $"SELECT a.id, a.note, a.at FROM {T} a UNION SELECT b.big, b.label, b.at FROM {T} b ORDER BY 3", "n?n" },
            // A sibling of the column's family that may be NULL, under either name.
            { $"SELECT s.a, s.b FROM (SELECT t.id + 1 AS a, t.n + 1 AS b FROM {T} t OFFSET 0) s WHERE s.a > 0", "??" },
            { $"SELECT s.id FROM (SELECT t.n + 1 AS id, t.id + 1 AS n FROM {T} t OFFSET 0) s WHERE s.n > 0", "?" },
            {
                $"SELECT s.l FROM (SELECT upper(t.label) AS l, upper(t.note) AS x FROM {T} t OFFSET 0) s "
                    + "WHERE s.x IS DISTINCT FROM 'q'",
                "?"
            },
            { $"SELECT s.a FROM (SELECT t.id + 1 AS a, NULL::integer AS b FROM {T} t OFFSET 0) s WHERE s.a > 0", "?" },
            // A column of the same kind that the query does not read, which only a MATERIALIZED query keeps.
            {
                $"WITH c AS (SELECT t.label, count(*) AS n, sum(t.id) AS total FROM {T} t GROUP BY t.label) "
                    + "SELECT c.n FROM c",
                "?"
            },
            {
                $"WITH c AS MATERIALIZED (SELECT t.label, count(*) AS n, sum(t.id) AS total FROM {T} t "
                    + "GROUP BY t.label) SELECT c.n FROM c",
                "n"
            },
            // A WITH query read twice keeps its unread column as MATERIALIZED does, and that column proves itself.
            {
                $"WITH c AS (SELECT t.label, count(*) AS n, sum(t.id) AS total FROM {T} t GROUP BY t.label) "
                    + "SELECT c1.n FROM c c1 JOIN c c2 ON c1.label = c2.label",
                "n"
            },
            // An expression of no type the walk reads, and an ORDER BY's expression outside the select list.
            {
                $"SELECT s.n, s.l FROM (SELECT initcap(t.label) AS l, t.id, count(*) AS n FROM {T} t GROUP BY 1, 2 "
                    + "OFFSET 0) s WHERE s.id > 0",
                "??"
            },
            {
                $"SELECT s.n FROM (SELECT t.id, count(*) AS n FROM {T} t GROUP BY t.id ORDER BY max(t.n) OFFSET 0) s "
                    + "WHERE s.id > 0",
                "?"
            },
            // A cast the plan does not print: an inlined function's result is text, whatever its entry's type.
            { "SELECT f.t, f.c FROM scan_columns.as_text() f", "??" },
            { "SELECT f.t FROM scan_columns.union_as_text() f", "?" },
            // A domain's column is of no family, so nothing rules it out.
            {
                $"SELECT s.c, s.m FROM (SELECT t.mail AS m, count(*) AS c FROM {T} t GROUP BY t.mail OFFSET 0) s",
                "??"
            },
            // A scan a join extends, a join inside the scan, and an aggregate with no group.
            {
                $"SELECT s.c FROM {T} a LEFT JOIN (SELECT t.id, count(*) AS c FROM {T} t GROUP BY t.id OFFSET 0) s "
                    + "ON s.id = a.n",
                "?"
            },
            {
                $"SELECT s.v FROM (SELECT a.id AS k, b.id AS v FROM {T} a LEFT JOIN {T} b ON b.n = a.id OFFSET 0) s",
                "?"
            },
            {
                $"SELECT s.m, s.c FROM (SELECT max(t.id) AS m, count(*) AS c FROM {T} t WHERE t.id < 0 OFFSET 0) s "
                    + "WHERE s.c >= 0",
                "??"
            },
            // A grouping set puts NULL in a key of its total row, so a plan with one proves nothing.
            {
                $"SELECT s.k2 FROM (SELECT t.id + 1 AS k2, count(*) AS c FROM {T} t GROUP BY ROLLUP (t.id) OFFSET 0) s "
                    + "WHERE s.c > 0",
                "?"
            },
            // Two columns of one table through one scan, the first kept and the second on the side a join extends.
            {
                $"SELECT s.a, s.b FROM (SELECT x.id AS a, y.label AS b FROM {T} x LEFT JOIN {T} y ON y.id = x.n "
                    + "OFFSET 0) s WHERE s.a > 0",
                "n?"
            },
            // A recursive CTE, and a user's cast over a set operation.
            {
                $"WITH RECURSIVE r AS (SELECT t.id, t.n FROM {T} t WHERE t.id = 1 "
                    + "UNION ALL SELECT r.id + 1, r.n FROM r WHERE r.id < 3) SELECT r.id, r.n FROM r",
                "??"
            },
            {
                $"SELECT s.a::bigint, s.b FROM (SELECT t.note AS a, t.id::bigint AS b FROM {T} t "
                    + $"UNION ALL SELECT t2.label, t2.id::bigint FROM {T} t2) s WHERE s.b < 0",
                "?n"
            },
            {
                $"SELECT DISTINCT s.a::bigint FROM (SELECT t.note AS a FROM {T} t "
                    + $"UNION ALL SELECT t2.label FROM {T} t2) s WHERE s.a ~ '^[0-9]+$'",
                "?"
            },
        };

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task DescribeAsync_ColumnThroughAScan_IsProvedOnlyWhenItCannotBeNull(string sql, string expected)
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);

            await ShouldAnswerAsync(sql, expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }

    // On 18 the word NEW, here a column's label, may name the new row, so no scan is read.  Before 18 it names none.
    [Fact]
    public async Task DescribeAsync_ColumnThroughAScanInAStatementThatSaysNew_IsProvedBefore18()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);

            await ShouldAnswerAsync(
                $"SELECT s.c AS new FROM (SELECT t.id, count(*) AS c FROM {T} t GROUP BY t.id OFFSET 0) s",
                postgres.Major >= 18 ? "?" : "n"
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }

    // A user's cast joins two families, so an entry of the other family may be the column: the date here, which is
    // NULL in a row, and which the walk would rule out on a server without the cast.  Both columns are read, since an
    // unread one prints as a NULL that blocks the proof whatever the casts are.
    [Fact]
    public async Task DescribeAsync_UsersCastAcrossFamilies_RulesNoEntryOut()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);
            await postgres.ExecuteAsync(ScratchSchema.ScanColumnsCast);

            await ShouldAnswerAsync("SELECT f.d, f.c FROM scan_columns.as_bigint() f", "??");
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }

    private Task ShouldAnswerAsync(string sql, string expected) =>
        ProvedColumns.ShouldAnswerAsync(postgres, sql, expected);
}
