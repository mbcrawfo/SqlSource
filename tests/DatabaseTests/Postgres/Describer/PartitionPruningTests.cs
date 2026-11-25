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
            // A stable function of the key, which the executor prunes too.
            { $"SELECT p.n + 1 AS v FROM {P} p WHERE p.k = extract(dow FROM now())::integer * 0 + 2", "?" },
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
        };

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
