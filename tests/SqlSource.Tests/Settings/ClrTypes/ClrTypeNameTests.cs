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

    [Theory]
    [InlineData("UserId", "UserId")]
    [InlineData("List<UserId>", "List<UserId>")]
    [InlineData("System.Collections.Generic.List<UserId>", "global::System.Collections.Generic.List<UserId>")]
    [InlineData("UserId[]", "UserId[]")]
    [InlineData("global::UserId", "global::UserId")]
    public void TryParse_ShortName_IsValid(string written, string text)
    {
        ClrTypeName.TryParse(written.AsSpan(), out var name).ShouldBeTrue();

        name!.Text.ShouldBe(text);
    }

    [Fact]
    public void TryParse_ShortAndGlobal_AreTwoNames()
    {
        ClrTypeName.TryParse("UserId".AsSpan(), out var bare).ShouldBeTrue();
        ClrTypeName.TryParse("global::UserId".AsSpan(), out var global).ShouldBeTrue();

        bare!.IsShort.ShouldBeTrue();
        global!.IsShort.ShouldBeFalse();
        bare.Text.ShouldNotBe(global.Text);
        bare.ShouldNotBe(global);
    }

    [Fact]
    public void TryParse_KeywordAndSeveralParts_AreNotShort()
    {
        ClrTypeName.TryParse("int".AsSpan(), out var keyword).ShouldBeTrue();
        ClrTypeName.TryParse("App.UserId".AsSpan(), out var several).ShouldBeTrue();

        keyword!.IsShort.ShouldBeFalse();
        several!.IsShort.ShouldBeFalse();
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

    // A converter's types are written with a keyword's type as the keyword, so a property's type is compared in that
    // form, wherever the name stands.
    [Theory]
    [InlineData("global::System.Int64", "long")]
    [InlineData("global::System.String", "string")]
    [InlineData("global::System.Object", "object")]
    [InlineData("global::System.Boolean", "bool")]
    [InlineData("global::System.Byte", "byte")]
    [InlineData("global::System.SByte", "sbyte")]
    [InlineData("global::System.Int16", "short")]
    [InlineData("global::System.UInt16", "ushort")]
    [InlineData("global::System.Int32", "int")]
    [InlineData("global::System.UInt32", "uint")]
    [InlineData("global::System.UInt64", "ulong")]
    [InlineData("global::System.IntPtr", "nint")]
    [InlineData("global::System.UIntPtr", "nuint")]
    [InlineData("global::System.Char", "char")]
    [InlineData("global::System.Single", "float")]
    [InlineData("global::System.Double", "double")]
    [InlineData("global::System.Decimal", "decimal")]
    [InlineData("global::System.Byte[]", "byte[]")]
    [InlineData("global::System.Int32?[]", "int?[]")]
    [InlineData("global::App.Id<global::System.Int64>", "global::App.Id<long>")]
    [InlineData(
        "global::App.Pair<global::System.String?, global::App.Box<global::System.Decimal[]>>[]",
        "global::App.Pair<string?, global::App.Box<decimal[]>>[]"
    )]
    [InlineData("global::System.Nullable<global::System.Int32>[]", "int?[]")]
    [InlineData("global::App.Id<global::System.Nullable<global::System.Int64>>", "global::App.Id<long?>")]
    [InlineData(
        "global::App.Id<global::System.Nullable<global::App.UserId>>[]",
        "global::App.Id<global::App.UserId?>[]"
    )]
    [InlineData("global::System.Nullable<global::App.UserId>[,][]", "global::App.UserId?[,][]")]
    [InlineData("global::System.Nullable<global::System.Int64>", "global::System.Nullable<long>")]
    [InlineData(
        "global::App.Id<global::System.Nullable<global::System.Int64>?>",
        "global::App.Id<global::System.Nullable<long>?>"
    )]
    [InlineData("global::System.Nullable<global::System.Int64>?[]", "global::System.Nullable<long>?[]")]
    [InlineData("global::System.Nullable<global::System.Int64?>[]", "global::System.Nullable<long?>[]")]
    [InlineData(
        "global::App.Id<global::System.Nullable<global::System.Int64?>>",
        "global::App.Id<global::System.Nullable<long?>>"
    )]
    [InlineData(
        "global::App.Id<global::System.Nullable<global::System.Int64[]>>",
        "global::App.Id<global::System.Nullable<long[]>>"
    )]
    [InlineData(
        "global::App.Id<global::System.Nullable<global::System.Nullable<global::System.Int64>>>",
        "global::App.Id<global::System.Nullable<long?>>"
    )]
    [InlineData(
        "global::App.Id<global::System.Nullable<int, int>>",
        "global::App.Id<global::System.Nullable<int, int>>"
    )]
    [InlineData("global::System.Guid", "global::System.Guid")]
    [InlineData("global::App.System.Int64", "global::App.System.Int64")]
    [InlineData("global::System.Int64.Inner", "global::System.Int64.Inner")]
    [InlineData(
        "global::System.Collections.Generic.List<global::System.Int32>",
        "global::System.Collections.Generic.List<int>"
    )]
    public void KeywordTextOf_TypeWithAFrameworkName_WritesTheKeyword(string text, string expected) =>
        ClrTypeName.KeywordTextOf(text).ShouldBe(expected);

    // A text without System., or one that is not a type, is given back as the same string.
    [Theory]
    [InlineData("long")]
    [InlineData("global::App.Id<long>")]
    [InlineData("not a type System.")]
    public void KeywordTextOf_TextWithNothingToChange_IsTheSameString(string text) =>
        ReferenceEquals(ClrTypeName.KeywordTextOf(text), text).ShouldBeTrue();
}
