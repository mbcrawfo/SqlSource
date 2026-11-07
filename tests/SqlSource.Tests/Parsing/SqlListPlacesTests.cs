using System;
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
    [InlineData("OUTPUT")]
    [InlineData("SET")]
    [InlineData("VALUES")]
    [InlineData("IF")]
    [InlineData("WHILE")]
    [InlineData("RETURN")]
    [InlineData("FOR")]
    [InlineData("WINDOW")]
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

    // OUTPUT before a join is the alias of a table.
    [Theory]
    [InlineData("JOIN")]
    [InlineData("INNER")]
    [InlineData("LEFT")]
    [InlineData("RIGHT")]
    [InlineData("FULL")]
    [InlineData("CROSS")]
    [InlineData("OUTER")]
    [InlineData("ON")]
    public void Find_SqlServerOutputBeforeAJoin_IsAName(string word)
    {
        var sql = "UPDATE t SET a = 1 FROM t output " + word + " u.id IN @a";
        var rules = SqlDialectRules.SqlServer;
        var lexemes = SqlLexer.Lex(sql, rules).Lexemes;

        _ = SqlListUses.Find(sql, lexemes, 0, lexemes.Count, rules, out var places);

        places.ShouldBe([SqlListPlace.None]);
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
