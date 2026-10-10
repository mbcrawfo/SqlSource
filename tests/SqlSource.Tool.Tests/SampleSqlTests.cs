using System;
using Shouldly;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

// The SQL a database is asked to describe: the query with each token's default in the token's place.
public sealed class SampleSqlTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private string Sample(string sql) => SampleSql.Build(Plans.Query(_folder, sql).Query);

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

    // The plan gives such a query a problem and the run never builds its sample.
    [Fact]
    public void Build_TokenWithoutADefault_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => Sample("-- name: A\nSELECT 1 FROM t {{where}};\n"))
            .Message.ShouldBe("The token 'where' of the query 'A' has no default.");
}
