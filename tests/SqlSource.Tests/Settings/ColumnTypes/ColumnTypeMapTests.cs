using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using Xunit;

namespace SqlSource.Tests.Settings.ColumnTypes;

public sealed class ColumnTypeMapTests
{
    [Fact]
    public void Find_TwoPartKey_MatchesAnySchema()
    {
        var map = Map(Entry("users.id", "UserId", SettingPlace.Property));

        ColumnTypeMap.Find(map, "public", "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "auth", "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, null, "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "public", "users", "name").ShouldBeNull();
        ColumnTypeMap.Find(map, "public", "posts", "id").ShouldBeNull();
    }

    [Fact]
    public void Find_ThreePartKey_MatchesItsSchemaAlone()
    {
        var map = Map(Entry("billing.invoices.id", "InvoiceId", SettingPlace.Property));

        ColumnTypeMap.Find(map, "billing", "invoices", "id")!.Type.ShouldBe("InvoiceId");
        ColumnTypeMap.Find(map, "BILLING", "Invoices", "ID")!.Type.ShouldBe("InvoiceId");
        ColumnTypeMap.Find(map, "public", "invoices", "id").ShouldBeNull();
        ColumnTypeMap.Find(map, null, "invoices", "id").ShouldBeNull();
    }

    [Fact]
    public void Find_SchemaWinsOverNoSchema_InOnePlace()
    {
        var map = Map(
            Entry("users.id", "UserId", SettingPlace.Metadata),
            Entry("auth.users.id", "AuthUserId", SettingPlace.Metadata)
        );

        ColumnTypeMap.Find(map, "auth", "users", "id")!.Type.ShouldBe("AuthUserId");
        ColumnTypeMap.Find(map, "public", "users", "id")!.Type.ShouldBe("UserId");
    }

    [Fact]
    public void Find_HigherPlaceWins_OverASchemaBelow()
    {
        var map = ColumnTypeMap
            .Over(
                Map(Entry("users.id", "UserId", SettingPlace.FileHeader)),
                Map(Entry("auth.users.id", "AuthUserId", SettingPlace.Property))
            )!
            .Value;

        ColumnTypeMap.Find(map, "auth", "users", "id")!.Type.ShouldBe("UserId");
    }

    [Fact]
    public void Find_QuotedPart_ComparesExactly()
    {
        var map = Map(Entry("\"Users\".id", "UserId", SettingPlace.Property));

        ColumnTypeMap.Find(map, "public", "Users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "public", "Users", "ID")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "public", "users", "id").ShouldBeNull();
        ColumnTypeMap.Find(map, "public", "USERS", "id").ShouldBeNull();
    }

    // A quoted schema too: a two-part key matches every schema, a quoted one its own case alone.
    [Fact]
    public void Find_QuotedSchema_ComparesExactly()
    {
        var map = Map(Entry("\"Auth\".users.id", "UserId", SettingPlace.Property));

        ColumnTypeMap.Find(map, "Auth", "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "auth", "users", "id").ShouldBeNull();
    }

    // Within one place, of two keys that differ only by case, the one spelled as the origin is.
    [Fact]
    public void Find_ExactSpelling_WinsOverAMatchIgnoringCase()
    {
        var map = Map(
            Entry("Users.Id", "Upper", SettingPlace.Property),
            Entry("users.id", "Lower", SettingPlace.Property)
        );

        ColumnTypeMap.Find(map, "public", "users", "id")!.Type.ShouldBe("Lower");
        ColumnTypeMap.Find(map, "public", "Users", "Id")!.Type.ShouldBe("Upper");
    }

    [Fact]
    public void Find_TextWithAQuoteInAQuotedPart_IsFound()
    {
        var map = Map(Entry("\"a\"\"b\".id", "AbId", SettingPlace.Property));

        ColumnTypeMap.Find(map, null, "a\"b", "id")!.Type.ShouldBe("AbId");
    }

    [Fact]
    public void Find_EmptyMap_FindsNothing() =>
        ColumnTypeMap.Find(EquatableArray<ColumnType>.Empty, "public", "users", "id").ShouldBeNull();

    [Fact]
    public void Over_Default_HidesTheKeyBelow()
    {
        var lower = Map(
            Entry("posts.id", "PostId", SettingPlace.Property),
            Entry("users.id", "UserId", SettingPlace.Property)
        );
        var higher = Map(Entry("users.id", null, SettingPlace.FileHeader));

        var merged = ColumnTypeMap.Over(higher, lower)!.Value;

        merged.ShouldBe(
            Map(Entry("posts.id", "PostId", SettingPlace.Property), Entry("users.id", null, SettingPlace.FileHeader))
        );
        ColumnTypeMap.Find(merged, "public", "users", "id").ShouldBeNull();
        ColumnTypeMap.Find(merged, "public", "posts", "id")!.Type.ShouldBe("PostId");
    }

    // A default of a two-part key above hides a three-part key below too: it wins by its place.
    [Fact]
    public void Over_DefaultAbove_WinsOverASchemaBelow()
    {
        var merged = ColumnTypeMap
            .Over(
                Map(Entry("users.id", null, SettingPlace.Metadata)),
                Map(Entry("auth.users.id", "AuthUserId", SettingPlace.Property))
            )!
            .Value;

        ColumnTypeMap.Find(merged, "auth", "users", "id").ShouldBeNull();
    }

    [Fact]
    public void Over_Higher_ReplacesOnlyTheKeysItNames()
    {
        var lower = Map(
            Entry("posts.id", "PostId", SettingPlace.Property),
            Entry("users.id", "UserId", SettingPlace.Property)
        );
        var higher = Map(Entry("users.id", "global::App.UserId", SettingPlace.Metadata));

        ColumnTypeMap
            .Over(higher, lower)
            .ShouldBe(
                Map(
                    Entry("posts.id", "PostId", SettingPlace.Property),
                    Entry("users.id", "global::App.UserId", SettingPlace.Metadata)
                )
            );
    }

    // A quoted part and a plain part of one text are one key, and keys that differ only by case are two.
    [Fact]
    public void Over_QuotedAndPlainSpellingOfOneText_AreOneKey()
    {
        ColumnTypeMap
            .Over(
                Map(Entry("\"users\".id", "A", SettingPlace.Metadata)),
                Map(Entry("users.id", "B", SettingPlace.Property))
            )
            .ShouldBe(Map(Entry("\"users\".id", "A", SettingPlace.Metadata)));

        ColumnTypeMap
            .Over(
                Map(Entry("Users.id", "A", SettingPlace.Metadata)),
                Map(Entry("users.id", "B", SettingPlace.Property))
            )!
            .Value.Count.ShouldBe(2);
    }

    // The keys of one table and column sit together, with or without a schema, the key without one first.
    [Fact]
    public void Over_Keys_AreInTheOrderOfTheTableTheColumnAndThenTheSchema()
    {
        var merged = ColumnTypeMap
            .Over(
                Map(
                    Entry("posts.id", "P", SettingPlace.Metadata),
                    Entry("z.posts.id", "Z", SettingPlace.Metadata),
                    Entry("a.users.id", "A", SettingPlace.Metadata)
                ),
                Map(Entry("users.id", "U", SettingPlace.Property), Entry("users.name", "N", SettingPlace.Property))
            )!
            .Value;

        merged.Select(entry => entry.Key).ShouldBe(["posts.id", "z.posts.id", "users.id", "a.users.id", "users.name"]);
    }

    [Fact]
    public void Over_NullOverNull_IsNull() => ColumnTypeMap.Over(null, null).ShouldBeNull();

    [Fact]
    public void Over_OneSideNull_IsTheOther()
    {
        var map = Map(Entry("users.id", "UserId", SettingPlace.Property));

        ColumnTypeMap.Over(map, null).ShouldBe(map);
        ColumnTypeMap.Over(null, map).ShouldBe(map);
    }

    [Fact]
    public void IndexOf_Key_IsItsPlaceOrTheComplementOfWhereItWouldGo()
    {
        var entries = new List<ColumnType>
        {
            Entry("posts.id", "P", SettingPlace.Property),
            Entry("users.id", "U", SettingPlace.Property),
        };

        ColumnTypeMap.IndexOf(entries, "users.id").ShouldBe(1);
        ColumnTypeMap.IndexOf(entries, "\"users\".id").ShouldBe(1);
        ColumnTypeMap.IndexOf(entries, "public.users.id").ShouldBe(~2);
        ColumnTypeMap.IndexOf(entries, "a.id").ShouldBe(~0);
    }

    private static ColumnType Entry(string key, string? type, SettingPlace place) => new(key, type, place);

    private static EquatableArray<ColumnType> Map(params ColumnType[] entries) => new(ImmutableArray.Create(entries));
}
