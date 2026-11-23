using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// A RETURNING column of a value an INSERT writes.  Each query tries to make the walk answer not null for a column that
// holds NULL, and runs, so that the row it returns shows what it wrote.
[Collection(UsesPostgres.Name)]
public sealed class WrittenValueTests(PostgresDatabases postgres)
{
    // The query, and for each column 'n' where the walk proves it not null and '?' where it does not.
    public static TheoryData<string, string> Queries =>
        new()
        {
            // Each column by its place: a literal, a default, a default of NULL, and a generated column.
            { "INSERT INTO written.t (id, a) VALUES (5, 'v') RETURNING g, id, a, b, c", "?nnn?" },
            { "INSERT INTO written.t (id) VALUES (5) RETURNING a", "?" },
            { "INSERT INTO written.t (id, a, b) VALUES (5, 'x', NULL) RETURNING b, a", "?n" },
            // Another column under the column's name, and the column under another's.
            { "INSERT INTO written.t (id, a, b) VALUES (5, 'x', NULL) RETURNING b AS a", "?" },
            { "INSERT INTO written.t (id, a, b) VALUES (5, NULL, 'x') RETURNING b AS a", "n" },
            // A parameter, a function that may return NULL, and one row of several that holds NULL.
            { "INSERT INTO written.t (id, a) VALUES (5, @p::text) RETURNING a", "?" },
            { "INSERT INTO written.t (id, a) VALUES (5, nullif(random()::text, '')) RETURNING a", "?" },
            { "INSERT INTO written.t (id, a) VALUES (5, 'x'), (6, NULL) RETURNING a", "?" },
            // A dropped column before the one written: the child prints a NULL in its place.
            { "INSERT INTO written.gone (a, b) VALUES (1, 'x') RETURNING b, a", "nn" },
            { "INSERT INTO written.gone (a) VALUES (1) RETURNING b, a", "?n" },
            { "INSERT INTO written.gone (b) VALUES ('x') RETURNING b, a", "n?" },
            // The rows of a SELECT.
            { "INSERT INTO written.t (id, a) SELECT s.id + 10, s.label FROM written.src s RETURNING a", "n" },
            { "INSERT INTO written.t (id, a) SELECT s.id + 10, s.note FROM written.src s RETURNING a", "?" },
            {
                "INSERT INTO written.t (id, a) SELECT l.id + 10, r.label FROM written.src l "
                    + "LEFT JOIN written.src r ON r.id = l.id + 100 RETURNING a",
                "?"
            },
            { "INSERT INTO written.t (id, a) SELECT x.id + 10, x.a FROM written.t x RETURNING a", "?" },
            { "INSERT INTO written.t AS x (id, a) SELECT x.id + 10, x.note FROM written.src x RETURNING x.a", "?" },
            { "INSERT INTO written.t AS x (id, a) SELECT x.id + 10, x.label FROM written.src x RETURNING x.a", "n" },
            // A set operation prints its first branch's value alone.
            { "INSERT INTO written.t (id, a) SELECT 300, 'x' UNION ALL SELECT 301, NULL RETURNING a", "?" },
            // ON CONFLICT: DO NOTHING returns only what it wrote, and DO UPDATE returns a row it did not.
            { "INSERT INTO written.t (id, a) VALUES (5, 'x') ON CONFLICT (id) DO NOTHING RETURNING a", "n" },
            {
                "INSERT INTO written.t (id, a) VALUES (1, 'x') ON CONFLICT (id) DO UPDATE SET a = NULL RETURNING a",
                "?"
            },
            { "INSERT INTO written.t (id, a) VALUES (1, 'x') ON CONFLICT (id) DO UPDATE SET b = 'y' RETURNING c", "?" },
            // A BEFORE row trigger on insert, enabled or disabled, and triggers that cannot change the row.
            { "INSERT INTO written.fired (id, a) VALUES (1, 'x') RETURNING a", "?" },
            { "INSERT INTO written.disabled (id, a) VALUES (1, 'x') RETURNING a", "?" },
            { "INSERT INTO written.after_insert (id, a) VALUES (1, 'x') RETURNING a", "n" },
            { "INSERT INTO written.per_statement (id, a) VALUES (1, 'x') RETURNING a", "n" },
            { "INSERT INTO written.before_update (id, a) VALUES (1, 'x') RETURNING a", "n" },
            // A BEFORE row trigger on a partition, two levels down, which the row reaches.
            { "INSERT INTO written.p (id, a) VALUES (150, 'x') RETURNING a", "?" },
            { "INSERT INTO written.p (id, a) VALUES (1, 'x') RETURNING a", "?" },
            // An expression over a partitioned target.
            { "INSERT INTO written.q (id, a) VALUES (1, 'x') RETURNING id + 1 AS i, a || 'y' AS s", "n?" },
            { "INSERT INTO written.p (id, a) VALUES (150, 'x') RETURNING id + 1 AS i, a || 'y' AS s", "n?" },
            // A rule: DO INSTEAD returns its action's row, and DO ALSO makes a second plan.
            { "INSERT INTO written.ruled (id, a) VALUES (1, 'x') RETURNING a, id", "??" },
            { "INSERT INTO written.also (id, a) VALUES (1, 'x') RETURNING a", "?" },
            // The word NEW, here an alias, may name the new row.
            { "INSERT INTO written.t (id, a) VALUES (5, 'x') RETURNING a AS new", "?" },
            // UPDATE stays a limit.
            { "UPDATE written.t SET a = 'x' RETURNING a", "?" },
        };

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task DescribeAsync_WrittenValue_IsProvedOnlyWhereItHoldsNoNull(string sql, string expected)
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);

            await ProvedColumns.ShouldAnswerAsync(postgres, sql, expected);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // Before 16 a partition's generated column can replace the value, so no value written to a partitioned target is
    // read, on a partitioned table with no such partition too.
    [Fact]
    public async Task DescribeAsync_ValueWrittenToAPartitionedTarget_IsProvedFrom16()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);

            await ProvedColumns.ShouldAnswerAsync(
                postgres,
                "INSERT INTO written.q (id, a) VALUES (1, 'x') RETURNING a",
                postgres.Major >= 16 ? "n" : "?"
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // What the proof from 16 rests on: the server refuses the partition.  Before 16 it holds NULL in the written
    // column.
    [Fact]
    public async Task DescribeAsync_PartitionsGeneratedColumnOverAWrittenValue_IsNotProved()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);
            if (postgres.Major >= 16)
            {
                _ = await Should.ThrowAsync<DbException>(() =>
                    postgres.ExecuteAsync(ScratchSchema.WrittenGeneratedPartition)
                );
                return;
            }

            await postgres.ExecuteAsync(ScratchSchema.WrittenGeneratedPartition);

            await ProvedColumns.ShouldAnswerAsync(
                postgres,
                "INSERT INTO written.pg (id, a, b) VALUES (1, 'x', 'y') RETURNING a",
                "?"
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // From 18 a RETURNING list reads the old row, which an INSERT has none of.
    [Fact]
    public async Task DescribeAsync_OldAndNewOfAnInsert_AreNotProved()
    {
        postgres.SkipBelow(18, "RETURNING names old and new from PostgreSQL 18.");
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);

            await ProvedColumns.ShouldAnswerAsync(
                postgres,
                "INSERT INTO written.t (id, a) VALUES (5, 'x') RETURNING old.a AS o, new.a AS n",
                "??"
            );
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // A table's whole column list keeps the model named after the table, so none of its columns is proved.
    [Fact]
    public async Task DescribeAsync_ReturningTheWholeTable_KeepsItsMatchAndItsAnswers()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);

            var description = await Sessions.ShouldDescribeAsync(
                postgres.ConnectionString,
                DescribeRequests.Of("INSERT INTO written.gone (a, b) VALUES (1, 'x') RETURNING *")
            );

            description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
            description
                .Columns.ShouldNotBeNull()
                .Select(static column => column.Nullable)
                .ShouldAllBe(static nullable => nullable == true);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }
}
