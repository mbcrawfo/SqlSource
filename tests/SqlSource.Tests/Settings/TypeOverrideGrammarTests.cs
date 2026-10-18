using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class TypeOverrideGrammarTests
{
    [Theory]
    [InlineData("timestamptz=System.DateTimeOffset", "timestamptz", "global::System.DateTimeOffset")]
    [InlineData("int4=int", "int4", "int")]
    [InlineData("numeric=global::System.Double", "numeric", "global::System.Double")]
    [InlineData(
        "int4[]=System.Collections.Generic.List<int?>",
        "int4[]",
        "global::System.Collections.Generic.List<int?>"
    )]
    [InlineData("public.user_status=MyApp.UserStatus", "public.user_status", "global::MyApp.UserStatus")]
    [InlineData(
        "hstore=System.Collections.Generic.Dictionary<string, string?>",
        "hstore",
        "global::System.Collections.Generic.Dictionary<string, string?>"
    )]
    [InlineData(
        "hstore=System.Collections.Generic.Dictionary<string,string?>",
        "hstore",
        "global::System.Collections.Generic.Dictionary<string, string?>"
    )]
    [InlineData("int4=MyApp.Outer.Inner", "int4", "global::MyApp.Outer.Inner")]
    [InlineData("int4=MyApp.Outer<int>.Inner<string>", "int4", "global::MyApp.Outer<int>.Inner<string>")]
    [InlineData("int4=int?[]", "int4", "int?[]")]
    [InlineData("int4=int[,]", "int4", "int[,]")]
    [InlineData("int4=int[][]", "int4", "int[][]")]
    [InlineData(
        "int4=System.Collections.Generic.List<int[ , ]?>",
        "int4",
        "global::System.Collections.Generic.List<int[,]?>"
    )]
    [InlineData("int4=global::App", "int4", "global::App")]
    [InlineData(
        "int4=System.Collections.Generic.List<System.Collections.Generic.List<global::System.Guid?>>",
        "int4",
        "global::System.Collections.Generic.List<global::System.Collections.Generic.List<global::System.Guid?>>"
    )]
    [InlineData("integer=long", "int4", "long")]
    [InlineData("pg_catalog.int8=decimal", "int8", "decimal")]
    [InlineData("Billing.Invoice$State=Billing.State", "Billing.Invoice$State", "global::Billing.State")]
    public void TryReadEntry_ValidEntry_GivesItsKeyAndTheCanonicalText(string text, string key, string type)
    {
        var entry = Read(text, spacesAroundEquals: false);

        entry.ShouldBe(new TypeOverride(key, type, SettingPlace.Property));
    }

    [Theory]
    [InlineData("timestamptz=default")]
    [InlineData("timestamptz=DEFAULT")]
    [InlineData("timestamptz=Default")]
    public void TryReadEntry_Default_HasNoType(string text) =>
        Read(text, spacesAroundEquals: false).ShouldBe(new TypeOverride("timestamptz", null, SettingPlace.Property));

    [Theory]
    [InlineData("numeric = System.Double", "global::System.Double")]
    [InlineData("numeric= System.Double", "global::System.Double")]
    [InlineData("numeric =System.Double", "global::System.Double")]
    [InlineData("numeric\t=\tdefault", null)]
    public void TryReadEntry_SpacesAroundTheEqualsInAMarker_AreAllowed(string text, string? type)
    {
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), spacesAroundEquals: true, SettingPlace.Query, out var entry)
            .ShouldBeTrue();

        entry.ShouldBe(new TypeOverride("numeric", type, SettingPlace.Query));
    }

    [Theory]
    [InlineData("numeric = System.Double")]
    [InlineData("numeric= System.Double")]
    [InlineData("numeric =System.Double")]
    public void TryReadEntry_SpacesAroundTheEqualsInAList_AreNotAllowed(string text) =>
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), spacesAroundEquals: false, SettingPlace.Property, out _)
            .ShouldBeFalse();

    [Theory]
    // A ? at the end of the whole type: a property's nullability is its column's.
    [InlineData("int4=int?")]
    [InlineData("int4=System.Collections.Generic.List<int>?")]
    [InlineData("int4=int[]?")]
    [InlineData("int4=(int, string)")]
    [InlineData("int4=dynamic")]
    [InlineData("int4=void")]
    [InlineData("int4=int*")]
    // Unqualified, and not a keyword: the generator cannot see a file's usings.
    [InlineData("int4=Guid")]
    [InlineData("int4=System.Collections.Generic.List<Guid>")]
    [InlineData("int4=System.Guid extra")]
    [InlineData("int4=System.Guid ")]
    [InlineData("int4=System.Collections.Generic.List<>")]
    [InlineData("int4=System.Collections.Generic.List<int")]
    [InlineData("int4=System.Collections.Generic.List<int,>")]
    [InlineData("int4=System.Collections.Generic.List<,int>")]
    [InlineData("int4=System.Collections.Generic.List<int>>")]
    [InlineData("int4=int[")]
    [InlineData("int4=int]")]
    [InlineData("int4=int[;]")]
    [InlineData("int4=int []")]
    [InlineData("int4=System. Guid")]
    [InlineData("int4=System..Guid")]
    [InlineData("int4=System.Guid.")]
    [InlineData("int4=.System.Guid")]
    [InlineData("int4=global::")]
    [InlineData("int4=global::int")]
    [InlineData("int4=global::global::System.Guid")]
    [InlineData("int4=System.string")]
    [InlineData("int4=System.class")]
    [InlineData("int4=int<string>")]
    [InlineData("int4=int.Max")]
    [InlineData("int4=@System.Guid")]
    [InlineData("int4=1System.Guid")]
    [InlineData("int4=System.Guid=int")]
    [InlineData("timestampz=int")]
    [InlineData("user_status=int")]
    [InlineData("int4=")]
    [InlineData("=int")]
    [InlineData("int4")]
    [InlineData("")]
    [InlineData("int4==int")]
    [InlineData("numeric(18,2)=decimal")]
    [InlineData("int4=default extra")]
    public void TryReadEntry_InvalidEntry_IsNotRead(string text)
    {
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), spacesAroundEquals: false, SettingPlace.Property, out var entry)
            .ShouldBeFalse();
        entry.ShouldBeNull();
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), spacesAroundEquals: true, SettingPlace.Query, out _)
            .ShouldBeFalse();
    }

    // Nothing a user writes may make the generator throw, so a type nested past a limit is not read, and the
    // stack is never at risk.
    [Fact]
    public void TryReadEntry_TypeNestedVeryDeeply_IsNotReadAndDoesNotThrow()
    {
        const int Depth = 100_000;
        var text = "int4=" + string.Concat(Enumerable.Repeat("A.B<", Depth)) + "int" + new string('>', Depth);

        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), spacesAroundEquals: false, SettingPlace.Property, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void ReadList_EntriesOverSeveralLines_AreReadAndTheLastOfAKeyWinsThroughItsAlias()
    {
        var invalid = new List<string>();

        var list = TypeOverrideGrammar.ReadList("int4=int\n  int8=long  integer=short", SettingPlace.Metadata, invalid);

        list.ShouldBe([
            new TypeOverride("int4", "short", SettingPlace.Metadata),
            new TypeOverride("int8", "long", SettingPlace.Metadata),
        ]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_InvalidEntry_IsRecordedAsWrittenAndTheOthersApply()
    {
        var invalid = new List<string>();

        var list = TypeOverrideGrammar.ReadList("int4=int timestampz=int", SettingPlace.Property, invalid);

        list.ShouldBe([new TypeOverride("int4", "int", SettingPlace.Property)]);
        invalid.ShouldBe(["timestampz=int"]);
    }

    // The targets of the package turn a line break into a space, so an entry split over lines inside <> arrives
    // with a space there, and is still one entry.
    [Theory]
    [InlineData("hstore=System.Collections.Generic.Dictionary<string, string?>")]
    [InlineData("hstore=System.Collections.Generic.Dictionary< string , string? >")]
    public void ReadList_SpaceInsideAngleBrackets_KeepsOneEntry(string text)
    {
        var invalid = new List<string>();

        var list = TypeOverrideGrammar.ReadList(text, SettingPlace.Property, invalid);

        list.ShouldBe([
            new TypeOverride(
                "hstore",
                "global::System.Collections.Generic.Dictionary<string, string?>",
                SettingPlace.Property
            ),
        ]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_SpaceInsideSquareBrackets_KeepsOneEntry()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("int4=int[ , ] int8=long", SettingPlace.Property, invalid)
            .ShouldBe([
                new TypeOverride("int4", "int[,]", SettingPlace.Property),
                new TypeOverride("int8", "long", SettingPlace.Property),
            ]);
        invalid.ShouldBeEmpty();
    }

    // Spaces around the equals separate a list's entries, and each part is an entry that is not valid.
    [Fact]
    public void ReadList_SpacesAroundTheEquals_AreEntriesThatAreNotValid()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar.ReadList("numeric = System.Double", SettingPlace.Property, invalid).ShouldBeEmpty();
        invalid.ShouldBe(["numeric", "=", "System.Double"]);
    }

    [Fact]
    public void ReadList_AngleBracketThatDoesNotClose_IsOneEntryThatIsNotValid()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList("int4=System.Collections.Generic.List<int int8=long", SettingPlace.Property, invalid)
            .ShouldBeEmpty();
        invalid.ShouldBe(["int4=System.Collections.Generic.List<int int8=long"]);
    }

    [Fact]
    public void ReadList_EntriesInAnyOrder_AreSortedByKeyIgnoringCase()
    {
        var invalid = new List<string>();

        TypeOverrideGrammar
            .ReadList(
                "text=string public.Zeta=App.Z Public.alpha=App.A timestamptz=default bool=bool",
                SettingPlace.Property,
                invalid
            )
            .Select(static entry => entry.Key)
            .ShouldBe(["bool", "Public.alpha", "public.Zeta", "text", "timestamptz"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public void ReadList_NoEntry_IsEmpty(string text)
    {
        var invalid = new List<string>();

        TypeOverrideGrammar.ReadList(text, SettingPlace.Property, invalid).ShouldBeEmpty();
        invalid.ShouldBeEmpty();
    }

    private static TypeOverride Read(string text, bool spacesAroundEquals)
    {
        TypeOverrideGrammar
            .TryReadEntry(text.AsSpan(), spacesAroundEquals, SettingPlace.Property, out var entry)
            .ShouldBeTrue();
        return entry.ShouldNotBeNull();
    }
}
