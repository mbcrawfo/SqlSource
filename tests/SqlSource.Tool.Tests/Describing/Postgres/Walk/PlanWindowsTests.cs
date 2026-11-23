using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// A window's frame as PostgreSQL 18 prints it: an aggregate over it is read only when the frame holds the current row.
public sealed class PlanWindowsTests
{
    [Theory]
    [InlineData("w1 AS (PARTITION BY t.k ORDER BY t.id)")]
    [InlineData("w1 AS (PARTITION BY t.k)")]
    [InlineData("w1 AS ()")]
    [InlineData("w1 AS (ORDER BY t.id ROWS UNBOUNDED PRECEDING)")]
    [InlineData("w1 AS (ORDER BY t.id RANGE CURRENT ROW)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)")]
    [InlineData("w1 AS (ORDER BY t.id RANGE BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING)")]
    [InlineData("w1 AS (ORDER BY t.id GROUPS BETWEEN CURRENT ROW AND CURRENT ROW)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING)")]
    [InlineData("w1 AS (ORDER BY (t.a = 'ROWS 1 FOLLOWING'::text))")]
    [InlineData("w1 AS (ORDER BY lower((t.rows)::text))")]
    public void HoldsCurrentRow_FrameThatHoldsIt_IsTrue(string window) =>
        PlanWindows.HoldsCurrentRow(window, "w1").ShouldBeTrue();

    [Theory]
    [InlineData("w1 AS (ORDER BY t.id ROWS BETWEEN '1'::bigint FOLLOWING AND '2'::bigint FOLLOWING)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS BETWEEN '2'::bigint PRECEDING AND '1'::bigint PRECEDING)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS BETWEEN '1'::bigint PRECEDING AND CURRENT ROW)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS BETWEEN CURRENT ROW AND '1'::bigint FOLLOWING)")]
    [InlineData("w1 AS (ORDER BY t.id RANGE BETWEEN 1 PRECEDING AND 1 FOLLOWING)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS '1'::bigint PRECEDING)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING EXCLUDE CURRENT ROW)")]
    [InlineData("w1 AS (ORDER BY t.id RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW EXCLUDE GROUP)")]
    [InlineData("w1 AS (ORDER BY t.id RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW EXCLUDE TIES)")]
    [InlineData("w1 AS (ORDER BY t.id ROWS)")]
    public void HoldsCurrentRow_FrameThatMayNotHoldIt_IsFalse(string window) =>
        PlanWindows.HoldsCurrentRow(window, "w1").ShouldBeFalse();

    // The definition must be the window the aggregate names, and whole.
    [Theory]
    [InlineData(null, "w1")]
    [InlineData("w2 AS (ORDER BY t.id)", "w1")]
    [InlineData("w1 IS (ORDER BY t.id)", "w1")]
    [InlineData("w1 AS ORDER BY t.id", "w1")]
    [InlineData("w1 AS (ORDER BY t.id", "w1")]
    [InlineData("w1 AS (ORDER BY t.id))", "w1")]
    [InlineData("w1", "w1")]
    public void HoldsCurrentRow_NoDefinitionOfTheWindow_IsFalse(string? window, string name) =>
        PlanWindows.HoldsCurrentRow(window, name).ShouldBeFalse();

    [Fact]
    public void HoldsCurrentRow_QuotedName_IsComparedAsPrinted()
    {
        PlanWindows.HoldsCurrentRow("\"W x\" AS (ORDER BY t.id)", "\"W x\"").ShouldBeTrue();
        PlanWindows.HoldsCurrentRow("\"W x\" AS (ORDER BY t.id)", "w").ShouldBeFalse();
    }
}
