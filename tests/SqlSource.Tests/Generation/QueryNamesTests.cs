using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation;

public sealed class QueryNamesTests
{
    private static readonly EquatableArray<SqlQuery> Queries = TestModels.Array(
        Query("GetUser"),
        Query("ListUsers"),
        Query("GetUser")
    );

    [Theory]
    [InlineData("GetUser", 0, 0)]
    [InlineData("GetUser", 1, 2)]
    [InlineData("ListUsers", 0, 1)]
    [InlineData("ListUsers", 1, 1)]
    public void IndexOf_NameAtOrAfterTheStart_IsItsFirstIndexThere(string name, int from, int expected) =>
        QueryNames.IndexOf(Queries, name, from).ShouldBe(expected);

    [Theory]
    [InlineData("DeleteUser", 0)]
    [InlineData("ListUsers", 2)]
    [InlineData("GetUser", 3)]
    public void IndexOf_NameAbsentFromTheStart_IsMinusOne(string name, int from) =>
        QueryNames.IndexOf(Queries, name, from).ShouldBe(-1);

    [Fact]
    public void IndexOf_NameOfAnotherCase_IsMinusOne()
    {
        var queries = TestModels.Array(Query("Q"));

        QueryNames.IndexOf(queries, "q", 0).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_NoQueries_IsMinusOne() =>
        QueryNames.IndexOf(EquatableArray<SqlQuery>.Empty, "GetUser", 0).ShouldBe(-1);

    private static SqlQuery Query(string name) =>
        new(
            name,
            new LocationInfo("/app/Repo/Users.sql", default, default),
            null,
            null,
            TestModels.Array(new SqlSegment(SqlSegmentKind.Literal, "SELECT 1;")),
            null,
            EquatableArray<SqlToken>.Empty,
            EquatableArray<SqlQueryParameter>.Empty,
            SettingsLevel.None,
            null,
            null,
            EquatableArray<ColumnMarker>.Empty,
            EquatableArray<DiagnosticInfo>.Empty
        );
}
