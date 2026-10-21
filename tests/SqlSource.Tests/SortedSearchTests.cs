using System;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class SortedSearchTests
{
    private static readonly EquatableArray<string> Sorted = new(["a", "B", "c"]);

    [Theory]
    [InlineData("a", 0)]
    [InlineData("b", 1)]
    [InlineData("C", 2)]
    public void IndexOf_KeyInTheList_IsItsIndex(string key, int index) =>
        SortedSearch.IndexOf(Sorted, key, static item => item, StringComparer.OrdinalIgnoreCase).ShouldBe(index);

    [Fact]
    public void IndexOf_KeyNotInTheList_IsMinusOne() =>
        SortedSearch.IndexOf(Sorted, "d", static item => item, StringComparer.OrdinalIgnoreCase).ShouldBe(-1);

    [Fact]
    public void IndexOf_EmptyList_IsMinusOne() =>
        SortedSearch
            .IndexOf(EquatableArray<string>.Empty, "a", static item => item, StringComparer.OrdinalIgnoreCase)
            .ShouldBe(-1);
}
