using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// A view's column, read from the entry the plan prints for it.  Each query tries to make the walk answer not null for a
// column that holds NULL, and runs over rows with a NULL in every column that may hold one.
[Collection(UsesPostgres.Name)]
public sealed class ViewColumnTests(PostgresDatabases postgres)
{
    // The query, and for each column 'n' where the walk proves it not null and '?' where it does not.
    public static TheoryData<string, string> Queries =>
        new()
        {
            { "SELECT v.next, v.id, v.label, v.note FROM written.v_plain v", "nnn?" },
            // The side a join inside the view extends, through a second view too.
            { "SELECT v.other, v.id FROM written.v_left v", "?n" },
            { "SELECT v.other, v.id FROM written.v_view v", "?n" },
            // A WHERE that makes the view's join inner.
            { "SELECT v.other, v.id FROM written.v_left v WHERE v.other = 'b'", "nn" },
            // An aggregate of a nullable column, and a view's expression that may be NULL.
            { "SELECT v.m, v.c FROM written.v_group v", "?n" },
            { "SELECT v.x, v.id FROM written.v_null v", "?n" },
            // A branch of a set operation that holds NULL.
            { "SELECT v.id, 1 AS one FROM written.v_union v", "??" },
            // A join of the query that extends the view.
            { "SELECT v.id, v.label FROM written.src s LEFT JOIN written.v_plain v ON v.id = -s.id", "??" },
            { "SELECT v.id, s.id AS s FROM written.v_plain v FULL JOIN written.src s ON v.id = -s.id", "??" },
            // A materialized view is a stored table that records no NOT NULL.
            { "SELECT m.id, 1 AS one FROM written.mv m", "?n" },
            // A statement that writes through a view returns what the write made of the row.
            { "INSERT INTO written.v_plain (id, label) VALUES (9, 'x') RETURNING label, id", "??" },
            { "UPDATE written.v_plain v SET note = NULL RETURNING v.note, v.id", "??" },
        };

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task DescribeAsync_ViewColumn_IsProvedOnlyWhereItHoldsNoNull(string sql, string expected)
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

    // A view's whole column list keeps the model named after the view, so none of its columns is proved.
    [Fact]
    public async Task DescribeAsync_WholeColumnListOfAView_KeepsItsMatchAndItsAnswers()
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);

            var description = await Sessions.ShouldDescribeAsync(
                postgres.ConnectionString,
                DescribeRequests.Of("SELECT * FROM written.v_plain")
            );

            description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
            description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
            description.Columns.ShouldNotBeNull().ShouldAllBe(static column => column.Nullable == null);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }

    // A list is described as one value, which the planner reads as a strict <> and so makes the view's join inner, but
    // an empty list keeps the rows the join extended.  The query's text shows no join to gate it.
    [Theory]
    [InlineData("SELECT v.other, v.id FROM written.v_left v WHERE v.other NOT IN @ids")]
    [InlineData("SELECT v.other || 'x' AS y, v.id FROM written.v_left v WHERE v.other NOT IN @ids")]
    public async Task DescribeAsync_NegatedListThatReducesAViewsJoin_IsNotProved(string sql)
    {
        try
        {
            await postgres.ExecuteAsync(ScratchSchema.Written);

            var description = await Sessions.ShouldDescribeAsync(
                postgres.ConnectionString,
                DescribeRequests.Planned("postgres", sql)
            );

            description.Columns.ShouldNotBeNull().Select(static column => column.Nullable).ShouldNotContain(false);
        }
        finally
        {
            await postgres.ExecuteAsync(ScratchSchema.DropWritten);
        }
    }
}
