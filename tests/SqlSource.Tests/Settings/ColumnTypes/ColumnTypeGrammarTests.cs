using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using Xunit;

namespace SqlSource.Tests.Settings.ColumnTypes;

public sealed class ColumnTypeGrammarTests
{
    [Fact]
    public void ReadList_Entries_SeparatedByWhiteSpace()
    {
        var invalid = new List<string>();

        ColumnTypeGrammar
            .ReadList(
                " users.id=UserId\n\tposts.created_by_user_id=UserId  billing.invoices.id=MyApp.Billing.InvoiceId ",
                SettingPlace.Property,
                invalid
            )
            .ShouldBe([
                new ColumnType("billing.invoices.id", "global::MyApp.Billing.InvoiceId", SettingPlace.Property),
                new ColumnType("posts.created_by_user_id", "UserId", SettingPlace.Property),
                new ColumnType("users.id", "UserId", SettingPlace.Property),
            ]);
        invalid.ShouldBeEmpty();
    }

    // White space inside <>, [] and quotes belongs to the entry, as in the type override.
    [Fact]
    public void ReadList_WhiteSpaceInsideBracketsAndQuotes_IsPartOfTheEntry()
    {
        var invalid = new List<string>();

        ColumnTypeGrammar
            .ReadList(
                "\"my schema\".\"user tags\".ids=System.Collections.Generic.Dictionary<string, int[ , ]>",
                SettingPlace.Metadata,
                invalid
            )
            .ShouldBe([
                new ColumnType(
                    "\"my schema\".\"user tags\".ids",
                    "global::System.Collections.Generic.Dictionary<string, int[,]>",
                    SettingPlace.Metadata
                ),
            ]);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadList_InvalidEntry_IsReportedAndTheOthersApply()
    {
        var invalid = new List<string>();

        ColumnTypeGrammar
            .ReadList(
                "users.id=UserId id=UserId posts.id posts.id=not-a-type a.b.c.d=X",
                SettingPlace.Property,
                invalid
            )
            .ShouldBe([new ColumnType("users.id", "UserId", SettingPlace.Property)]);
        invalid.ShouldBe(["id=UserId", "posts.id", "posts.id=not-a-type", "a.b.c.d=X"]);
    }

    [Fact]
    public void ReadList_LastEntryOfAKey_Wins()
    {
        var invalid = new List<string>();

        ColumnTypeGrammar
            .ReadList("users.id=UserId posts.id=PostId \"users\".id=default", SettingPlace.Property, invalid)
            .ShouldBe([
                new ColumnType("posts.id", "PostId", SettingPlace.Property),
                new ColumnType("\"users\".id", null, SettingPlace.Property),
            ]);
        invalid.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("users.id=default")]
    [InlineData("users.id=DEFAULT")]
    public void ReadList_Default_HasNoType(string text) =>
        ColumnTypeGrammar
            .ReadList(text, SettingPlace.Property, [])
            .ShouldBe([new ColumnType("users.id", null, SettingPlace.Property)]);

    [Fact]
    public void ReadList_Nothing_IsEmpty() =>
        ColumnTypeGrammar.ReadList("  ", SettingPlace.Property, []).ShouldBeEmpty();

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
