using Shouldly;
using SqlSource.Tool.Describing.Postgres.Statements;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Statements;

// The columns of a VALUES statement whose every row holds a literal: anything else in a cell may be NULL or is not
// read.
public sealed class ValuesStatementTests
{
    [Theory]
    [InlineData("VALUES (1)", new[] { true })]
    [InlineData("values (1, 'a'), (2, 'b')", new[] { true, true })]
    [InlineData(
        "VALUES (1.5, -2, 3, .5, 1., 1e5, TRUE, false)",
        new[] { true, true, true, true, true, true, true, true }
    )]
    [InlineData("-- name: X\nVALUES /* c */ (1), -- d\n(2);", new[] { true })]
    [InlineData("VALUES ('it''s')", new[] { true })]
    [InlineData("VALUES (1) ORDER BY 1", new[] { true })]
    [InlineData("VALUES (1) LIMIT 1", new[] { true })]
    [InlineData("VALUES (1) OFFSET 1", new[] { true })]
    [InlineData("VALUES (1) FETCH FIRST 1 ROW ONLY", new[] { true })]
    public void NeverNullColumns_RowsOfLiterals_AreEachTrue(string sql, bool[] expected) =>
        ValuesStatement.NeverNullColumns(sql).ShouldBe(expected);

    // One row that holds anything but a literal keeps the column from counting, and the others count.
    [Theory]
    [InlineData("VALUES (1, 'a'), (NULL, 'b')", new[] { false, true })]
    [InlineData("VALUES (null)", new[] { false })]
    [InlineData("VALUES (@p, 1)", new[] { false, true })]
    [InlineData("VALUES (1::int, 1)", new[] { false, true })]
    [InlineData("VALUES (1 + 1, 1)", new[] { false, true })]
    [InlineData("VALUES ((1), 1)", new[] { false, true })]
    [InlineData("VALUES (f(1, 2), 1)", new[] { false, true })]
    [InlineData("VALUES (E'a', 1)", new[] { false, true })]
    [InlineData("VALUES (U&'a', 1)", new[] { false, true })]
    [InlineData("VALUES ('a' 'b', 1)", new[] { false, true })]
    [InlineData("VALUES ($$a$$, 1)", new[] { false, true })]
    [InlineData("VALUES (\"a\", 1)", new[] { false, true })]
    [InlineData("VALUES (+1, 1)", new[] { false, true })]
    [InlineData("VALUES (- - 1, 1)", new[] { false, true })]
    [InlineData("VALUES (-'1', 1)", new[] { false, true })]
    [InlineData("VALUES (1.2.3, 1)", new[] { false, true })]
    [InlineData("VALUES (1e, 1)", new[] { false, true })]
    [InlineData("VALUES (0x10, 1)", new[] { false, true })]
    [InlineData("VALUES (., 1)", new[] { false, true })]
    [InlineData("VALUES (DEFAULT, 1)", new[] { false, true })]
    public void NeverNullColumns_CellThatIsNoLiteral_IsFalse(string sql, bool[] expected) =>
        ValuesStatement.NeverNullColumns(sql).ShouldBe(expected);

    // No VALUES alone: another statement, rows of two lengths, a row that does not close, or something after the rows.
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("(VALUES (1))")]
    [InlineData("WITH v AS (SELECT 1) VALUES (1)")]
    [InlineData("VALUES")]
    [InlineData("VALUES 1")]
    [InlineData("VALUES ()")]
    [InlineData("VALUES (1,)")]
    [InlineData("VALUES (,1)")]
    [InlineData("VALUES (1")]
    [InlineData("VALUES (1), (2, 3)")]
    [InlineData("VALUES (1),")]
    [InlineData("VALUES (1) UNION ALL SELECT NULL")]
    [InlineData("VALUES (1) EXCEPT VALUES (2)")]
    [InlineData("VALUES (1); SELECT 2")]
    public void NeverNullColumns_NoValuesAlone_IsNull(string sql) =>
        ValuesStatement.NeverNullColumns(sql).ShouldBeNull();
}
