using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// What each dialect makes of each construct is pinned through the lexer, in SqlLexerTests.  These are about the table
// itself.
public class SqlDialectRulesTests
{
    [Fact]
    public void For_EveryDialect_GivesItsOwnSharedInstance()
    {
        var dialects = Enum.GetValues<SqlDialect>();

        var rules = dialects.Select(SqlDialectRules.For).ToArray();

        rules.Distinct().Count().ShouldBe(dialects.Length);
        rules.ShouldBe(dialects.Select(SqlDialectRules.For));
        SqlDialectRules.For(SqlDialect.Ansi).ShouldBeSameAs(SqlDialectRules.Ansi);
        SqlDialectRules.For(SqlDialect.SqlServer).ShouldBeSameAs(SqlDialectRules.SqlServer);
        SqlDialectRules.For(SqlDialect.PostgreSql).ShouldBeSameAs(SqlDialectRules.PostgreSql);
        SqlDialectRules.For(SqlDialect.MySql).ShouldBeSameAs(SqlDialectRules.MySql);
        SqlDialectRules.For(SqlDialect.MariaDb).ShouldBeSameAs(SqlDialectRules.MariaDb);
        SqlDialectRules.For(SqlDialect.Sqlite).ShouldBeSameAs(SqlDialectRules.Sqlite);
        SqlDialectRules.For(SqlDialect.Oracle).ShouldBeSameAs(SqlDialectRules.Oracle);
    }

    // A value that is not a dialect cannot come from a name, and must not throw inside the compiler if it ever does.
    [Fact]
    public void For_ValueThatIsNotADialect_GivesTheAnsiRules() =>
        SqlDialectRules.For((SqlDialect)99).ShouldBeSameAs(SqlDialectRules.Ansi);

    [Theory]
    [InlineData("Ansi", "'\"`$")]
    [InlineData("SqlServer", "'\"[")]
    [InlineData("PostgreSql", "'\"$")]
    [InlineData("MySql", "'\"`$")]
    [InlineData("MariaDb", "'\"`")]
    [InlineData("Sqlite", "'\"`[")]
    [InlineData("Oracle", "'\"")]
    public void ReaderFor_Character_HasAReaderOnlyWhereTheDialectOpensAQuotedRegion(string dialect, string openers)
    {
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));

        var found = Enumerable
            .Range(0, char.MaxValue + 1)
            .Select(value => (char)value)
            .Where(value => rules.ReaderFor(value) is not null);

        new string([.. found]).ShouldBe(new string([.. openers.Order()]));
    }

    [Theory]
    [InlineData("Ansi", true, false, false, false, false)]
    [InlineData("SqlServer", true, false, false, false, false)]
    [InlineData("PostgreSql", true, false, false, false, false)]
    [InlineData("MySql", false, true, true, false, false)]
    [InlineData("MariaDb", false, true, true, false, true)]
    [InlineData("Sqlite", false, false, false, false, false)]
    [InlineData("Oracle", false, false, false, true, false)]
    public void CommentRules_OfEachDialect_AreTheRowOfTheTable(
        string dialect,
        bool nestedComments,
        bool dashNeedsWhitespace,
        bool hashComments,
        bool lineHints,
        bool mariaDbHints
    )
    {
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));

        rules.NestedComments.ShouldBe(nestedComments);
        rules.DashNeedsWhitespace.ShouldBe(dashNeedsWhitespace);
        rules.HashComments.ShouldBe(hashComments);
        rules.LineHints.ShouldBe(lineHints);
        rules.MariaDbHints.ShouldBe(mariaDbHints);
    }

    [Theory]
    [InlineData("SELECT a, b FROM t WHERE x = 1", 0, -1)]
    [InlineData("", 0, -1)]
    [InlineData("a - b", 0, 2)]
    [InlineData("a / b", 0, 2)]
    [InlineData("a # b", 0, 2)]
    [InlineData("a 'b'", 0, 2)]
    [InlineData("a \"b\"", 0, 2)]
    [InlineData("a [b]", 0, 2)]
    [InlineData("a - b - c", 3, 6)]
    [InlineData("a - b", 5, -1)]
    public void FindStarter_Text_FindsTheNextCharacterThatCanStartACommentOrAQuotedRegion(
        string text,
        int start,
        int expected
    ) => SqlDialectRules.SqlServer.FindStarter(text, start).ShouldBe(expected);

    // A character that opens nothing in a dialect is plain text there, and is skipped with the rest.
    [Fact]
    public void FindStarter_OpenerOfAnotherDialect_IsSkipped()
    {
        SqlDialectRules.SqlServer.FindStarter("a `b` $$ c", 0).ShouldBe(-1);
        SqlDialectRules.Oracle.FindStarter("a [b] `c` $$ d", 0).ShouldBe(-1);
        SqlDialectRules.Ansi.FindStarter("a [b] `c`", 0).ShouldBe(6);
    }
}
