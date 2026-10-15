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

    // PostgreSQL reads § as part of the alias, so the $x$ opens no quote and the LEFT is a word of the query.  Read
    // as a quote, the $x$ would hide the join in a string that runs to the comment.
    [Fact]
    public void IsGated_AliasThatEndsLikeADollarQuote_IsGated() =>
        OuterJoinGate
            .IsGated("SELECT o.total AS §$x$ FROM users u LEFT JOIN orders o ON o.user_id = u.id --$x$")
            .ShouldBeTrue();

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
    [InlineData("SELECT 'merge' FROM t")]
    // Only OLD and NEW are names the server resolves when quoted: a quoted word of the others does not count.
    [InlineData("SELECT \"left\" FROM t")]
    // A parameter is no word of the SQL.
    [InlineData("SELECT id FROM users WHERE id = @left")]
    [InlineData("")]
    public void IsGated_QueryThatHoldsNoWord_IsNotGated(string sql) => OuterJoinGate.IsGated(sql).ShouldBeFalse();

    [Theory]
    [MemberData(nameof(QuotedOldOrNew))]
    public void IsGated_QuotedOldOrNewOrUnicodeEscapedIdentifier_IsGated(string sql) =>
        OuterJoinGate.IsGated(sql).ShouldBeTrue();

    [Theory]
    [MemberData(nameof(NoQuotedOldOrNew))]
    public void IsGated_OldOrNewInAStringACommentOrAnotherQuotedName_IsNotGated(string sql) =>
        OuterJoinGate.IsGated(sql).ShouldBeFalse();

    // Whether a reference at a ModifyTable may be PostgreSQL 18's old or new row.
    [Theory]
    [InlineData("INSERT INTO t (id) VALUES (1) RETURNING old.id")]
    [InlineData("INSERT INTO t (id) VALUES (1) RETURNING NEW.id")]
    [InlineData("UPDATE t SET v = 1 RETURNING WITH (OLD AS o) o.id")]
    public void NamesOldOrNew_QueryThatHoldsOldOrNew_NamesThem(string sql) =>
        OuterJoinGate.NamesOldOrNew(sql).ShouldBeTrue();

    [Theory]
    [MemberData(nameof(QuotedOldOrNew))]
    public void NamesOldOrNew_QuotedOldOrNewOrUnicodeEscapedIdentifier_NamesThem(string sql) =>
        OuterJoinGate.NamesOldOrNew(sql).ShouldBeTrue();

    [Theory]
    [MemberData(nameof(NoQuotedOldOrNew))]
    public void NamesOldOrNew_OldOrNewInAStringACommentOrAnotherQuotedName_DoesNotNameThem(string sql) =>
        OuterJoinGate.NamesOldOrNew(sql).ShouldBeFalse();

    [Theory]
    [InlineData("SELECT 1")]
    // Inside a string and a comment.
    [InlineData("SELECT 'old' FROM t")]
    [InlineData("SELECT id FROM t -- old")]
    // A longer word that holds one.
    [InlineData("SELECT bold, newest FROM t")]
    // The other words of the gate.
    [InlineData("SELECT a.id FROM a LEFT JOIN b ON true")]
    public void NamesOldOrNew_QueryThatHoldsNeither_DoesNotNameThem(string sql) =>
        OuterJoinGate.NamesOldOrNew(sql).ShouldBeFalse();

    // PostgreSQL 18 resolves "old" and "new" as the old and the new row of a RETURNING list, so a quoted identifier
    // of either name counts, in any case.  The tool does not decode the escapes of a Unicode-escaped identifier, so
    // any one counts.
    public static TheoryData<string> QuotedOldOrNew =>
        [
            "INSERT INTO t (id) VALUES (1) RETURNING \"old\".id",
            "INSERT INTO t (id) VALUES (1) RETURNING \"NEW\".id",
            "INSERT INTO t (id) VALUES (1) RETURNING \"Old\".id",
            "INSERT INTO t (id) VALUES (1) RETURNING U&\"\\006Fld\".id",
            "INSERT INTO t (id) VALUES (1) RETURNING U&\"!006Fld\" UESCAPE '!'",
            "INSERT INTO t (id) VALUES (1) RETURNING u&\"x\".id",
            // Wherever it stands, a table's alias included: the walk then maps only the target's alias.
            "SELECT \"new\", \"old\" FROM t",
            "SELECT \"old\".id FROM t \"old\"",
        ];

    // A string never counts, nor a comment, nor a quoted identifier of another name: "o""ld" is the name o"ld.
    public static TheoryData<string> NoQuotedOldOrNew =>
        [
            "SELECT 'old' FROM t",
            "SELECT E'new' FROM t",
            "SELECT U&'old' FROM t",
            "SELECT $$old$$ FROM t",
            "SELECT id FROM t -- \"old\"",
            "SELECT \"bold\" FROM t",
            "SELECT \"new_total\" FROM t",
            "SELECT \"o\"\"ld\" FROM t",
        ];
}
