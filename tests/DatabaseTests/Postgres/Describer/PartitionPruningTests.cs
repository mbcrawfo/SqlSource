using System.Threading.Tasks;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// A plan asked with partition pruning: a constant of the partition key prunes at planning, and a parameter or a stable
// function when the executor starts, which asks the plan again without pruning.  Each query tries to make the walk
// answer not null for a column that a partition left out of the plan, or one it holds, makes NULL, and runs over rows
// with a NULL in the one partition that allows it.
[Collection(UsesPostgres.Name)]
public sealed class PartitionPruningTests(PostgresDatabases postgres)
{
    private const string P = "pruning.p";

    // The query, and for each column 'n' where the walk proves it not null and '?' where it does not.
    public static TheoryData<string, string> Queries =>
        new()
        {
            // A parameter of the key, which the executor prunes for its NULL: asked again, every partition shows.
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k = @p", "?" },
            { $"SELECT p.id + 1 AS v FROM {P} p WHERE p.k = @p", "n" },
            // A stable function of the key, which the executor prunes too: every partition proves id.
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k = extract(dow FROM now())::integer * 0 + 2", "?" },
            { $"SELECT p.id + 1 AS v FROM {P} p WHERE p.k = extract(dow FROM now())::integer * 0 + 2", "n" },
            // A constant of the key, which the planner prunes: the partition left, or none.
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k = 1", "n" },
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k = 2", "?" },
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k = 3", "?" },
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k IN (1, 2)", "?" },
            // A set operation over what the planner left of each branch.
            { $"SELECT p.n + 0 AS v FROM {P} p WHERE p.k = 1 UNION ALL SELECT q.n + 0 FROM {P} q WHERE q.k = 1", "n" },
            { $"SELECT p.n + 0 AS v FROM {P} p WHERE p.k = 1 UNION ALL SELECT q.n + 0 FROM {P} q WHERE q.k = 2", "?" },
            { $"SELECT p.n + 0 AS v FROM {P} p WHERE p.k = 1 UNION ALL SELECT q.n + 0 FROM {P} q WHERE q.k = @p", "?" },
            // A gated query's column of the table, which keeps the table's own answer whatever partition is left.
            { $"SELECT p.n, t.id FROM {P} p LEFT JOIN pruning.t t ON t.id = p.id WHERE p.k = 1", "??" },
            { $"SELECT p.n, t.id FROM {P} p LEFT JOIN pruning.t t ON t.id = p.id WHERE p.k = 2", "??" },
            { $"SELECT p.n, t.id FROM {P} p LEFT JOIN pruning.t t ON t.id = p.id WHERE p.k = @p", "??" },
            { $"SELECT p.n, t.id FROM pruning.t t LEFT JOIN {P} p ON p.id = t.id AND p.k = 1", "?n" },
            // An UPDATE returns the new row, which may move to a partition the planner pruned.
            { $"UPDATE {P} p SET k = 2, n = NULL WHERE p.k = 1 RETURNING p.n + 1 AS v", "?" },
            { $"UPDATE {P} p SET k = 2, n = NULL WHERE p.k IN (1, 3) RETURNING p.n + 1 AS v", "?" },
            { $"UPDATE {P} p SET k = 2, n = NULL WHERE p.k = 1 AND p.id = 1 RETURNING p.n + 1 AS v, p.id", "?n" },
            {
                $"UPDATE {P} p SET k = 2, n = NULL FROM pruning.t t WHERE t.id = p.id AND p.k = 1 "
                    + "RETURNING p.n + 1 AS v",
                "?"
            },
            {
                $"WITH u AS (UPDATE {P} p SET k = 2, n = NULL WHERE p.k = 1 RETURNING p.n + 1 AS v) SELECT u.v FROM u",
                "?"
            },
            // A DELETE returns the old row, which is in the partition left.
            { $"DELETE FROM {P} p WHERE p.k = 1 RETURNING p.n + 1 AS v", "n" },
        };

    // A MERGE returns the new row of an update too, with or without a CHECK that constraint exclusion reads.  MERGE
    // has RETURNING from PostgreSQL 17.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DescribeAsync_MergeThatMovesARowToAPrunedPartition_KeepsItsReturningUnproved(bool check)
    {
        postgres.SkipBelow(17, "MERGE has no RETURNING below 17");
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Pruning);
            if (check)
            {
                await postgres.ExecuteAsync(ScratchSchema.PruningChecked);
            }

            await ProvedColumns.ShouldAnswerAsync(
                postgres,
                $"MERGE INTO {P} p USING (SELECT 1 AS id) s ON p.id = s.id AND p.k = 1 "
                    + "WHEN MATCHED THEN UPDATE SET k = 2, n = NULL RETURNING p.n + 1 AS v",
                "?"
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropPruning);
        }
    }

    // A CHECK of the partition a row moves to, which constraint exclusion reads whatever partition pruning is: the
    // plan without pruning still shows that partition.  A read is right to leave it out.
    public static TheoryData<string, string> CheckedQueries =>
        new()
        {
            { $"UPDATE {P} p SET k = 2, n = NULL WHERE p.k = 1 RETURNING p.n + 1 AS v", "?" },
            {
                $"UPDATE {P} p SET k = 2, n = NULL FROM pruning.t t WHERE t.id = p.id AND p.k = 1 "
                    + "RETURNING p.n + 1 AS v",
                "?"
            },
            {
                $"WITH u AS (UPDATE {P} p SET k = 2, n = NULL WHERE p.k = 1 RETURNING p.n + 1 AS v) SELECT u.v FROM u",
                "?"
            },
            { $"UPDATE {P} p SET k = 2, n = NULL WHERE p.k = @p RETURNING p.n + 1 AS v", "?" },
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k = 1", "n" },
        };

    [Theory]
    [MemberData(nameof(CheckedQueries))]
    public async Task DescribeAsync_ColumnOverAPartitionAConstraintRulesOut_IsProvedOnlyWhenItCannotBeNull(
        string sql,
        string expected
    )
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Pruning);
            await postgres.ExecuteAsync(ScratchSchema.PruningChecked);

            await ProvedColumns.ShouldAnswerAsync(postgres, sql, expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropPruning);
        }
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task DescribeAsync_ColumnOverAPrunedPartitionedTable_IsProvedOnlyWhenItCannotBeNull(
        string sql,
        string expected
    )
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Pruning);

            await ProvedColumns.ShouldAnswerAsync(postgres, sql, expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropPruning);
        }
    }
}
