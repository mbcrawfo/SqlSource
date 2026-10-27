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

    [Theory]
    [InlineData("posts.id not null -> PostId")]
    [InlineData("posts.id null -> PostId")]
    [InlineData("posts.id not null")]
    [InlineData("posts.id")]
    [InlineData("id -> PostId")]
    [InlineData("posts.id -> ")]
    [InlineData("posts.id -> not a type")]
    [InlineData("posts.id = PostId")]
    [InlineData("-> PostId")]
    public void TryReadHeaderEntry_Nullability_IsNotValid(string value)
    {
        ColumnTypeGrammar.TryReadHeaderEntry(value.AsSpan(), out var entry).ShouldBeFalse();

        entry.ShouldBeNull();
    }
}
