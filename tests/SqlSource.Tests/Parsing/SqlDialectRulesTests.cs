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

        var rules = dialects.Select(dialect => SqlDialectRules.For(dialect)).ToArray();

        rules.Distinct().Count().ShouldBe(dialects.Length);
        rules.ShouldBe(dialects.Select(dialect => SqlDialectRules.For(dialect)));
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

    [Fact]
    public void For_EachSetOfOptions_GivesMySqlAndMariaDbTheirOwnSharedInstance()
    {
        var choices = MySqlFamilyChoices();

        var rules = choices.Select(choice => SqlDialectRules.For(choice)).ToArray();

        rules.Distinct().Count().ShouldBe(choices.Length);
        rules.ShouldBe(choices.Select(choice => SqlDialectRules.For(choice)));
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None))
            .ShouldBeSameAs(SqlDialectRules.MySql);
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.None))
            .ShouldBeSameAs(SqlDialectRules.MariaDb);
    }

    // An option changes how a quoted region ends, and nothing about comments, hints or what opens a region.
    [Fact]
    public void For_AnySetOfOptions_KeepsTheCommentRulesAndTheOpenersOfTheDialect()
    {
        foreach (var choice in MySqlFamilyChoices())
        {
            var rules = SqlDialectRules.For(choice);
            var isMariaDb = choice.Dialect == SqlDialect.MariaDb;

            rules.NestedComments.ShouldBeFalse(choice.ToString());
            rules.DashNeedsWhitespace.ShouldBeTrue(choice.ToString());
            rules.HashComments.ShouldBeTrue(choice.ToString());
            rules.LineHints.ShouldBeFalse(choice.ToString());
            rules.MariaDbHints.ShouldBe(isMariaDb, choice.ToString());
            (rules.ReaderFor('$') is null).ShouldBe(isMariaDb, choice.ToString());
            _ = rules.ReaderFor('\'').ShouldNotBeNull(choice.ToString());
            _ = rules.ReaderFor('"').ShouldNotBeNull(choice.ToString());
            _ = rules.ReaderFor('`').ShouldNotBeNull(choice.ToString());
        }
    }

    [Fact]
    public void For_OptionsOfADialectThatHasNone_AreIgnored() =>
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.PostgreSql, SqlDialectOptions.AnsiQuotes))
            .ShouldBeSameAs(SqlDialectRules.PostgreSql);

    // A value that no name gives must not throw inside the compiler if it ever arrives.
    [Fact]
    public void For_OptionsThatAreNotDefined_AreIgnored() =>
        SqlDialectRules
            .For(new SqlDialectChoice(SqlDialect.MySql, (SqlDialectOptions)4))
            .ShouldBeSameAs(SqlDialectRules.MySql);

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
    [InlineData("Ansi", "None")]
    [InlineData("SqlServer", "None")]
    [InlineData("PostgreSql", "AcrossLineComments")]
    [InlineData("MySql", "None")]
    [InlineData("MariaDb", "None")]
    [InlineData("Sqlite", "None")]
    [InlineData("Oracle", "None")]
    public void StringContinuation_OfEachDialect_IsTheRowOfTheTable(string dialect, string expected) =>
        SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect)).StringContinuation.ToString().ShouldBe(expected);

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

    private static SqlDialectChoice[] MySqlFamilyChoices()
    {
        SqlDialect[] dialects = [SqlDialect.MySql, SqlDialect.MariaDb];
        SqlDialectOptions[] options =
        [
            SqlDialectOptions.None,
            SqlDialectOptions.AnsiQuotes,
            SqlDialectOptions.NoBackslashEscapes,
            SqlDialectOptions.AnsiQuotes | SqlDialectOptions.NoBackslashEscapes,
        ];

        return [.. dialects.SelectMany(dialect => options.Select(option => new SqlDialectChoice(dialect, option)))];
    }
}
