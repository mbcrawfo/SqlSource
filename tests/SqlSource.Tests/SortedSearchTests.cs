using System;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class SortedSearchTests
{
    [Fact]
    public void IndexOf_KeyInTheList_IsItsIndex()
    {
        var sorted = new EquatableArray<string>(["a", "B", "c"]);

        SortedSearch.IndexOf(sorted, "b", static key => key, StringComparer.OrdinalIgnoreCase).ShouldBe(1);
        SortedSearch.IndexOf(sorted, "a", static key => key, StringComparer.OrdinalIgnoreCase).ShouldBe(0);
        SortedSearch.IndexOf(sorted, "C", static key => key, StringComparer.OrdinalIgnoreCase).ShouldBe(2);
        SortedSearch.IndexOf(sorted, "d", static key => key, StringComparer.OrdinalIgnoreCase).ShouldBe(-1);
        SortedSearch
            .IndexOf(EquatableArray<string>.Empty, "a", static key => key, StringComparer.OrdinalIgnoreCase)
            .ShouldBe(-1);
    }
}
