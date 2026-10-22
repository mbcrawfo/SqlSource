using System;
using System.Linq;
using Shouldly;
using SqlSource.Settings.ClrTypes;
using Xunit;

namespace SqlSource.Tests.Settings.ClrTypes;

// The forms of the grammar are TypeOverrideGrammarTests'.  These are the parts of a name that the resolution of a
// mapped type walks.
public sealed class ClrTypeNameTests
{
    [Fact]
    public void TryParse_NestedGenericWithRanks_GivesEachPart()
    {
        ClrTypeName.TryParse("App.Outer<int?, System.Guid[]>.Inner?[,][]".AsSpan(), out var name).ShouldBeTrue();

        var type = name.ShouldNotBeNull();
        type.Keyword.ShouldBeNull();
        type.Parts.Select(static part => part.Identifier).ShouldBe(["App", "Outer", "Inner"]);
        type.Parts[0].Arguments.ShouldBeEmpty();
        type.Parts[2].Arguments.ShouldBeEmpty();
        var arguments = type.Parts[1].Arguments;
        arguments.Count.ShouldBe(2);
        arguments[0].Type.Keyword.ShouldBe("int");
        arguments[0].Nullable.ShouldBeTrue();
        arguments[1].Type.Text.ShouldBe("global::System.Guid[]");
        arguments[1].Nullable.ShouldBeFalse();
        type.Ranks.ShouldBe([new ClrRank(NullableElement: true, Dimensions: 2), new ClrRank(false, 1)]);
        type.Text.ShouldBe("global::App.Outer<int?, global::System.Guid[]>.Inner?[,][]");
    }

    [Fact]
    public void TryParse_Keyword_HasNoParts()
    {
        ClrTypeName.TryParse("string".AsSpan(), out var name).ShouldBeTrue();

        name.ShouldBe(new ClrTypeName("string", EquatableArray<ClrNamePart>.Empty, EquatableArray<ClrRank>.Empty));
    }

    [Fact]
    public void TryParse_SameTypeWrittenTwoWays_IsEqual()
    {
        ClrTypeName.TryParse("System.Collections.Generic.List<int>".AsSpan(), out var first).ShouldBeTrue();
        ClrTypeName.TryParse("global::System.Collections.Generic.List< int >".AsSpan(), out var second).ShouldBeTrue();

        first.ShouldBe(second);
    }

    [Fact]
    public void TryParse_NotAType_GivesNull()
    {
        ClrTypeName.TryParse("int?".AsSpan(), out var name).ShouldBeFalse();

        name.ShouldBeNull();
    }

    // An open generic is a type that cannot be a property's
    // (docs/superpowers/specs/2026-10-18-query-generation-phase-5-2-type-map-design.md).  The grammar has no way to
    // write one, so no override names it.
    [Fact]
    public void TryParse_OpenGeneric_IsRefused()
    {
        ClrTypeName.TryParse("System.Collections.Generic.List<>".AsSpan(), out _).ShouldBeFalse();
        ClrTypeName.TryParse("global::MyApp.Box<>".AsSpan(), out _).ShouldBeFalse();
    }
}
