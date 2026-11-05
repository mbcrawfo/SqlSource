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
    [InlineData("postgres", "id not in @ids", new[] { "List" })]
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
