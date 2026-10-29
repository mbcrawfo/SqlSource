using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Settings.ColumnTypes;

public sealed class ColumnTypeMapTests
{
    // A settings file in the folder of the files, and one in a folder above it, which ranks below the nearer.
    private static readonly SettingPlace Near = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);
    private static readonly SettingPlace Far = SettingPlace.OfSettingsFile("/sqlsource.json", 0);

    [Fact]
    public void Find_TwoPartKey_MatchesAnySchema()
    {
        var map = TestSidecars.Of(Entry("users.id", "UserId", Far));

        ColumnTypeMap.Find(map, "public", "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "auth", "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, null, "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "public", "users", "name").ShouldBeNull();
        ColumnTypeMap.Find(map, "public", "posts", "id").ShouldBeNull();
    }

    [Fact]
    public void Find_ThreePartKey_MatchesItsSchemaAlone()
    {
        var map = TestSidecars.Of(Entry("billing.invoices.id", "InvoiceId", Far));

        ColumnTypeMap.Find(map, "billing", "invoices", "id")!.Type.ShouldBe("InvoiceId");
        ColumnTypeMap.Find(map, "BILLING", "Invoices", "ID")!.Type.ShouldBe("InvoiceId");
        ColumnTypeMap.Find(map, "public", "invoices", "id").ShouldBeNull();
        ColumnTypeMap.Find(map, null, "invoices", "id").ShouldBeNull();
    }

    [Fact]
    public void Find_SchemaWinsOverNoSchema_InOnePlace()
    {
        var map = TestSidecars.Of(Entry("users.id", "UserId", Near), Entry("auth.users.id", "AuthUserId", Near));

        ColumnTypeMap.Find(map, "auth", "users", "id")!.Type.ShouldBe("AuthUserId");
        ColumnTypeMap.Find(map, "public", "users", "id")!.Type.ShouldBe("UserId");
    }

    [Fact]
    public void Find_HigherPlaceWins_OverASchemaBelow()
    {
        var map = ColumnTypeMap
            .Over(
                TestSidecars.Of(Entry("users.id", "UserId", SettingPlace.FileHeader)),
                TestSidecars.Of(Entry("auth.users.id", "AuthUserId", Far))
            )!
            .Value;

        ColumnTypeMap.Find(map, "auth", "users", "id")!.Type.ShouldBe("UserId");
    }

    // The header's entry wins by its place, though the settings file's key names the schema.
    [Fact]
    public void Find_HeaderWins_OverASchemaInASettingsFile()
    {
        var map = ColumnTypeMap
            .Over(
                TestSidecars.Of(Entry("users.id", "UserId", SettingPlace.FileHeader)),
                TestSidecars.Of(Entry("public.users.id", "OtherId", Near))
            )!
            .Value;

        ColumnTypeMap.Find(map, "public", "users", "id")!.Type.ShouldBe("UserId");
    }

    [Fact]
    public void Find_QuotedPart_ComparesExactly()
    {
        var map = TestSidecars.Of(Entry("\"Users\".id", "UserId", Far));

        ColumnTypeMap.Find(map, "public", "Users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "public", "Users", "ID")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "public", "users", "id").ShouldBeNull();
        ColumnTypeMap.Find(map, "public", "USERS", "id").ShouldBeNull();
    }

    // A quoted schema too: a two-part key matches every schema, a quoted one its own case alone.
    [Fact]
    public void Find_QuotedSchema_ComparesExactly()
    {
        var map = TestSidecars.Of(Entry("\"Auth\".users.id", "UserId", Far));

        ColumnTypeMap.Find(map, "Auth", "users", "id")!.Type.ShouldBe("UserId");
        ColumnTypeMap.Find(map, "auth", "users", "id").ShouldBeNull();
    }

    // Within one place, of two keys that differ only by case, the one spelled exactly as the column's origin wins.
    [Fact]
    public void Find_ExactSpelling_WinsOverAMatchIgnoringCase()
    {
        var map = TestSidecars.Of(Entry("Users.Id", "Upper", Far), Entry("users.id", "Lower", Far));

        ColumnTypeMap.Find(map, "public", "users", "id")!.Type.ShouldBe("Lower");
        ColumnTypeMap.Find(map, "public", "Users", "Id")!.Type.ShouldBe("Upper");
    }

    // Within one place, a schema weighs more than the spelling: the key with a schema wins though the other is spelled
    // as the origin is and it is not.
    [Fact]
    public void Find_KeyWithASchemaAndOneSpelledAsTheOriginInOnePlace_IsTheOneWithTheSchema()
    {
        var map = ColumnTypeMap
            .Over(TestSidecars.Of(Entry("Users.id", "A", Far)), TestSidecars.Of(Entry("auth.users.id", "B", Far)))!
            .Value;

        ColumnTypeMap.Find(map, "auth", "Users", "id")!.Type.ShouldBe("B");
    }

    [Fact]
    public void Find_TextWithAQuoteInAQuotedPart_IsFound()
    {
        var map = TestSidecars.Of(Entry("\"a\"\"b\".id", "AbId", Far));

        ColumnTypeMap.Find(map, null, "a\"b", "id")!.Type.ShouldBe("AbId");
    }

    [Fact]
    public void Find_EmptyMap_FindsNothing() =>
        ColumnTypeMap.Find(EquatableArray<ColumnType>.Empty, "public", "users", "id").ShouldBeNull();

    [Fact]
    public void Over_Default_HidesTheKeyBelow()
    {
        var lower = TestSidecars.Of(Entry("posts.id", "PostId", Far), Entry("users.id", "UserId", Far));
        var higher = TestSidecars.Of(Entry("users.id", null, SettingPlace.FileHeader));

        var merged = ColumnTypeMap.Over(higher, lower)!.Value;

        merged.ShouldBe(
            TestSidecars.Of(Entry("posts.id", "PostId", Far), Entry("users.id", null, SettingPlace.FileHeader))
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
                TestSidecars.Of(Entry("users.id", null, Near)),
                TestSidecars.Of(Entry("auth.users.id", "AuthUserId", Far))
            )!
            .Value;

        ColumnTypeMap.Find(merged, "auth", "users", "id").ShouldBeNull();
    }

    [Fact]
    public void Over_Higher_ReplacesOnlyTheKeysItNames()
    {
        var lower = TestSidecars.Of(Entry("posts.id", "PostId", Far), Entry("users.id", "UserId", Far));
        var higher = TestSidecars.Of(Entry("users.id", "global::App.UserId", Near));

        ColumnTypeMap
            .Over(higher, lower)
            .ShouldBe(TestSidecars.Of(Entry("posts.id", "PostId", Far), Entry("users.id", "global::App.UserId", Near)));
    }

    // A quoted part and a plain part of one text are one key, and keys that differ only by case are two.
    [Fact]
    public void Over_QuotedAndPlainSpellingOfOneText_AreOneKey()
    {
        ColumnTypeMap
            .Over(TestSidecars.Of(Entry("\"users\".id", "A", Near)), TestSidecars.Of(Entry("users.id", "B", Far)))
            .ShouldBe(TestSidecars.Of(Entry("\"users\".id", "A", Near)));

        ColumnTypeMap
            .Over(TestSidecars.Of(Entry("Users.id", "A", Near)), TestSidecars.Of(Entry("users.id", "B", Far)))!
            .Value.Count.ShouldBe(2);
    }

    // The keys of one table and column sit together, with or without a schema, the key without one first.
    [Fact]
    public void Over_Keys_AreInTheOrderOfTheTableTheColumnAndThenTheSchema()
    {
        var merged = ColumnTypeMap
            .Over(
                TestSidecars.Of(
                    Entry("posts.id", "P", Near),
                    Entry("z.posts.id", "Z", Near),
                    Entry("a.users.id", "A", Near)
                ),
                TestSidecars.Of(Entry("users.id", "U", Far), Entry("users.name", "N", Far))
            )!
            .Value;

        merged.Select(entry => entry.Key).ShouldBe(["posts.id", "z.posts.id", "users.id", "a.users.id", "users.name"]);
    }

    [Fact]
    public void Over_NullOverNull_IsNull() => ColumnTypeMap.Over(null, null).ShouldBeNull();

    [Fact]
    public void Over_OneSideNull_IsTheOther()
    {
        var map = TestSidecars.Of(Entry("users.id", "UserId", Far));

        ColumnTypeMap.Over(map, null).ShouldBe(map);
        ColumnTypeMap.Over(null, map).ShouldBe(map);
    }

    [Fact]
    public void IndexOf_Key_IsItsPlaceOrTheComplementOfWhereItWouldGo()
    {
        var entries = new List<ColumnType> { Entry("posts.id", "P", Far), Entry("users.id", "U", Far) };

        ColumnTypeMap.IndexOf(entries, "users.id").ShouldBe(1);
        ColumnTypeMap.IndexOf(entries, "\"users\".id").ShouldBe(1);
        ColumnTypeMap.IndexOf(entries, "public.users.id").ShouldBe(~2);
        ColumnTypeMap.IndexOf(entries, "a.id").ShouldBe(~0);
    }

    private static ColumnType Entry(string key, string? type, SettingPlace place) => new(key, type, place);
}
