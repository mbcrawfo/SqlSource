using System;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class SqlParameterNameTests
{
    private static readonly EquatableArray<SqlQueryParameter> Listed = new([
        new SqlQueryParameter("id", null, null, false),
        new SqlQueryParameter("Name", null, null, false),
    ]);

    [Fact]
    public void Of_ParameterSpan_IsTheSpanLessThePrefix() =>
        SqlParameterName.Of(new TextSpan(4, 3)).ShouldBe(new TextSpan(5, 2));

    [Theory]
    [InlineData("@id", "@", "id")]
    [InlineData(":id", "@:", "id")]
    [InlineData("@id", "@:", "id")]
    public void WithoutPrefix_NameWithAPrefix_IsTheBareName(string name, string prefixes, string expected) =>
        SqlParameterName.WithoutPrefix(name, prefixes).ShouldBe(expected);

    [Theory]
    [InlineData("id", "@", "id")]
    [InlineData(":id", "@", ":id")]
    [InlineData("", "@", "")]
    public void WithoutPrefix_NameWithoutAPrefix_IsTheName(string name, string prefixes, string expected) =>
        SqlParameterName.WithoutPrefix(name, prefixes).ShouldBe(expected);

    [Theory]
    [InlineData("id", 0)]
    [InlineData("ID", 0)]
    [InlineData("NAME", 1)]
    [InlineData("name", 1)]
    public void IndexOf_NameInAnyCase_FindsTheParameter(string name, int expected) =>
        SqlParameterName.IndexOf(Listed, name.AsSpan()).ShouldBe(expected);

    [Theory]
    [InlineData("other")]
    [InlineData("i")]
    [InlineData("")]
    public void IndexOf_NameNotInTheList_IsMinusOne(string name) =>
        SqlParameterName.IndexOf(Listed, name.AsSpan()).ShouldBe(-1);

    [Fact]
    public void IndexOf_EmptyList_IsMinusOne() =>
        SqlParameterName.IndexOf(EquatableArray<SqlQueryParameter>.Empty, "id".AsSpan()).ShouldBe(-1);

    [Theory]
    [InlineData("Id", "iD", true)]
    [InlineData("Id", "Idx", false)]
    public void Comparer_Names_AreEqualWhenTheyDifferOnlyInCase(string a, string b, bool expected)
    {
        SqlParameterName.Comparer.Equals(a, b).ShouldBe(expected);
        string.Equals(a, b, SqlParameterName.Comparison).ShouldBe(expected);
    }
}
