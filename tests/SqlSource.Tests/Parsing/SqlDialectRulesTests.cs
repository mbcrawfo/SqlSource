using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// What each dialect makes of each construct is pinned through the lexer, in SqlLexerTests.  These are about the table
// itself.
public sealed class SqlDialectRulesTests
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
        SqlDialectRules.For(SqlDialect.CockroachDb).ShouldBeSameAs(SqlDialectRules.CockroachDb);
    }

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
    [InlineData("CockroachDb", "'\"$")]
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
    [InlineData("CockroachDb", true, false, false, false, false)]
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
    [InlineData("CockroachDb", "AcrossWhitespace")]
    public void StringContinuation_OfEachDialect_IsTheRowOfTheTable(string dialect, string expected) =>
        SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect)).StringContinuation.ToString().ShouldBe(expected);

    // T-SQL takes "@", "#" and "$" in a name and refuses other currency signs, such as "€"; the others read every
    // character above U+007F as a letter, and take only "$" of the three.
    [Theory]
    [InlineData("Ansi", false)]
    [InlineData("SqlServer", true)]
    [InlineData("PostgreSql", false)]
    [InlineData("MySql", false)]
    [InlineData("MariaDb", false)]
    [InlineData("Sqlite", false)]
    [InlineData("Oracle", false)]
    [InlineData("CockroachDb", false)]
    public void IsNameCharacter_OfEachDialect_ReadsNamesAsSqlServerOrAsPostgreSqlDoes(string dialect, bool sqlServer)
    {
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));

        rules.IsNameCharacter('a').ShouldBeTrue();
        rules.IsNameCharacter('_').ShouldBeTrue();
        rules.IsNameCharacter('$').ShouldBeTrue();
        rules.IsNameCharacter(' ').ShouldBeFalse();
        rules.IsNameCharacter('@').ShouldBe(sqlServer);
        rules.IsNameCharacter('#').ShouldBe(sqlServer);
        rules.IsNameCharacter('\u20AC').ShouldBe(!sqlServer);
    }

    // T-SQL reads a variable's name by its rule for names: a combining mark, a format character, a letter number, a
    // connector, "$", "#" and "@" included.  The others take letters, digits and "_".
    [Theory]
    [InlineData("Ansi", false)]
    [InlineData("SqlServer", true)]
    [InlineData("PostgreSql", false)]
    [InlineData("MySql", false)]
    [InlineData("MariaDb", false)]
    [InlineData("Sqlite", false)]
    [InlineData("Oracle", false)]
    [InlineData("CockroachDb", false)]
    public void IsParameterNameCharacter_OfEachDialect_ReadsTheServersRuleUnderSqlServerAlone(
        string dialect,
        bool sqlServer
    )
    {
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));

        rules.IsParameterNameCharacter('a').ShouldBeTrue();
        rules.IsParameterNameCharacter('1').ShouldBeTrue();
        rules.IsParameterNameCharacter('_').ShouldBeTrue();
        rules.IsParameterNameCharacter('\u0301').ShouldBe(sqlServer);
        rules.IsParameterNameCharacter('\u200C').ShouldBe(sqlServer);
        rules.IsParameterNameCharacter('\u2160').ShouldBe(sqlServer);
        rules.IsParameterNameCharacter('\u203F').ShouldBe(sqlServer);
        rules.IsParameterNameCharacter('$').ShouldBe(sqlServer);
        rules.IsParameterNameCharacter('#').ShouldBe(sqlServer);
        rules.IsParameterNameCharacter('@').ShouldBe(sqlServer);
        rules.IsParameterNameCharacter('\u00B7').ShouldBeFalse();
        rules.IsParameterNameCharacter('\u20AC').ShouldBeFalse();
        rules.IsParameterNameCharacter(' ').ShouldBeFalse();
    }

    // A name never starts with the prefix, though SQL Server takes "@" inside one.
    [Theory]
    [InlineData("@a$b;", 4)]
    [InlineData("@@x", 1)]
    [InlineData("@", 1)]
    public void FindParameterNameEnd_AfterThePrefix_IsTheEndOfTheName(string text, int expected) =>
        SqlDialectRules.SqlServer.FindParameterNameEnd(text, 1, text.Length).ShouldBe(expected);

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

    [Fact]
    public void ParameterPrefix_EveryDialectAndOptionSet_IsTheAtSign()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            foreach (
                var options in new[]
                {
                    SqlDialectOptions.None,
                    SqlDialectOptions.AnsiQuotes,
                    SqlDialectOptions.NoBackslashEscapes,
                }
            )
            {
                var rules = SqlDialectRules.For(new SqlDialectChoice(dialect, options));

                rules.ParameterPrefix.ShouldBe('@');
                rules.FindStarter("abc @p", 0).ShouldBe(4);
            }
        }
    }

    [Fact]
    public void Dialect_OfTheRulesOfAChoice_IsThatChoicesDialect()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            SqlDialectRules.For(new SqlDialectChoice(dialect, SqlDialectOptions.None)).Dialect.ShouldBe(dialect);
        }
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
