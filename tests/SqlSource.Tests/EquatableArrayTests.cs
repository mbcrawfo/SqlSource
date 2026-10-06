using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public class EquatableArrayTests
{
    [Fact]
    public void Equals_SameItemsInSeparateInstances_ReturnsTrue()
    {
        var left = Of("a", "b");
        var right = Of("a", "b");

        left.Equals(right).ShouldBeTrue();
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        left.Equals((object)right).ShouldBeTrue();
        left.Equals("a").ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentItems_ReturnsFalse()
    {
        Of("a", "b").Equals(Of("a", "c")).ShouldBeFalse();
        Of("a").Equals(Of("a", "b")).ShouldBeFalse();
        (Of("a") != Of("b")).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_SameItemsInSeparateInstances_IsTheSame() =>
        Of("a", "b").GetHashCode().ShouldBe(Of("a", "b").GetHashCode());

    [Fact]
    public void Default_BehavesAsEmpty()
    {
        var array = default(EquatableArray<string>);

        array.Count.ShouldBe(0);
        array.ShouldBeEmpty();
        array.Equals(EquatableArray<string>.Empty).ShouldBeTrue();
        array.GetHashCode().ShouldBe(EquatableArray<string>.Empty.GetHashCode());
    }

    [Fact]
    public void Indexer_ReturnsItemsInOrder()
    {
        var array = Of("a", "b");

        array.Count.ShouldBe(2);
        array[0].ShouldBe("a");
        array[1].ShouldBe("b");
        array.ToArray().ShouldBe(["a", "b"]);
        array.ShouldBe(["a", "b"]);
    }

    private static EquatableArray<string> Of(params string[] items) => new(ImmutableArray.Create(items));
}
