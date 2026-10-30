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

    [Fact]
    public void IndexOf_ProbeOfAnItemInTheList_IsItsIndex() =>
        SortedSearch.IndexOf(3, new IntProbe([1, 3, 5], 3)).ShouldBe(1);

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    public void IndexOf_ProbeOfNoItem_IsMinusOne(int key) =>
        SortedSearch.IndexOf(3, new IntProbe([1, 3, 5], key)).ShouldBe(-1);

    [Fact]
    public void IndexOf_ProbeOfAnEmptyList_IsMinusOne() => SortedSearch.IndexOf(0, new IntProbe([], 1)).ShouldBe(-1);

    [Fact]
    public void LowerBound_KeyInARunOfEqualItems_IsTheFirstOfTheRun() =>
        SortedSearch.LowerBound(5, new IntProbe([1, 3, 3, 3, 5], 3)).ShouldBe(1);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(6, 3)]
    public void LowerBound_KeyBetweenItems_IsWhereItWouldGo(int key, int index) =>
        SortedSearch.LowerBound(3, new IntProbe([1, 3, 5], key)).ShouldBe(index);

    [Fact]
    public void LowerBound_EmptyList_IsZero() => SortedSearch.LowerBound(0, new IntProbe([], 1)).ShouldBe(0);

    [Theory]
    [InlineData(2, 3, 2)]
    [InlineData(3, 1, 3)]
    [InlineData(4, 3, 4)]
    public void LowerBound_FromAStart_SearchesFromThere(int start, int key, int index) =>
        SortedSearch.LowerBound(start, 4, new IntProbe([1, 3, 3, 5], key)).ShouldBe(index);

    private readonly struct IntProbe(int[] items, int key) : SortedSearch.IProbe
    {
        public int CompareAt(int index) => items[index].CompareTo(key);
    }
}
