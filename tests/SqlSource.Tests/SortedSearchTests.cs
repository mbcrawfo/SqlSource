using System;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class SortedSearchTests
{
    private static readonly EquatableArray<string> Sorted = new(["a", "B", "c"]);

    // A key that starts another, as a folder starts the folders below it.
    private static readonly EquatableArray<string> Prefixes = new(["a", "B", "bc"]);

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

    [Theory]
    [InlineData("a/x", 1, 0)]
    [InlineData("b/x", 1, 1)]
    [InlineData("Bc/x", 2, 2)]
    [InlineData("", 0, -1)]
    [InlineData("abc", 2, -1)]
    [InlineData("bcd", 3, -1)]
    [InlineData("bb", 2, -1)]
    public void IndexOf_KeyThatIsTheStartOfAText_IsTheIndexOfThatStart(string text, int keyLength, int index) =>
        SortedSearch
            .IndexOf(Prefixes, text, keyLength, static item => item, StringComparison.OrdinalIgnoreCase)
            .ShouldBe(index);
}
