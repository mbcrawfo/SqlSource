using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class SqlListUsesTests
{
    private const int Iterations = 20;

    [Theory]
    [InlineData("postgres", "id IN @ids", new[] { "List" })]
    [InlineData("postgres", "id not in @ids", new[] { "NegatedList" })]
    // A NOT is open at its depth, and in every parenthesis inside it, until an AND, an OR or a comma at that depth, or
    // the parenthesis that closes it.
    [InlineData("postgres", "id NOT /* c */ IN @ids", new[] { "NegatedList" })]
    [InlineData("postgres", "NOT id IN @ids", new[] { "NegatedList" })]
    [InlineData("postgres", "NOT (id IN @ids)", new[] { "NegatedList" })]
    [InlineData("postgres", "NOT (a = 1 AND (id IN @ids))", new[] { "NegatedList" })]
    [InlineData("postgres", "NOT EXISTS (SELECT 1 FROM t WHERE t.id IN @ids)", new[] { "NegatedList" })]
    [InlineData("postgres", "NOT a AND id IN @ids", new[] { "List" })]
    [InlineData("postgres", "NOT (a) OR id IN @ids", new[] { "List" })]
    [InlineData("postgres", "(NOT a) AND id IN @ids", new[] { "List" })]
    [InlineData("postgres", "f(NOT a, id IN @ids)", new[] { "List" })]
    [InlineData("postgres", "id NOT IN @a AND id IN @b", new[] { "NegatedList", "List" })]
    [InlineData("postgres", "id IN @a AND NOT id IN @b", new[] { "List", "NegatedList" })]
    // The NOT of IS NOT tests a value and negates nothing after it.
    [InlineData("postgres", "x IS NOT DISTINCT FROM id IN @ids", new[] { "List" })]
    // Only a NOT negates: a test of the truth of a list is read as a list (TD-0102).
    [InlineData("postgres", "(id IN @ids) IS FALSE", new[] { "List" })]
    [InlineData("postgres", "id IN /* c */ @ids", new[] { "List" })]
    [InlineData("postgres", "id IN\n-- c\n@ids", new[] { "List" })]
    [InlineData("postgres", "id IN /*+ hint */ @ids", new[] { "List" })]
    [InlineData("postgres", "id IN (@ids)", new[] { "Value" })]
    [InlineData("postgres", "a JOIN @t", new[] { "Value" })]
    [InlineData("postgres", "position('@' IN @email) > 0", new[] { "Value" })]
    [InlineData("postgres", "position('@' IN lower(@email))", new[] { "Value" })]
    [InlineData("postgres", "(id IN @ids)", new[] { "List" })]
    [InlineData("postgres", "@ids IS NULL OR id IN @ids", new[] { "NullTest", "List" })]
    // No "in" anywhere, so no use is a list and every use reads as a value: a null test matters only of a list.
    [InlineData("postgres", "@ids is not null", new[] { "Value" })]
    [InlineData("postgres", "id IN @ids OR @ids is not null", new[] { "List", "NullTest" })]
    [InlineData("postgres", "@ids ISNULL", new[] { "Value" })]
    [InlineData("postgres", "'IN' @x", new[] { "Value" })]
    [InlineData("postgres", "\"IN\" @x", new[] { "Value" })]
    [InlineData("mssql", "[IN] @x", new[] { "Value" })]
    // "#" is a name character of T-SQL, so "x#IN" is one word.
    [InlineData("mssql", "x#IN @y", new[] { "Value" })]
    [InlineData("postgres", "id IN @ids AND @other = 1", new[] { "List", "Value" })]
    // A ")" that closes nothing is passed over.
    [InlineData("postgres", "f(a)) AND id IN @ids", new[] { "List" })]
    // Only an IN directly inside the parentheses of position is that of the call.
    [InlineData("postgres", "position(a IN (SELECT x FROM t WHERE id IN @x))", new[] { "List" })]
    [InlineData("postgres", "id IN @ids OR @ids IS /* c */ NOT NULL", new[] { "List", "NullTest" })]
    [InlineData("postgres", "id IN {{t}} @ids", new[] { "Value" })]
    public void Find_Use_IsClassified(string dialect, string sql, string[] expected)
    {
        SqlDialectName.TryParse(dialect, out var choice).ShouldBeTrue();
        var rules = SqlDialectRules.For(choice);
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;
        var parameters = lexemes.Count(static lexeme => lexeme.Kind == SqlLexemeKind.Parameter);

        var uses = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules);

        // An empty result is how Find says that every use is a value.
        var read = uses.IsEmpty ? Enumerable.Repeat(SqlParameterUse.Value, parameters) : uses;
        read.Select(static use => use.ToString()).ShouldBe(expected);
    }

    // A query is a range of its file's lexemes, and only the uses of that range are given.
    [Fact]
    public void Find_RangeOfLexemes_GivesTheUsesOfThatRangeOnly()
    {
        const string Sql = "id IN @a; @b IS NULL OR id IN @c";
        var rules = SqlDialectRules.PostgreSql;
        var lexemes = SqlLexer.Lex(Sql, rules).Lexemes;
        lexemes.Count.ShouldBe(6);

        SqlListUses.Find(Sql, lexemes, 0, 2, rules).ShouldBe([SqlParameterUse.List]);
        SqlListUses.Find(Sql, lexemes, 2, 6, rules).ShouldBe([SqlParameterUse.NullTest, SqlParameterUse.List]);
        SqlListUses.Find(Sql, lexemes, 2, 2, rules).ShouldBeEmpty();
    }

    // A range starts where the lexemes of the query before it end, and an IN there is not the range's.  One that
    // starts the range is.
    [Fact]
    public void Find_RangeNextToAnIn_ReadsTheWordWithinTheRangeOnly()
    {
        const string Before = "id IN /* the query before ends here */ @x";
        var rules = SqlDialectRules.PostgreSql;
        var before = SqlLexer.Lex(Before, rules).Lexemes;
        before.Count.ShouldBe(4);

        SqlListUses.Find(Before, before, 0, 4, rules).ShouldBe([SqlParameterUse.List]);
        SqlListUses.Find(Before, before, 1, 4, rules).ShouldBeEmpty();

        const string Starts = "@a = 'x'IN @b";
        var starts = SqlLexer.Lex(Starts, rules).Lexemes;
        starts.Count.ShouldBe(5);

        SqlListUses.Find(Starts, starts, 3, 5, rules).ShouldBe([SqlParameterUse.List]);
        SqlListUses.Find(Starts, starts, 0, 3, rules).ShouldBeEmpty();
    }

    // A null test matters only of a list, so a query that holds IN but no list use gives no uses.
    [Theory]
    [InlineData("id IN (@ids) OR @ids IS NULL")]
    [InlineData("position('@' IN @email) > 0")]
    public void Find_InWithoutAListUse_IsEmpty(string sql)
    {
        var rules = SqlDialectRules.PostgreSql;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules).ShouldBeEmpty();
    }

    // Most queries hold no word IN, and a search says so: the walk, and its result, are never made.  The IN of JOIN,
    // INTO and INT is not the word.
    [Fact]
    public void Find_TextWithoutIn_IsEmptyAndAllocatesNothing()
    {
        const string Sql =
            "INSERT INTO r SELECT a, b::int FROM t INNER JOIN u ON u.id = t.id\n"
            + "WHERE a = @a AND (b = @b OR @b IS NULL) -- note\nORDER BY a";
        var rules = SqlDialectRules.PostgreSql;
        var lexemes = SqlLexer.Lex(Sql, rules).Lexemes;

        SqlListUses.Find(Sql, lexemes, 0, lexemes.Count, rules).IsEmpty.ShouldBeTrue();

        // The first calls pay for one-off work: JIT compilation and static initialisers.
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlListUses.Find(Sql, lexemes, 0, lexemes.Count, rules);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = SqlListUses.Find(Sql, lexemes, 0, lexemes.Count, rules);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(SqlDialect.Ansi))]
    [InlineData(nameof(SqlDialect.SqlServer))]
    [InlineData(nameof(SqlDialect.PostgreSql))]
    [InlineData(nameof(SqlDialect.MySql))]
    [InlineData(nameof(SqlDialect.MariaDb))]
    [InlineData(nameof(SqlDialect.Sqlite))]
    [InlineData(nameof(SqlDialect.Oracle))]
    [InlineData(nameof(SqlDialect.CockroachDb))]
    public void Find_EveryDialect_FindsAListUse(string dialect)
    {
        const string Sql = "SELECT a FROM t WHERE a IN @a";
        var rules = SqlDialectRules.For(Enum.Parse<SqlDialect>(dialect));
        var lexemes = SqlLexer.Lex(Sql, rules).Lexemes;

        SqlListUses.Find(Sql, lexemes, 0, lexemes.Count, rules).ShouldBe([SqlParameterUse.List]);
    }
}
