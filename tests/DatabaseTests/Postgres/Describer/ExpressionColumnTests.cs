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
            // A GROUP BY the WHERE fixes: PostgreSQL 18 prints no key, and returns no row for no input.
            { $"SELECT t.id, max(t.amount) FROM {T} t WHERE t.id = 1 GROUP BY t.id", "nn" },
            { $"SELECT max(t.amount) FROM {T} t WHERE t.id = -5 GROUP BY t.id", "n" },
            { $"SELECT t.big, sum(t.amount) FROM {T} t WHERE t.big = @p GROUP BY t.big", "nn" },
            { $"SELECT max(t.amount) FROM {T} t GROUP BY 'x'::text", "n" },
            { $"SELECT max(t.n) FROM {T} t WHERE t.id = 1 GROUP BY t.id", "?" },
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
        };

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
