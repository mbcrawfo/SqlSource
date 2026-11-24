using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The columns a WHERE keeps from being NULL: each shape it reads, beside the near misses that must keep nothing.
public sealed class RowFilterTests
{
    [Theory]
    [InlineData("x IS NOT NULL", "x")]
    [InlineData("o.x IS NOT NULL", "o.x")]
    [InlineData("[o].[x] IS NOT NULL", "o.x")]
    [InlineData("x = 1", "x")]
    [InlineData("1 = x", "x")]
    [InlineData("x = @p", "x")]
    [InlineData("@p = x", "x")]
    [InlineData("x <> @p", "x")]
    [InlineData("x != 'a'", "x")]
    [InlineData("x < 1", "x")]
    [InlineData("x <= 1", "x")]
    [InlineData("x > 1", "x")]
    [InlineData("x >= 1", "x")]
    [InlineData("x !< 1", "x")]
    [InlineData("x !> 1", "x")]
    [InlineData("x = y", "x|y")]
    [InlineData("x = (SELECT MAX(id) FROM t)", "x")]
    [InlineData("x LIKE 'a%'", "x")]
    [InlineData("x LIKE @p ESCAPE '!'", "x")]
    [InlineData("x NOT LIKE 'a%'", "x")]
    [InlineData("x = 1 AND y IS NOT NULL", "x|y")]
    [InlineData("(x = 1) AND (y > 2)", "x|y")]
    [InlineData("(x = 1 AND y > 2)", "x|y")]
    [InlineData("d BETWEEN 1 AND 2 AND x = 1", "x")]
    [InlineData("x = 1 AND d NOT BETWEEN 1 AND 2 AND y = 2", "x|y")]
    [InlineData("(a = 1 OR b = 2) AND x = 1", "x")]
    [InlineData("CASE WHEN a = 1 OR b = 2 THEN 1 END = 1 AND x = 1", "x")]
    public void Read_Conjuncts_KeepTheirColumns(string where, string columns) => Columns(where).ShouldBe(columns);

    [Theory]
    [InlineData("x IS NULL")]
    [InlineData("x = 1 OR y = 2")]
    [InlineData("x = 1 AND y = 2 OR z = 3")]
    [InlineData("z = 3 OR x = 1 AND y = 2")]
    [InlineData("(x = 1 OR y = 2)")]
    [InlineData("NOT x = 1")]
    [InlineData("NOT x IS NOT NULL")]
    [InlineData("NOT (x IS NOT NULL)")]
    [InlineData("NOT x LIKE 'a'")]
    [InlineData("NOT x NOT LIKE 'a'")]
    [InlineData("x = NULL")]
    [InlineData("NULL = x")]
    [InlineData("x <> (NULL)")]
    [InlineData("x > ALL (SELECT id FROM t WHERE 1 = 0)")]
    [InlineData("x = ANY (SELECT id FROM t)")]
    [InlineData("x <> SOME (SELECT id FROM t)")]
    [InlineData("x + 1 = 2")]
    [InlineData("ISNULL(x, 0) = 0")]
    [InlineData("COALESCE(x, 0) >= 0")]
    [InlineData("x COLLATE Latin1_General_BIN = 'a'")]
    [InlineData("-x = 1")]
    [InlineData("x IN (1, 2)")]
    [InlineData("EXISTS (SELECT 1 FROM t WHERE t.x = 1)")]
    [InlineData("x BETWEEN 1 AND 2")]
    [InlineData("'a' LIKE x")]
    [InlineData("CASE WHEN x = 1 THEN 1 END = 1")]
    [InlineData("(SELECT x FROM t) = 1")]
    public void Read_NearMiss_KeepsNothing(string where) => Columns(where).ShouldBe("");

    [Fact]
    public void Read_NoWhere_KeepsNothing()
    {
        var tokens = TsqlTokens.Read("SELECT a FROM t");

        RowFilter.Read(tokens, null).Columns.ShouldBeEmpty();
    }

    // A name written otherwise may be another column under a case-sensitive collation, and a name with another
    // qualifier another table's.
    [Theory]
    [InlineData("x", true)]
    [InlineData("[x]", true)]
    [InlineData("X", false)]
    [InlineData("o.x", false)]
    public void Keeps_ComparesThePartsOrdinally(string name, bool kept)
    {
        var filtered = TsqlTokens.Read("SELECT a FROM t WHERE x = 1");
        var filter = RowFilter.Read(filtered, SelectStatement.Read(filtered).ShouldNotBeNull().Body.Where);
        var written = TsqlTokens.Read(name);

        filter.Keeps(RowFilter.NameOf(written, new TokenRange(0, written.Count)).ShouldNotBeNull()).ShouldBe(kept);
    }

    private static string Columns(string where)
    {
        var tokens = TsqlTokens.Read($"SELECT a FROM t WHERE {where}");
        var filter = RowFilter.Read(tokens, SelectStatement.Read(tokens).ShouldNotBeNull().Body.Where);
        return string.Join('|', filter.Columns.Select(static parts => string.Join('.', parts)));
    }
}
