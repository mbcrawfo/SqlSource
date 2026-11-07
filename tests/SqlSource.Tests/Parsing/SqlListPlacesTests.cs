using System;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class SqlListPlacesTests
{
    private const int Iterations = 20;

    // Each query, and the place of each of its uses, by name.
    public static TheoryData<string, string[]> Queries =>
        new()
        {
            // An aggregate's parentheses, and those of its WITHIN GROUP, unless OVER follows the call.
            { "SELECT SUM(CASE WHEN s IN @a THEN 1 ELSE 0 END) FROM t", ["InAggregate"] },
            { "SELECT COUNT(IIF(id NOT IN @a, 1, NULL)) FROM t", ["UnderNot, InAggregate"] },
            { "SELECT SUM(CASE WHEN s NOT IN @a THEN 1 END) OVER (PARTITION BY g) FROM t", ["UnderNot"] },
            { "SELECT SUM(x) OVER (PARTITION BY CASE WHEN s IN @a THEN 1 END) FROM t", ["None"] },
            { "SELECT SUM(x) OVER (PARTITION BY CASE WHEN s NOT IN @a THEN 1 END) FROM t", ["UnderNot"] },
            {
                "SELECT STRING_AGG(s, ',') WITHIN GROUP (ORDER BY CASE WHEN id IN @a THEN 1 END) FROM t",
                ["InAggregate"]
            },
            {
                "SELECT STRING_AGG(s, ',') WITHIN GROUP (ORDER BY CASE WHEN id IN @a THEN 1 END) OVER () FROM t",
                ["None"]
            },
            { "SELECT g FROM t GROUP BY g HAVING SUM(CASE WHEN id IN @a THEN 1 END) > 0", ["InAggregate"] },
            { "SELECT g FROM t GROUP BY g HAVING g IN @a", ["None"] },
            { "SELECT sum /* c */ (CASE WHEN id IN @a THEN 1 END) FROM t", ["InAggregate"] },
            { "SELECT COUNT(*) FROM t WHERE id IN @a", ["None"] },
            { "SELECT SUM((SELECT 1 FROM u WHERE u.id IN @a)) FROM t", ["InAggregate"] },
            // A word after "." or a quoted name is no aggregate.
            { "SELECT dbo.SUM(CASE WHEN id IN @a THEN 1 END) FROM t", ["None"] },
            { "SELECT [SUM](CASE WHEN id IN @a THEN 1 END) FROM t", ["None"] },
            { "SELECT MAX(x) FROM (SELECT CASE WHEN id IN @a THEN 1 END AS x FROM t) AS d", ["None"] },
            // GROUP BY, until a clause at its depth, the ")" that closes it, or ";".
            { "SELECT COUNT(*) FROM t GROUP BY ROLLUP (CASE WHEN id IN @a THEN 1 ELSE 0 END)", ["InGroupBy"] },
            {
                "SELECT COUNT(*) FROM t GROUP BY CASE WHEN id IN @a THEN 1 END ORDER BY CASE WHEN id IN @b THEN 1 END",
                ["InGroupBy", "None"]
            },
            { "SELECT COUNT(*) FROM t GROUP /* c */ BY CASE WHEN id NOT IN @a THEN 1 END", ["UnderNot, InGroupBy"] },
            { "SELECT x FROM (SELECT g AS x FROM t GROUP BY g) AS d WHERE x IN @a", ["None"] },
            { "SELECT a FROM t GROUP BY a; SELECT b FROM u WHERE b IN @a", ["None"] },
            {
                "SELECT (SELECT COUNT(*) FROM u GROUP BY CASE WHEN u.id IN @a THEN 1 END) FROM t WHERE id IN @b",
                ["InGroupBy", "None"]
            },
            { "SELECT a FROM t GROUP BY a UNION SELECT b FROM u WHERE b IN @a", ["None"] },
            // OUTPUT, until a clause at its depth, the ")" that closes it, or ";".
            {
                "UPDATE t SET s = 'x' OUTPUT CASE WHEN inserted.id IN @a THEN 1 END WHERE id IN @b",
                ["InOutput", "None"]
            },
            // @log is a use too, a value, though the query would declare it as a table variable.
            { "DELETE t OUTPUT deleted.id INTO @log WHERE id NOT IN @a", ["None", "UnderNot"] },
            { "INSERT t OUTPUT inserted.id SELECT a FROM u WHERE u.id IN @a", ["None"] },
            { "DELETE t OUTPUT IIF(deleted.id IN @a, 1, 0); SELECT a FROM u WHERE a IN @b", ["InOutput", "None"] },
            // A column named like a clause word opens nothing.
            { "SELECT t.output, t.[group] FROM t WHERE id IN @a", ["None"] },
            { "SELECT d.group_id FROM d WHERE d.id IN @a", ["None"] },
            { "SELECT g FROM t WHERE t.group IN @a", ["None"] },
            // A use that is no list stands in no place.
            { "SELECT id FROM t WHERE id = @v AND s IN @a", ["None", "None"] },
            { "SELECT SUM(@v) FROM t WHERE @a IS NULL OR id NOT IN @a", ["None", "None", "UnderNot"] },
            // A NOT reaches a list until AND, OR or a comma at its depth, the ")" that closes the depth, or a word
            // that starts a clause, a statement or a condition of its own.
            { "SELECT a FROM t WHERE id NOT IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT (a = 1 OR id IN @a)", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT (y = 1) AND s IN @a", ["None"] },
            { "SELECT a FROM t WHERE NOT EXISTS (SELECT 1 FROM u WHERE u.id IN @a)", ["None"] },
            { "SELECT a FROM t WHERE x IS NOT DISTINCT FROM IIF(s IN @a, 1, 0)", ["None"] },
            {
                "SELECT g FROM t WHERE NOT EXISTS (SELECT 1 FROM u WHERE u.id = t.id) GROUP BY g"
                    + " HAVING SUM(CASE WHEN s IN @a THEN 1 END) > 0",
                ["InAggregate"]
            },
            { "SELECT COUNT(*) FROM t WHERE NOT a = 1 GROUP BY CASE WHEN s IN @a THEN 1 END", ["InGroupBy"] },
            { "SELECT COUNT(*) FROM t WHERE NOT a = 1 HAVING SUM(CASE WHEN s IN @a THEN 1 END) > 0", ["InAggregate"] },
            {
                "SELECT g FROM t WHERE NOT a = 1 GROUP BY g ORDER BY SUM(CASE WHEN s IN @a THEN 1 END)",
                ["InAggregate"]
            },
            { "SELECT g FROM t JOIN u ON NOT u.a = t.a WHERE t.id IN @a", ["None"] },
            { "DELETE t WHERE NOT a = 1; SELECT SUM(CASE WHEN s IN @a THEN 1 END) FROM t", ["InAggregate"] },
            {
                "IF NOT EXISTS (SELECT 1 FROM t) BEGIN SELECT SUM(CASE WHEN s IN @a THEN 1 END) FROM u END",
                ["InAggregate"]
            },
            {
                "MERGE t USING u ON t.id = u.id WHEN NOT MATCHED THEN INSERT (id) VALUES (u.id)"
                    + " OUTPUT CASE WHEN inserted.id IN @a THEN 1 END;",
                ["InOutput"]
            },
            { "MERGE t USING u ON NOT t.a = u.a WHEN MATCHED AND t.id IN @a THEN DELETE;", ["None"] },
            // CASE is a frame of its own: it starts clean, WHEN, THEN and ELSE end a NOT inside it, and END gives back
            // the NOT open before it.
            { "SELECT COUNT(CASE WHEN n NOT LIKE 'a%' THEN 1 WHEN s IN @a THEN 2 END) FROM t", ["InAggregate"] },
            { "SELECT COUNT(CASE WHEN n NOT LIKE 'a%' THEN IIF(s IN @a, 1, 0) END) FROM t", ["InAggregate"] },
            { "SELECT a FROM t WHERE NOT CASE WHEN a = 1 THEN 1 ELSE 0 END IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT (CASE IIF(s IN @a, 1, 0) WHEN 1 THEN 1 END = 1)", ["None"] },
            { "SELECT a FROM t WHERE NOT y = CASE WHEN a = 1 AND s IN @a THEN 1 ELSE 0 END", ["None"] },
            // A ")" closes a CASE left open inside it.
            { "SELECT SUM((CASE WHEN a = 1 THEN 1)) FROM t WHERE s IN @a", ["None"] },
            // The condition of IIF is a condition of its own, as that of WHEN is.
            { "SELECT a FROM t WHERE NOT IIF(s IN @a, 1, 0) = 1", ["None"] },
            { "SELECT a FROM t WHERE NOT IIF(a = 1, 1, 0) IN @a", ["UnderNot"] },
            // PRODUCT, and the spatial aggregates after geometry:: or geography::.
            { "SELECT PRODUCT(CASE WHEN id NOT IN @a THEN 2 ELSE 1 END) FROM t", ["UnderNot, InAggregate"] },
            { "SELECT PRODUCT(CASE WHEN id NOT IN @a THEN 2 ELSE 1 END) OVER () FROM t", ["UnderNot"] },
            { "SELECT geometry::UnionAggregate(CASE WHEN id NOT IN @a THEN g END) FROM t", ["UnderNot, InAggregate"] },
            { "SELECT geometry::EnvelopeAggregate(CASE WHEN id IN @a THEN g END) FROM t", ["InAggregate"] },
            { "SELECT GEOGRAPHY::collectionaggregate(CASE WHEN id IN @a THEN g END) FROM t", ["InAggregate"] },
            { "SELECT geography :: ConvexHullAggregate(CASE WHEN id IN @a THEN g END) FROM t", ["InAggregate"] },
            { "SELECT geometry::UnionAggregate(CASE WHEN id IN @a THEN g END) OVER () FROM t", ["None"] },
            { "SELECT dbo.UnionAggregate(CASE WHEN id IN @a THEN g END) FROM t", ["None"] },
            { "SELECT UnionAggregate(CASE WHEN id IN @a THEN g END) FROM t", ["None"] },
            { "SELECT geometry:UnionAggregate(CASE WHEN id IN @a THEN g END) FROM t", ["None"] },
            { "SELECT geometry::Point(CASE WHEN id IN @a THEN 1 END, 0, 0) FROM t", ["None"] },
            // OUTPUT opens only in a statement that changes rows, before its SELECT, and never as a name after AS.
            { "SELECT x AS output, CASE WHEN s NOT IN @a THEN 1 END AS f FROM t", ["UnderNot"] },
            { "SELECT output, CASE WHEN s NOT IN @a THEN 1 END FROM t", ["UnderNot"] },
            { "INSERT INTO t (a) SELECT output, CASE WHEN s NOT IN @a THEN 1 END FROM u", ["UnderNot"] },
            { "INSERT INTO t (a) SELECT output AS f, CASE WHEN s NOT IN @a THEN 1 END FROM u", ["UnderNot"] },
            { "UPDATE t SET a = 1 FROM t AS output WITH (NOLOCK) JOIN u ON u.id IN @a", ["None"] },
            { "UPDATE t SET a = 1 FROM t AS output JOIN u ON output.id = u.id AND u.id IN @a", ["None"] },
            { "UPDATE t SET a = output, b = IIF(s IN @a, 1, 0)", ["None"] },
            { "UPDATE t SET output = IIF(s IN @a, 1, 0)", ["None"] },
            { "INSERT INTO t (a) OUTPUT CASE WHEN inserted.a IN @a THEN 1 END SELECT b FROM u", ["InOutput"] },
            { "UPDATE t SET a = 1 OUTPUT CASE WHEN inserted.a IN @a THEN 1 END", ["InOutput"] },
            { "DELETE t OUTPUT CASE WHEN deleted.a IN @a THEN 1 END", ["InOutput"] },
            {
                "MERGE t USING u ON t.id = u.id WHEN MATCHED THEN DELETE OUTPUT IIF(deleted.a IN @a, 1, 0);",
                ["InOutput"]
            },
            {
                "WITH c AS (SELECT 1 AS x) UPDATE t SET a = 1 OUTPUT CASE WHEN inserted.a IN @a THEN 1 END",
                ["InOutput"]
            },
            // A place ends at ";", and at UNION or, for OUTPUT, SELECT at its depth.
            { "SELECT a FROM t GROUP BY a; SELECT CASE WHEN b IN @a THEN 1 END FROM u", ["None"] },
            { "SELECT a FROM t GROUP BY a UNION SELECT CASE WHEN b IN @a THEN 1 END FROM u", ["None"] },
            { "SELECT a FROM t GROUP BY a UNION (SELECT CASE WHEN b IN @a THEN 1 END FROM u)", ["None"] },
            { "SELECT a FROM t GROUP BY a; IF IIF(1 IN @a, 1, 0) = 1 RETURN", ["None"] },
            { "DELETE t OUTPUT deleted.a; IF IIF(1 IN @a, 1, 0) = 1 RETURN", ["None"] },
            { "INSERT t (a) OUTPUT inserted.a SELECT CASE WHEN b IN @a THEN 1 END FROM u", ["None"] },
            // A word ends a NOT only where it can be no name: OUTPUT and WINDOW are names, and the GROUP of WITHIN
            // GROUP and the FROM of IS DISTINCT FROM start no clause.
            { "SELECT a FROM t WHERE NOT output IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT (output IN @a)", ["UnderNot"] },
            { "UPDATE t SET a = 1 WHERE NOT output IN @a", ["UnderNot"] },
            { "MERGE t USING u ON t.id = u.id WHEN MATCHED AND NOT output IN @a THEN DELETE;", ["UnderNot"] },
            { "SELECT COUNT(CASE WHEN NOT output IN @a THEN 1 END) FROM t", ["UnderNot, InAggregate"] },
            { "SELECT a FROM t WHERE NOT window IN @a", ["UnderNot"] },
            { "SELECT COUNT(*) FROM t GROUP BY window, CASE WHEN s NOT IN @a THEN 1 END", ["UnderNot, InGroupBy"] },
            {
                "SELECT g, SUM(x) OVER w FROM t GROUP BY g WINDOW w AS (PARTITION BY CASE WHEN s IN @a THEN 1 END)",
                ["None"]
            },
            {
                "SELECT g FROM t GROUP BY g HAVING NOT STRING_AGG(n, ',') WITHIN GROUP (ORDER BY n) IN @a",
                ["UnderNot"]
            },
            {
                "SELECT g FROM t GROUP BY g HAVING NOT (STRING_AGG(n, ',') WITHIN GROUP (ORDER BY n) IN @a)",
                ["UnderNot"]
            },
            {
                "SELECT g FROM t GROUP BY g HAVING NOT APPROX_PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY x) IN @a",
                ["UnderNot"]
            },
            {
                "SELECT CASE WHEN NOT PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY x) OVER () IN @a THEN 1 END FROM t",
                ["UnderNot"]
            },
            { "SELECT a FROM t WHERE NOT (x IS DISTINCT FROM y OR s IN @a)", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT (x IS NOT DISTINCT FROM y OR s IN @a)", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT t.output IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT by IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT geometry IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT end_date IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT CAST(x AS int) IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT LEFT(s, 1) IN @a", ["UnderNot"] },
            { "SELECT a FROM t WHERE NOT x COLLATE c IN @a", ["UnderNot"] },
            // OUTPUT opens a place only where its clause stands: before the WHERE of a statement that changes rows,
            // and not as a name.
            { "DELETE FROM t WHERE output NOT IN @a", ["UnderNot"] },
            { "DELETE FROM t WHERE output IS NULL OR b NOT IN @a", ["UnderNot"] },
            { "UPDATE t SET a = 1 WHERE output LIKE 'x%' OR b NOT IN @a", ["UnderNot"] },
            { "DELETE t OUTPUT deleted.id WHERE output NOT IN @a", ["UnderNot"] },
            { "UPDATE t SET a = 1 FROM output AS o JOIN u ON u.id NOT IN @a", ["UnderNot"] },
            { "UPDATE t SET a = output OUTPUT CASE WHEN inserted.a IN @a THEN 1 END", ["InOutput"] },
            {
                "MERGE t USING u ON t.id = u.id WHEN MATCHED THEN DELETE OUTPUT CASE WHEN deleted.id IN @a THEN 1 END;",
                ["InOutput"]
            },
            { "INSERT t (a) OUTPUT CASE WHEN inserted.a IN @a THEN 1 END VALUES (1)", ["InOutput"] },
            {
                "UPDATE t SET a = CASE WHEN b = 1 THEN 1 END OUTPUT CASE WHEN inserted.a IN @a THEN 1 END",
                ["InOutput"]
            },
            { "UPDATE t SET a = DEFAULT OUTPUT CASE WHEN inserted.a IN @a THEN 1 END", ["InOutput"] },
            // A place ends at the statement after it, which no ";" need end.
            { "SELECT g FROM t GROUP BY g IF EXISTS (SELECT 1 FROM u WHERE u.id NOT IN @a) PRINT 'x'", ["UnderNot"] },
            {
                "SELECT g FROM t GROUP BY g DECLARE @n int = (SELECT COUNT(*) FROM u WHERE u.id NOT IN @a)",
                ["None", "UnderNot"]
            },
            { "UPDATE t SET a = 1 OUTPUT inserted.a UPDATE u SET b = IIF(c NOT IN @a, 1, 0)", ["UnderNot"] },
            {
                "UPDATE t SET a = 1 OUTPUT inserted.a IF EXISTS (SELECT 1 FROM u WHERE u.id NOT IN @a) PRINT 'x'",
                ["UnderNot"]
            },
            { "SELECT g FROM t GROUP BY g SELECT a FROM u WHERE a NOT IN @a", ["UnderNot"] },
            {
                "SELECT g FROM t GROUP BY g HAVING COUNT(*) > 1"
                    + " IF EXISTS (SELECT 1 FROM u WHERE u.id NOT IN @a) PRINT 'x'",
                ["UnderNot"]
            },
        };

    // Each word, or item, ends a NOT open before it at its depth.
    [Theory]
    [InlineData("AND")]
    [InlineData("OR")]
    [InlineData(",")]
    [InlineData(";")]
    [InlineData("WHERE")]
    [InlineData("ON")]
    [InlineData("HAVING")]
    [InlineData("WHEN")]
    [InlineData("THEN")]
    [InlineData("ELSE")]
    [InlineData("END")]
    [InlineData("BEGIN")]
    [InlineData("GROUP")]
    [InlineData("ORDER")]
    [InlineData("FROM")]
    [InlineData("SELECT")]
    [InlineData("UNION")]
    [InlineData("EXCEPT")]
    [InlineData("INTERSECT")]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("MERGE")]
    [InlineData("SET")]
    [InlineData("VALUES")]
    [InlineData("IF")]
    [InlineData("WHILE")]
    [InlineData("RETURN")]
    [InlineData("FOR")]
    [InlineData("WITH")]
    [InlineData("OPTION")]
    [InlineData("INTO")]
    [InlineData("DEFAULT")]
    [InlineData("EXEC")]
    [InlineData("EXECUTE")]
    public void Find_SqlServerWordAfterANot_EndsIt(string word)
    {
        var sql = "WHERE NOT a = 1 " + word + " b IN @a";
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        _ = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out var places);

        places.ShouldBe([SqlListPlace.None]);
    }

    // A word that can be a name, or a word of a clause where it starts none, ends no NOT.
    [Theory]
    [InlineData("WHERE NOT a = 1 OUTPUT b IN @a")]
    [InlineData("WHERE NOT a = 1 WINDOW b IN @a")]
    [InlineData("WHERE NOT a = 1 THROW b IN @a")]
    [InlineData("WHERE NOT a = 1 DECLARE b IN @a")]
    [InlineData("WHERE NOT a = 1 PRINT b IN @a")]
    [InlineData("WHERE NOT a = 1 RAISERROR b IN @a")]
    [InlineData("WHERE NOT a = 1 WITHIN GROUP b IN @a")]
    [InlineData("WHERE NOT a IS DISTINCT FROM b IN @a")]
    public void Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing(string sql)
    {
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        _ = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out var places);

        places.ShouldBe([SqlListPlace.UnderNot]);
    }

    // The word OUTPUT before one of these items is a name, as in output.id, SET output = 1, a = output, b = 2,
    // (output), a table's alias before a join, or a column tested or compared, and opens no place.
    [Theory]
    [InlineData(".")]
    [InlineData("=")]
    [InlineData(",")]
    [InlineData(")")]
    [InlineData("JOIN")]
    [InlineData("INNER")]
    [InlineData("LEFT")]
    [InlineData("RIGHT")]
    [InlineData("FULL")]
    [InlineData("CROSS")]
    [InlineData("OUTER")]
    [InlineData("ON")]
    [InlineData("IN")]
    [InlineData("IS")]
    [InlineData("LIKE")]
    [InlineData("BETWEEN")]
    [InlineData("NOT")]
    [InlineData("AND")]
    [InlineData("OR")]
    [InlineData("COLLATE")]
    public void Find_SqlServerOutputBeforeAnItemOfAName_OpensNoPlace(string next) =>
        PlacesOf("UPDATE t SET a = b output " + next + " c IIF(s NOT IN @a, 1, 0)").ShouldBe([SqlListPlace.UnderNot]);

    // The word OUTPUT after one of these items is a name too, as in a = output, SET output = 1 or FROM output AS o.
    // DELETE, END and DEFAULT end what may come before the clause, as in THEN DELETE OUTPUT.
    [Theory]
    [InlineData("=")]
    [InlineData(",")]
    [InlineData("AS")]
    [InlineData("BY")]
    [InlineData("NOT")]
    [InlineData("AND")]
    [InlineData("OR")]
    [InlineData("IS")]
    [InlineData("CASE")]
    [InlineData("DISTINCT")]
    [InlineData("GROUP")]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("MERGE")]
    [InlineData("SELECT")]
    [InlineData("WHERE")]
    [InlineData("FROM")]
    [InlineData("HAVING")]
    [InlineData("ORDER")]
    [InlineData("FOR")]
    [InlineData("UNION")]
    [InlineData("EXCEPT")]
    [InlineData("INTERSECT")]
    [InlineData("WITH")]
    [InlineData("OPTION")]
    [InlineData("INTO")]
    [InlineData("VALUES")]
    [InlineData("EXEC")]
    [InlineData("EXECUTE")]
    [InlineData("ON")]
    [InlineData("WHEN")]
    [InlineData("THEN")]
    [InlineData("ELSE")]
    [InlineData("SET")]
    [InlineData("IF")]
    [InlineData("WHILE")]
    [InlineData("RETURN")]
    [InlineData("BEGIN")]
    [InlineData("DECLARE")]
    [InlineData("PRINT")]
    [InlineData("RAISERROR")]
    public void Find_SqlServerOutputAfterAnItemOfAName_OpensNoPlace(string before) =>
        PlacesOf("UPDATE t SET a = b " + before + " output c IIF(s NOT IN @a, 1, 0)").ShouldBe([SqlListPlace.UnderNot]);

    // Between a value and an item that starts one, the word OUTPUT opens its clause, but after the statement's WHERE.
    [Theory]
    [InlineData("UPDATE t SET a = b output c IIF(s NOT IN @a, 1, 0)", "UnderNot, InOutput")]
    [InlineData("DELETE t WHERE a = b output c IIF(s NOT IN @a, 1, 0)", "UnderNot")]
    public void Find_SqlServerOutputAfterAValue_OpensItsClauseBeforeTheWhere(string sql, string expected) =>
        PlacesOf(sql).Select(static place => place.ToString()).ShouldBe([expected]);

    // A statement ends a GROUP BY and an OUTPUT before it, without a ";".
    [Theory]
    [InlineData("IF")]
    [InlineData("WHILE")]
    [InlineData("SET")]
    [InlineData("RETURN")]
    [InlineData("BEGIN")]
    [InlineData("DECLARE")]
    [InlineData("PRINT")]
    [InlineData("RAISERROR")]
    [InlineData("THROW")]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("DELETE")]
    [InlineData("MERGE")]
    public void Find_SqlServerStatementAfterAPlace_EndsIt(string word)
    {
        PlacesOf("SELECT g FROM t GROUP BY g " + word + " IIF(s NOT IN @a, 1, 0)").ShouldBe([SqlListPlace.UnderNot]);
        PlacesOf("UPDATE t SET a = 1 OUTPUT inserted.a " + word + " IIF(s NOT IN @a, 1, 0)")
            .ShouldBe([SqlListPlace.UnderNot]);
    }

    // T-SQL puts a list in no CASE before its first WHEN, nor after IS NOT, but for a call that ends a NOT itself, so
    // these fragments show that a CASE starts with no NOT and that the NOT of IS NOT opens none.
    [Theory]
    [InlineData("WHERE NOT CASE s IN @a WHEN 1 THEN 1 END = 1")]
    [InlineData("WHERE a IS NOT NULL b IN @a")]
    public void Find_SqlServerFragmentWithANotThatReachesNothing_IsNotUnderNot(string sql)
    {
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        _ = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out var places);

        places.ShouldBe([SqlListPlace.None]);
    }

    // Without such a word, the NOT reaches the list.
    [Fact]
    public void Find_SqlServerNotWithNoWordAfterIt_ReachesTheList()
    {
        const string Sql = "WHERE NOT a = 1 b IN @a";
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(Sql, rules).Lexemes;

        _ = SqlListUses.Find(Sql, lexemes, 0, lexemes.Count, rules, out var places);

        places.ShouldBe([SqlListPlace.UnderNot]);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void Find_SqlServer_ReadsWhereEachListStands(string sql, string[] expected)
    {
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        var uses = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out var places);

        places.Length.ShouldBe(uses.Length);
        places.Select(static place => place.ToString()).ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void Find_WithPlaces_GivesTheUsesWithout(string sql, string[] expected)
    {
        _ = expected;
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        var uses = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out _);

        uses.ShouldBe(SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules));
    }

    [Theory]
    [InlineData("SELECT SUM(x) FROM t GROUP BY g")]
    [InlineData("SELECT COUNT(*) FROM t WHERE id IN (@a) OR @a IS NULL")]
    [InlineData("")]
    public void Find_SqlServerWithoutAList_GivesNoPlaces(string sql)
    {
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        var uses = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out var places);

        uses.IsEmpty.ShouldBeTrue();
        places.IsEmpty.ShouldBeTrue();
    }

    // The reading of places keeps its depths and the place of each use in arrays rented from the pool, so it adds
    // the result's array alone to what Find allocates without it: here 22 depths, more than the 16 slots of the first
    // array the pool gives.
    [Fact]
    public void Find_WithPlaces_AllocatesThePlacesAlone()
    {
        var sql =
            "SELECT SUM("
            + new string('(', 20)
            + "CASE WHEN x IN @a THEN 1 END"
            + new string(')', 20)
            + ") FROM t GROUP BY CASE WHEN y IN @b THEN 1 END HAVING COUNT(*) > 0 AND z NOT IN @b";
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        var without = Allocated(() => SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules));
        var with = Allocated(() => SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out _));

        (with - without).ShouldBe(Allocated(static () => new SqlListPlace[3]));
    }

    private static ImmutableArray<SqlListPlace> PlacesOf(string sql)
    {
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        _ = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out var places);
        return places;
    }

    // What a call allocates, once the first calls have paid for one-off work and filled the pool.
    private static long Allocated<T>(Func<T> call)
    {
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = call();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = call();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
