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
