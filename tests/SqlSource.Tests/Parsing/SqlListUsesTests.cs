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
    [InlineData("postgres", "SELECT NOT a, id IN @ids", new[] { "List" })]
    [InlineData("postgres", "id NOT IN @a AND id IN @b", new[] { "NegatedList", "List" })]
    [InlineData("postgres", "id IN @a AND NOT id IN @b", new[] { "List", "NegatedList" })]
    // The NOT of IS NOT tests a value and negates nothing after it, though here the list is a value compared.
    [InlineData("postgres", "WHERE NOT b AND x IS NOT DISTINCT FROM id IN @ids", new[] { "NegatedList" })]
    // A list whose truth the query uses as a value, compared, tested, cast or passed to a call, is negated too: an
    // empty list makes it FALSE where one value would make it NULL, and the value can turn that FALSE true.
    [InlineData("postgres", "WHERE (id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE (id IN @ids) IS NOT TRUE", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE (id IN @ids) = false", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE false = (id IN @ids)", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE false = id IN @ids", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE id IN @ids = false", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE (id IN @ids) <> true", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE (id IN @ids) < true", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE (id IN @ids)::int = 0", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE (id IN @ids) IN (false)", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE COALESCE(id IN @ids, false) = false", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE CASE id IN @ids WHEN false THEN true END", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE CASE WHEN a THEN id IN @ids END = false", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE f(id IN @ids)", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE a = 1 AND (id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE x IS DISTINCT FROM y AND (id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE left(a, 1) = 'x' AND (id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "JOIN t ON (t.id IN @ids) = false JOIN u ON true", new[] { "NegatedList" })]
    // A quoted name before "(" is a call's, and its argument a value.
    [InlineData("postgres", "WHERE \"f\"(id IN @ids)", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE s.\"f\"(id IN @ids)", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE U&\"f\"(id IN @ids)", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE \"f\"(id IN @ids, a)", new[] { "NegatedList" })]
    // A comma inside brackets, the GROUP of WITHIN GROUP and a name after "." end no condition.
    [InlineData("postgres", "WHERE a && ARRAY[1, 2] AND (id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE a[1:2] = ARRAY[3, 4] AND (id IN @ids) = false", new[] { "NegatedList" })]
    [InlineData(
        "postgres",
        "SELECT a FROM t HAVING percentile_cont(0.5) WITHIN GROUP (ORDER BY a) > 0 AND (id IN @ids) IS FALSE",
        new[] { "NegatedList" }
    )]
    [InlineData("postgres", "WHERE t.set = 1 AND (id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE t.by = 1 AND t.output = 2 AND (id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "SELECT a FROM t GROUP BY a HAVING (a IN @ids) IS FALSE", new[] { "NegatedList" })]
    // A value inside a subquery or a WITH query may reach a filter of the query around it.
    [InlineData("postgres", "WITH s AS (SELECT id IN @ids AS f FROM t) SELECT f FROM s", new[] { "NegatedList" })]
    [InlineData("postgres", "SELECT f FROM (SELECT id IN @ids AS f FROM t) s", new[] { "NegatedList" })]
    [InlineData("postgres", "SELECT (SELECT t.id IN @ids FROM t LIMIT 1)", new[] { "NegatedList" })]
    [InlineData("postgres", "SELECT f FROM (SELECT a WHERE b UNION SELECT id IN @ids AS f) s", new[] { "NegatedList" })]
    // A list reached from WHERE, ON, HAVING or WHEN through AND, OR and parentheses alone is a condition, which FALSE
    // and NULL fail alike, and so is one in the output of the outermost statement.
    [InlineData("postgres", "WHERE id IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE lower(id) IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE id + 1 IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE data->>'k' IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE id::text IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE (@ids IS NULL OR id IN @ids)", new[] { "NullTest", "List" })]
    [InlineData("postgres", "WHERE a BETWEEN 1 AND 2 AND id IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE a = 1 OR (b AND id IN @ids)", new[] { "List" })]
    [InlineData("postgres", "WHERE EXISTS (SELECT 1 FROM t WHERE t.id IN @ids)", new[] { "List" })]
    [InlineData("postgres", "WHERE CASE WHEN id IN @ids THEN false ELSE true END", new[] { "List" })]
    [InlineData("postgres", "WHERE CASE WHEN a THEN id IN @ids ELSE false END", new[] { "List" })]
    [InlineData("postgres", "WHERE x LIKE @p AND id IN @ids", new[] { "Value", "List" })]
    [InlineData("postgres", "SELECT a FROM t JOIN u ON u.id IN @ids LEFT JOIN v ON true", new[] { "List" })]
    [InlineData("postgres", "SELECT id IN @ids AS f FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT (id IN @ids) = false AS f", new[] { "List" })]
    [InlineData("postgres", "SELECT a FROM t ORDER BY id IN @ids DESC", new[] { "List" })]
    [InlineData("postgres", "SELECT a FROM t GROUP BY id IN @ids", new[] { "List" })]
    [InlineData("postgres", "SELECT a FROM t GROUP BY a HAVING a IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE ARRAY[1, 2] && a AND id IN @ids", new[] { "List" })]
    // A call, a CASE or a bracket inside the outermost output is output too, but a subquery's own output is not.
    [InlineData("postgres", "SELECT coalesce(id IN @ids, false) FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT coalesce(false, id IN @ids) FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT CASE (id IN @ids) WHEN true THEN 1 END", new[] { "List" })]
    [InlineData("postgres", "SELECT ARRAY[1, (id IN @ids)::int]", new[] { "List" })]
    [InlineData("postgres", "SELECT a FROM t ORDER BY coalesce(id IN @ids, false)", new[] { "List" })]
    [InlineData("postgres", "SELECT f((SELECT t.id IN @ids FROM t))", new[] { "NegatedList" })]
    [InlineData("postgres", "SELECT count(*) FILTER (WHERE (id IN @ids) IS FALSE) FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT CASE WHEN a > 1 THEN (id IN @ids)::int ELSE 0 END", new[] { "List" })]
    [InlineData("postgres", "SELECT CASE WHEN b THEN coalesce(id IN @ids, false) END", new[] { "List" })]
    [InlineData("postgres", "SELECT string_agg(n, ',' ORDER BY id IN @ids DESC) FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT row_number() OVER (ORDER BY id IN @ids DESC) FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT a FROM t WINDOW w AS (ORDER BY id IN @ids)", new[] { "List" })]
    [InlineData("postgres", "SELECT DISTINCT ON (a) (id IN @ids) = false AS f FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT DISTINCT ON (id IN @ids) a FROM t", new[] { "List" })]
    [InlineData("postgres", "SELECT a WHERE b UNION SELECT id IN @ids", new[] { "List" })]
    [InlineData("postgres", "UPDATE t SET f = id IN @ids WHERE a = 1", new[] { "List" })]
    [InlineData("postgres", "DELETE FROM t WHERE a = 1 RETURNING id IN @ids", new[] { "List" })]
    [InlineData("postgres", "SELECT count(*) FILTER (WHERE id IN @ids) FROM t", new[] { "List" })]
    [InlineData(
        "postgres",
        "INSERT INTO t SELECT a FROM u WHERE u.id IN @ids ON CONFLICT DO NOTHING",
        new[] { "List" }
    )]
    // A statement's end starts the reading again at the outermost level.
    [InlineData(
        "postgres",
        "SELECT (SELECT 1 WHERE (a IN @x) = false); SELECT b IN @y",
        new[] { "NegatedList", "List" }
    )]
    [InlineData("postgres", "SELECT 1 WHERE (a IN @x); SELECT b IN @y", new[] { "List", "List" })]
    // A parenthesis or a CASE left open ends with the query, and a ")" that closes nothing is passed over.
    [InlineData("postgres", "WHERE (id IN @ids", new[] { "List" })]
    [InlineData("postgres", "WHERE id IN @ids) IS FALSE", new[] { "NegatedList" })]
    [InlineData("postgres", "WHERE CASE WHEN a THEN (id IN @ids) = false", new[] { "NegatedList" })]
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
