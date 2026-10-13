using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The gate: whether a query holds a word that can make a NOT NULL column of a table come back NULL.  It reads
// words and no structure, so it may say yes where there is no join, and never says no where there is one.
public class OuterJoinGateTests
{
    [Theory]
    [InlineData("LEFT")]
    [InlineData("RIGHT")]
    [InlineData("FULL")]
    [InlineData("OUTER")]
    [InlineData("ROLLUP")]
    [InlineData("CUBE")]
    [InlineData("GROUPING")]
    [InlineData("OLD")]
    [InlineData("NEW")]
    [InlineData("MERGE")]
    public void IsGated_EachWordInAnyCase_IsGated(string word)
    {
        // CA1308 asks for upper case in a string, and the lower case here is the point of the test.
        var lower = string.Concat(word.Select(char.ToLowerInvariant));
        var mixed = char.ToUpperInvariant(word[0]) + lower[1..];

        OuterJoinGate.IsGated($"SELECT 1 FROM a {word} b").ShouldBeTrue();
        OuterJoinGate.IsGated($"SELECT 1 FROM a {lower} b").ShouldBeTrue();
        OuterJoinGate.IsGated($"SELECT 1 FROM a {mixed} b").ShouldBeTrue();
    }

    [Theory]
    [InlineData("SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY ROLLUP (status)")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY GROUPING SETS ((status), ())")]
    [InlineData("SELECT status, grouping(status) FROM users GROUP BY CUBE(status)")]
    // A function, and no join: the gate cannot tell, and the walk finds nothing to null-extend.
    [InlineData("SELECT left(name, 3) FROM users")]
    [InlineData("left")]
    [InlineData("SELECT 1 FROM a\nLEFT\nJOIN b ON true")]
    [InlineData("SELECT 1 FROM a NATURAL FULL JOIN b")]
    [InlineData("SELECT 1 FROM \"a\"LEFT JOIN b ON true")]
    // The old and the new row of a RETURNING list, and a MERGE: NULL with no join in the plan.
    [InlineData("INSERT INTO t (id) VALUES (1) RETURNING old.id, new.id")]
    [InlineData("DELETE FROM t WHERE id = 1 RETURNING old.id")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS o, NEW AS n) o.v, n.v")]
    [InlineData("MERGE INTO t USING s ON t.id = s.id WHEN MATCHED THEN DELETE RETURNING s.id")]
    public void IsGated_QueryThatHoldsAWord_IsGated(string sql) => OuterJoinGate.IsGated(sql).ShouldBeTrue();

    [Theory]
    [InlineData("SELECT id FROM users")]
    [InlineData("SELECT 1 FROM a JOIN b ON true")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY status")]
    // Inside a string, a comment, a hint, a quoted identifier and a dollar-quoted string.
    [InlineData("SELECT 'left join' FROM users")]
    [InlineData("SELECT id FROM users -- left join")]
    [InlineData("SELECT id /* FULL OUTER */ FROM users")]
    [InlineData("SELECT /*+ rollup */ id FROM users")]
    [InlineData("SELECT \"left\", \"FULL\" FROM users")]
    [InlineData("SELECT $$ left $$ FROM users")]
    [InlineData("SELECT $tag$ cube $tag$ FROM users")]
    // A longer word that holds one.
    [InlineData("SELECT leftover FROM users")]
    [InlineData("SELECT x_left, left_x, left1, a.fullname FROM users")]
    [InlineData("SELECT groupings, cubed, outermost, rights FROM users")]
    [InlineData("SELECT renewal, older, newest, merged, new_value, old_id FROM t")]
    [InlineData("SELECT \"new\", \"old\" FROM t")]
    [InlineData("SELECT 'merge' FROM t")]
    // A parameter is no word of the SQL.
    [InlineData("SELECT id FROM users WHERE id = @left")]
    [InlineData("")]
    public void IsGated_QueryThatHoldsNoWord_IsNotGated(string sql) => OuterJoinGate.IsGated(sql).ShouldBeFalse();
}
