using System;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// The SQL a database is asked to describe: the query with each token's default in the token's place.
public sealed class SampleSqlTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private string Sample(string sql) => SampleSql.Build(Plans.Query(_folder, sql).Query, SqlDialect.PostgreSql);

    private string SqlServerSample(string sql) =>
        SampleSql.Build(Plans.Query(_folder, sql, "mssql").Query, SqlDialect.SqlServer);

    [Fact]
    public void Build_QueryWithoutTokens_IsItsSql() =>
        Sample("-- name: A\nSELECT 1 FROM t;\n").ShouldBe("SELECT 1 FROM t;");

    [Fact]
    public void Build_TokenWithADefault_HasTheDefaultInItsPlace() =>
        Sample("-- name: A\nSELECT 1 FROM t {{where:WHERE x = 1}};\n").ShouldBe("SELECT 1 FROM t WHERE x = 1;");

    [Fact]
    public void Build_TokenWithAnEmptyDefault_HasNothingInItsPlace() =>
        Sample("-- name: A\nSELECT 1 FROM t {{where:}};\n").ShouldBe("SELECT 1 FROM t ;");

    [Fact]
    public void Build_OneTokenTwice_HasItsDefaultInBothPlaces() =>
        Sample("-- name: A\nSELECT {{col:id}} FROM t ORDER BY {{col}};\n").ShouldBe("SELECT id FROM t ORDER BY id;");

    [Fact]
    public void Build_DefaultFromAMarker_HasTheDefaultInTheTokensPlace() =>
        Sample("-- name: A\n-- token: {{where:WHERE x = 1}}\nSELECT 1 FROM t {{where}};\n")
            .ShouldBe("SELECT 1 FROM t WHERE x = 1;");

    // A list is described as one value of its element, so a use after IN is a list of one.
    [Fact]
    public void Build_ListUse_IsParenthesised() =>
        Sample("-- name: A\nSELECT 1 FROM t WHERE id IN @ids;\n").ShouldBe("SELECT 1 FROM t WHERE id IN (@ids);");

    // The test of a list for NULL is no use of the parameter: the server is told nothing about it.  On PostgreSQL it
    // is a value the planner cannot fold, so that the condition's other side stays a condition, as the running query's
    // boolean parameter keeps it.
    [Fact]
    public void Build_NullTest_IsASubqueryOfNull() =>
        Sample("-- name: A\nSELECT 1 FROM t WHERE (@ids IS NULL OR id IN @ids);\n")
            .ShouldBe("SELECT 1 FROM t WHERE ((SELECT NULL) IS NULL OR id IN (@ids));");

    // PostgreSQL refuses a subquery in the index predicate of ON CONFLICT, which reduces no join, so the test there is
    // NULL.
    [Fact]
    public void Build_NullTestInAnIndexPredicate_IsNull() =>
        Sample(
                "-- name: A\nINSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) WHERE (@kinds IS NULL OR kind IN @kinds) DO NOTHING;\n"
            )
            .ShouldBe(
                "INSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) WHERE (NULL IS NULL OR kind IN (@kinds)) DO NOTHING;"
            );

    [Fact]
    public void Build_NullTestInAnIndexPredicateWithoutParentheses_IsNull() =>
        Sample(
                "-- name: A\ninsert into t (id, kind) values (1, 'a') "
                    + "on conflict ((id + 0), kind) where @kinds is null or kind in @kinds do nothing;\n"
            )
            .ShouldBe(
                "insert into t (id, kind) values (1, 'a') "
                    + "on conflict ((id + 0), kind) where NULL is null or kind in (@kinds) do nothing;"
            );

    // A parenthesis closed inside the predicate does not end it.
    [Fact]
    public void Build_NullTestAfterAFunctionInAnIndexPredicate_IsNull() =>
        Sample(
                "-- name: A\nINSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) WHERE (lower(kind) <> '' AND (@kinds IS NULL OR kind IN @kinds)) DO NOTHING;\n"
            )
            .ShouldBe(
                "INSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) WHERE (lower(kind) <> '' AND (NULL IS NULL OR kind IN (@kinds))) DO NOTHING;"
            );

    // A join ON a function named conflict, and the WHERE after it, is no conflict target: no DO ends its WHERE, and
    // NULL there would let the planner make the LEFT JOIN inner.
    [Theory]
    [InlineData(
        "SELECT b.total FROM a LEFT JOIN b ON conflict(a.id, b.id) WHERE (@ids IS NULL OR a.id IN @ids)",
        "SELECT b.total FROM a LEFT JOIN b ON conflict(a.id, b.id) WHERE ((SELECT NULL) IS NULL OR a.id IN (@ids))"
    )]
    [InlineData(
        "SELECT s.total FROM (SELECT b.total FROM a LEFT JOIN b ON conflict(a.id, b.id) "
            + "WHERE (@ids IS NULL OR a.id IN @ids)) s",
        "SELECT s.total FROM (SELECT b.total FROM a LEFT JOIN b ON conflict(a.id, b.id) "
            + "WHERE ((SELECT NULL) IS NULL OR a.id IN (@ids))) s"
    )]
    [InlineData(
        "INSERT INTO t SELECT a.id FROM a JOIN b ON conflict(a.id, b.id) WHERE (@ids IS NULL OR b.id IN @ids) "
            + "ON CONFLICT DO NOTHING",
        "INSERT INTO t SELECT a.id FROM a JOIN b ON conflict(a.id, b.id) "
            + "WHERE ((SELECT NULL) IS NULL OR b.id IN (@ids)) ON CONFLICT DO NOTHING"
    )]
    public void Build_NullTestAfterAJoinOnAFunctionNamedConflict_IsASubqueryOfNull(string sql, string sample) =>
        Sample("-- name: A\n" + sql + ";\n").ShouldBe(sample + ";");

    // A conflict target without a predicate leaves the WHERE of DO UPDATE a condition like any other.
    [Fact]
    public void Build_NullTestInTheWhereOfDoUpdateWithoutAPredicate_IsASubqueryOfNull() =>
        Sample(
                "-- name: A\nINSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) DO UPDATE SET kind = 'b' WHERE (@kinds IS NULL OR t.kind IN @kinds);\n"
            )
            .ShouldBe(
                "INSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) DO UPDATE SET kind = 'b' WHERE ((SELECT NULL) IS NULL OR t.kind IN (@kinds));"
            );

    // The predicate ends at the DO of the action, whose WHERE is a condition like any other.
    [Fact]
    public void Build_NullTestInTheWhereOfDoUpdate_IsASubqueryOfNull() =>
        Sample(
                "-- name: A\nINSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) WHERE (@kinds IS NULL OR kind IN @kinds) "
                    + "DO UPDATE SET kind = 'b' WHERE (@kinds IS NULL OR t.kind IN @kinds);\n"
            )
            .ShouldBe(
                "INSERT INTO t (id, kind) VALUES (1, 'a') "
                    + "ON CONFLICT (id) WHERE (NULL IS NULL OR kind IN (@kinds)) "
                    + "DO UPDATE SET kind = 'b' WHERE ((SELECT NULL) IS NULL OR t.kind IN (@kinds));"
            );

    // A conflict on a constraint names no index, and has no predicate.
    [Fact]
    public void Build_NullTestAfterOnConstraint_IsASubqueryOfNull() =>
        Sample(
                "-- name: A\nINSERT INTO t (id, kind) VALUES (1, 'a') ON CONFLICT ON CONSTRAINT t_pkey "
                    + "DO UPDATE SET kind = 'b' WHERE (@kinds IS NULL OR t.kind IN @kinds);\n"
            )
            .ShouldBe(
                "INSERT INTO t (id, kind) VALUES (1, 'a') ON CONFLICT ON CONSTRAINT t_pkey "
                    + "DO UPDATE SET kind = 'b' WHERE ((SELECT NULL) IS NULL OR t.kind IN (@kinds));"
            );

    // Words in a string or a quoted name are no clause, a DO among them included.
    [Theory]
    [InlineData(
        "SELECT 1 FROM t WHERE name <> 'on conflict (id) where' AND (@ids IS NULL OR id IN @ids) AND kind <> 'do'",
        "SELECT 1 FROM t WHERE name <> 'on conflict (id) where' AND ((SELECT NULL) IS NULL OR id IN (@ids)) "
            + "AND kind <> 'do'"
    )]
    [InlineData(
        "SELECT 1 AS \"on conflict (id) where\" FROM t WHERE (@ids IS NULL OR id IN @ids) AND \"do\" > 0",
        "SELECT 1 AS \"on conflict (id) where\" FROM t WHERE ((SELECT NULL) IS NULL OR id IN (@ids)) AND \"do\" > 0"
    )]
    public void Build_NullTestBetweenQuotedRegionsOfTheWords_IsASubqueryOfNull(string sql, string sample) =>
        Sample("-- name: A\n" + sql + ";\n").ShouldBe(sample + ";");

    // The predicate of an INSERT inside a WITH query ends at its DO, so the outer query's test is the subquery.
    [Fact]
    public void Build_NullTestsInAndAfterAWithQuerysIndexPredicate_EachHasItsOwn() =>
        Sample(
                "-- name: A\nWITH i AS (INSERT INTO t (id) VALUES (1) "
                    + "ON CONFLICT (id) WHERE (@ids IS NULL OR id IN @ids) DO NOTHING RETURNING id) "
                    + "SELECT id FROM i WHERE (@ids IS NULL OR id IN @ids);\n"
            )
            .ShouldBe(
                "WITH i AS (INSERT INTO t (id) VALUES (1) "
                    + "ON CONFLICT (id) WHERE (NULL IS NULL OR id IN (@ids)) DO NOTHING RETURNING id) "
                    + "SELECT id FROM i WHERE ((SELECT NULL) IS NULL OR id IN (@ids));"
            );

    // SQL Server refuses a subquery in an aggregate, a GROUP BY and an OUTPUT clause, and folds nothing it describes.
    [Fact]
    public void Build_NullTestOnSqlServer_IsNull() =>
        SqlServerSample("-- name: A\nSELECT 1 FROM t WHERE (@ids IS NULL OR id IN @ids);\n")
            .ShouldBe("SELECT 1 FROM t WHERE (NULL IS NULL OR id IN (@ids));");

    // A parameter that is no list is described as written, its test for NULL too, beside a list.
    [Fact]
    public void Build_NullTestOfAValueBesideAList_IsAsWritten() =>
        Sample("-- name: A\nSELECT 1 FROM t WHERE (@name IS NULL OR name = @name) AND id IN @ids;\n")
            .ShouldBe("SELECT 1 FROM t WHERE (@name IS NULL OR name = @name) AND id IN (@ids);");

    // The plan gives such a query a problem and the run never builds its sample.
    [Fact]
    public void Build_TokenWithoutADefault_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => Sample("-- name: A\nSELECT 1 FROM t {{where}};\n"))
            .Message.ShouldBe("The token 'where' of the query 'A' has no default.");
}
