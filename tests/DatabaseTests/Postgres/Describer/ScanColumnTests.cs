using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
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
            // A recursive CTE, and a user's cast over a set operation.
            {
                $"WITH RECURSIVE r AS (SELECT t.id, t.n FROM {T} t WHERE t.id = 1 "
                    + "UNION ALL SELECT r.id + 1, r.n FROM r WHERE r.id < 3) SELECT r.id, r.n FROM r",
                "??"
            },
            {
                $"SELECT s.a::bigint, s.b FROM (SELECT t.note AS a, t.id::bigint AS b FROM {T} t "
                    + $"UNION ALL SELECT t2.label, t2.id::bigint FROM {T} t2) s WHERE s.b < 0",
                "??"
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

    // A user's cast joins two families, so an entry of the other family may be the column: the date here, which is
    // NULL in a row, and which the walk would rule out on a server without the cast.
    [Fact]
    public async Task DescribeAsync_UsersCastAcrossFamilies_RulesNoEntryOut()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.ScanColumns);
            await postgres.ExecuteAsync(ScratchSchema.ScanColumnsCast);

            await ShouldAnswerAsync("SELECT f.d FROM scan_columns.as_bigint() f", "?");
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropScanColumns);
        }
    }

    private async Task ShouldAnswerAsync(string sql, string expected)
    {
        var description = await Sessions.ShouldDescribeAsync(postgres.ConnectionString, sql);
        var columns = description.Columns.ShouldNotBeNull();
        string.Concat(columns.Select(static column => column.Nullable == false ? 'n' : '?')).ShouldBe(expected, sql);

        var nulls = await ColumnsWithANullAsync(sql);
        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Nullable == false)
            {
                nulls.ShouldNotContain(index, sql);
            }
        }
    }

    private async Task<HashSet<int>> ColumnsWithANullAsync(string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(token);
        var nulls = new HashSet<int>();
        while (await reader.ReadAsync(token))
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (await reader.IsDBNullAsync(index, token))
                {
                    _ = nulls.Add(index);
                }
            }
        }

        return nulls;
    }
}
