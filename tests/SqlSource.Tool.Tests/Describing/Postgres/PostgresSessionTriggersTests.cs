using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: "triggers", asked only when a value an INSERT writes would change an
// answer.  A call the script does not hold is a CaptureMissException, so a describe that asks it needlessly fails.
public sealed class PostgresSessionTriggersTests
{
    private const string Sql = "INSERT INTO t (id, note) VALUES (1, 'x') RETURNING note";

    private static readonly PostgresTableRow Target = PostgresTables.TableOf(
        16810,
        "t",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "note", PostgresRows.Text.Oid)
    );

    private static readonly PostgresColumnRow Note = PostgresTables.ColumnOf(
        0,
        "note",
        PostgresRows.Text.Oid,
        Target,
        2
    );

    private static async Task<SidecarColumn> DescribeAsync(string written, PostgresTriggersAnswer? answer)
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, Sql, [], new([], null));
        _ = script.Columns(1, 0, Note);
        _ = script.Tables([Target.Oid], Target);
        _ = script.Resolves([PostgresRows.Text.Oid], PostgresRows.Text);
        _ = script.Checks(Sql, [], Note);
        _ = script.Explains(1, 0, PostgresScript.PlanOf(Insert("t", ["note"], "1", written)));
        if (answer is not null)
        {
            _ = script.Triggers(new PostgresRelationName("public", "t"), answer);
        }

        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Listed(Sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DescribeAsync_WrittenValueAndNoTrigger_IsProved()
    {
        var column = await DescribeAsync("'x'::text", new PostgresTriggersAnswer(false, null));

        column.Nullable.ShouldBe(false);
        column.NullableSource.ShouldBe(SidecarValues.NullableSource.Expression);
    }

    [Fact]
    public async Task DescribeAsync_WrittenValueAndATrigger_KeepsTheCatalog()
    {
        var column = await DescribeAsync("'x'::text", new PostgresTriggersAnswer(true, null));

        column.Nullable.ShouldBe(true);
        column.NullableSource.ShouldBe(SidecarValues.NullableSource.Catalog);
    }

    [Fact]
    public async Task DescribeAsync_TriggersRefused_KeepsTheCatalog()
    {
        var refused = new PostgresServerError("42501", "permission denied", null, null, null);

        var column = await DescribeAsync("'x'::text", new PostgresTriggersAnswer(null, refused));

        column.Nullable.ShouldBe(true);
        column.NullableSource.ShouldBe(SidecarValues.NullableSource.Catalog);
    }

    // No answer of "triggers" can change a value that is not proved, so it is not asked.
    [Fact]
    public async Task DescribeAsync_ValueNotProved_DoesNotAskTriggers()
    {
        var column = await DescribeAsync("NULL::text", null);

        column.Nullable.ShouldBe(true);
    }
}
