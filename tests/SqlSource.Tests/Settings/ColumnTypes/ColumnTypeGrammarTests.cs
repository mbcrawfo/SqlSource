using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using Xunit;

namespace SqlSource.Tests.Settings.ColumnTypes;

public sealed class ColumnTypeGrammarTests
{
    private static readonly SettingPlace Place = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

    [Fact]
    public void ReadMembers_Members_AreEntriesInTheOrderOfTheMapAtThePlace()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        ColumnTypeGrammar
            .ReadMembers(
                [new("users.id", "UserId"), new("public.posts.created_by", "UserId")],
                Place,
                invalid,
                repeated
            )
            .ShouldBe([
                new ColumnType("public.posts.created_by", "UserId", Place),
                new ColumnType("users.id", "UserId", Place),
            ]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // A member is a key and a type, each trimmed, and the type default ignoring case.
    [Theory]
    [InlineData("users.id", "default", null)]
    [InlineData(" users.id\t", " DEFAULT ", null)]
    [InlineData(" users.id ", " MyApp.UserId ", "global::MyApp.UserId")]
    public void ReadMembers_WhiteSpaceAroundTheKeyAndTheType_IsTrimmed(string key, string type, string? expected)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        ColumnTypeGrammar
            .ReadMembers([new(key, type)], Place, invalid, repeated)
            .ShouldBe([new ColumnType("users.id", expected, Place)]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // A member whose key is no column's source, or whose type is not valid or empty, is named by its index, and the
    // others apply.
    [Theory]
    [InlineData("id", "X")]
    [InlineData("a.b.c.d", "X")]
    [InlineData("users.id", "int?")]
    [InlineData("users.id", "")]
    [InlineData("users.id", " ")]
    [InlineData("", "X")]
    public void ReadMembers_MemberThatIsNotValid_IsItsIndexAndTheOthersApply(string key, string type)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        ColumnTypeGrammar
            .ReadMembers([new("posts.id", "PostId"), new(key, type)], Place, invalid, repeated)
            .ShouldBe([new ColumnType("posts.id", "PostId", Place)]);
        invalid.ShouldBe([1]);
        repeated.ShouldBeEmpty();
    }

    // One key is the map's identity: a quoted and a plain spelling of one text, and one text twice.  The first member
    // applies.
    [Theory]
    [InlineData("users.id", "\"users\".id")]
    [InlineData("users.id", "users.id")]
    public void ReadMembers_TwoMembersOfOneKey_TheSecondIsRepeatedAndTheFirstApplies(string first, string second)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        ColumnTypeGrammar
            .ReadMembers([new(first, "A"), new("posts.id", "PostId"), new(second, "B")], Place, invalid, repeated)
            .ShouldBe([new ColumnType("posts.id", "PostId", Place), new ColumnType("users.id", "A", Place)]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBe([(2, 0)]);
    }

    // The flags say which member is written in the object form with "json": true.
    [Fact]
    public void ReadMembers_MemberFlaggedAsJson_IsAnEntryThatSaysSo()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        ColumnTypeGrammar
            .ReadMembers(
                [new("orders.metadata", "OrderMetadata"), new("orders.note", "Note")],
                Place,
                invalid,
                repeated,
                [true, false]
            )
            .ShouldBe([
                new ColumnType("orders.metadata", "OrderMetadata", Place, IsJson: true),
                new ColumnType("orders.note", "Note", Place),
            ]);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // The opt-in needs a type: default with it is not valid, as -> json default is not in a marker.
    [Theory]
    [InlineData("default")]
    [InlineData(" Default ")]
    public void ReadMembers_JsonMemberOfDefault_IsItsIndexAndTheOthersApply(string type)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        ColumnTypeGrammar
            .ReadMembers(
                [new("posts.id", "PostId"), new("orders.metadata", type)],
                Place,
                invalid,
                repeated,
                [false, true]
            )
            .ShouldBe([new ColumnType("posts.id", "PostId", Place)]);
        invalid.ShouldBe([1]);
        repeated.ShouldBeEmpty();
    }

    [Fact]
    public void ReadMembers_NoMember_IsEmpty()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        ColumnTypeGrammar.ReadMembers([], Place, invalid, repeated).ShouldBeEmpty();
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("posts.id -> PostId", "posts.id", "PostId")]
    [InlineData("posts.id->PostId", "posts.id", "PostId")]
    [InlineData("public.posts.id\t->\tMyApp.PostId", "public.posts.id", "global::MyApp.PostId")]
    [InlineData("\"a->b\".id -> PostId", "\"a->b\".id", "PostId")]
    [InlineData("U&\"p\\006Fsts\".id -> PostId", "\"posts\".id", "PostId")]
    [InlineData("posts.id -> default", "posts.id", null)]
    [InlineData("posts.id -> Default", "posts.id", null)]
    public void TryReadHeaderEntry_Arrow_IsValid(string value, string key, string? type)
    {
        ColumnTypeGrammar.TryReadHeaderEntry(value.AsSpan(), out var entry).ShouldBeTrue();

        entry.ShouldBe(new ColumnType(key, type, SettingPlace.FileHeader));
    }

    // The word json, white space, then the type is the JSON opt-in.  json alone is a type of that name, as it was
    // before the opt-in.
    [Theory]
    [InlineData("orders.metadata -> json OrderMetadata", "OrderMetadata", true)]
    [InlineData("orders.metadata->JSON\tMyApp.OrderMetadata", "global::MyApp.OrderMetadata", true)]
    [InlineData("orders.metadata -> json  json", "json", true)]
    [InlineData("orders.metadata -> json", "json", false)]
    [InlineData("orders.metadata -> jsonOrderMetadata", "jsonOrderMetadata", false)]
    public void TryReadHeaderEntry_JsonBeforeTheType_IsTheOptIn(string value, string type, bool isJson)
    {
        ColumnTypeGrammar.TryReadHeaderEntry(value.AsSpan(), out var entry).ShouldBeTrue();

        entry.ShouldBe(new ColumnType("orders.metadata", type, SettingPlace.FileHeader, isJson));
    }

    [Theory]
    [InlineData("posts.id not null -> PostId")]
    [InlineData("posts.id null -> PostId")]
    [InlineData("orders.metadata -> json default")]
    [InlineData("orders.metadata -> json not a type")]
    [InlineData("orders.metadata -> json int?")]
    [InlineData("posts.id not null")]
    [InlineData("posts.id")]
    [InlineData("id -> PostId")]
    [InlineData("posts.id -> ")]
    [InlineData("posts.id -> not a type")]
    [InlineData("posts.id = PostId")]
    [InlineData("-> PostId")]
    public void TryReadHeaderEntry_MalformedEntry_IsNotValid(string value)
    {
        ColumnTypeGrammar.TryReadHeaderEntry(value.AsSpan(), out var entry).ShouldBeFalse();

        entry.ShouldBeNull();
    }

    [Theory]
    [InlineData("OrderMetadata", "OrderMetadata", false)]
    [InlineData("default", null, false)]
    [InlineData("json", "json", false)]
    [InlineData("json OrderMetadata", "OrderMetadata", true)]
    [InlineData("Json \t App.OrderMetadata", "global::App.OrderMetadata", true)]
    public void TryReadMarkerType_TypeAfterTheArrow_IsReadWithTheOptIn(string written, string? type, bool isJson)
    {
        ColumnTypeGrammar.TryReadMarkerType(written.AsSpan(), out var read, out var json).ShouldBeTrue();

        read.ShouldBe(type);
        json.ShouldBe(isJson);
    }

    // The opt-in with nothing after its white space, with default, or with a type that is not valid.
    [Theory]
    [InlineData("json ")]
    [InlineData("json default")]
    [InlineData("json DEFAULT")]
    [InlineData("json int?")]
    [InlineData("json a b")]
    [InlineData("")]
    [InlineData("not a type")]
    public void TryReadMarkerType_TypeThatIsNotValid_IsFalse(string written) =>
        ColumnTypeGrammar.TryReadMarkerType(written.AsSpan(), out _, out _).ShouldBeFalse();
}
