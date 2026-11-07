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
        };

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
    // the result's array alone to what Find allocates without it: here 12 depths, more than the 8 of the first array.
    [Fact]
    public void Find_WithPlaces_AllocatesThePlacesAlone()
    {
        var sql =
            "SELECT SUM("
            + new string('(', 12)
            + "CASE WHEN x IN @a THEN 1 END"
            + new string(')', 12)
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
