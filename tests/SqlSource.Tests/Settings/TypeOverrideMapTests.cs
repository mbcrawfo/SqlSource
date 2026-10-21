using System;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class TypeOverrideMapTests
{
    [Fact]
    public void Over_Higher_ReplacesOnlyTheKeysItNames()
    {
        var lower = Map(
            Entry("int4", "int", SettingPlace.Property),
            Entry("timestamptz", "global::System.DateTimeOffset", SettingPlace.Property)
        );
        var higher = Map(Entry("timestamptz", "global::System.DateTime", SettingPlace.Query));

        TypeOverrideMap
            .Over(higher, lower)
            .ShouldBe(
                Map(
                    Entry("int4", "int", SettingPlace.Property),
                    Entry("timestamptz", "global::System.DateTime", SettingPlace.Query)
                )
            );
    }

    [Fact]
    public void Over_KeysThatDifferByCase_AreTwoKeys()
    {
        var lower = Map(
            Entry("bool", "bool", SettingPlace.Property),
            Entry("public.Status", "global::App.S", SettingPlace.Property)
        );
        var higher = Map(
            Entry("int4", "long", SettingPlace.Metadata),
            Entry("PUBLIC.status", "global::App.T", SettingPlace.Metadata),
            Entry("text", "string", SettingPlace.Metadata)
        );

        TypeOverrideMap
            .Over(higher, lower)
            .ShouldBe(
                Map(
                    Entry("bool", "bool", SettingPlace.Property),
                    Entry("int4", "long", SettingPlace.Metadata),
                    Entry("PUBLIC.status", "global::App.T", SettingPlace.Metadata),
                    Entry("public.Status", "global::App.S", SettingPlace.Property),
                    Entry("text", "string", SettingPlace.Metadata)
                )
            );
    }

    [Fact]
    public void Over_QuotedAndPlainSpellingOfOneText_AreOneKey() =>
        TypeOverrideMap
            .Over(
                Map(Entry("public.\"status\"", "long", SettingPlace.Query)),
                Map(Entry("public.status", "int", SettingPlace.Property))
            )
            .ShouldBe(Map(Entry("public.\"status\"", "long", SettingPlace.Query)));

    // A shorter list of parts is first when the first parts are equal, and a key that is not an array before an array.
    [Theory]
    [InlineData("public", "A")]
    [InlineData("public.tag", "B")]
    [InlineData("public.tag[]", "C")]
    [InlineData("public.x", "D")]
    public void Over_ShorterPartsAndNonArrayFirst_AreOrderedAndFound(string key, string type)
    {
        var merged = TypeOverrideMap
            .Over(
                Map(Entry("public.tag[]", "C", SettingPlace.Query), Entry("public.x", "D", SettingPlace.Query)),
                Map(Entry("public", "A", SettingPlace.Property), Entry("public.tag", "B", SettingPlace.Property))
            )!
            .Value;

        merged.Select(entry => entry.Key).ShouldBe(["public", "public.tag", "public.tag[]", "public.x"]);
        TypeOverrideMap.Find(merged, key)!.Type.ShouldBe(type);
    }

    [Fact]
    public void Over_DefaultAbove_HidesTheKeyBelowAndFindFindsNothing()
    {
        var lower = Map(
            Entry("int4", "long", SettingPlace.Property),
            Entry("timestamptz", "global::System.DateTimeOffset", SettingPlace.Property)
        );
        var higher = Map(Entry("timestamptz", null, SettingPlace.FileHeader));

        var merged = TypeOverrideMap.Over(higher, lower)!.Value;

        merged.ShouldBe(
            Map(Entry("int4", "long", SettingPlace.Property), Entry("timestamptz", null, SettingPlace.FileHeader))
        );
        TypeOverrideMap.Find(merged, "timestamptz").ShouldBeNull();
        TypeOverrideMap.Find(merged, "int4")!.Type.ShouldBe("long");
    }

    // A default kept in a merged map still hides a key of a level merged under it later.
    [Fact]
    public void Over_DefaultMergedOverALowerLevel_StillHidesItsKey()
    {
        var property = Map(Entry("int4", "long", SettingPlace.Property));
        var metadata = Map(Entry("int4", null, SettingPlace.Metadata));
        var markers = Map(Entry("text", "string", SettingPlace.Query));

        var merged = TypeOverrideMap.Over(markers, TypeOverrideMap.Over(metadata, property))!.Value;

        merged.ShouldBe(Map(Entry("int4", null, SettingPlace.Metadata), Entry("text", "string", SettingPlace.Query)));
        TypeOverrideMap.Find(merged, "int4").ShouldBeNull();
    }

    [Fact]
    public void Over_NullOverNull_IsNull() => TypeOverrideMap.Over(null, null).ShouldBeNull();

    [Fact]
    public void Over_OneSideNull_IsTheOther()
    {
        var map = Map(Entry("int4", "long", SettingPlace.Property));

        TypeOverrideMap.Over(map, null).ShouldBe(map);
        TypeOverrideMap.Over(null, map).ShouldBe(map);
    }

    [Theory]
    [InlineData("int4", "long")]
    [InlineData("INT4", "long")]
    [InlineData("public.user_status", "global::App.Status")]
    [InlineData("Public.User_Status", "global::App.Status")]
    [InlineData("text", "string")]
    [InlineData("bool", null)]
    [InlineData("int8", null)]
    [InlineData("zzz", null)]
    public void Find_Key_IsFoundIgnoringCase(string key, string? type)
    {
        var map = Map(
            Entry("int4", "long", SettingPlace.Property),
            Entry("public.USER_STATUS", "global::App.Status", SettingPlace.Property),
            Entry("text", "string", SettingPlace.Property)
        );

        TypeOverrideMap.Find(map, key)?.Type.ShouldBe(type);
        (TypeOverrideMap.Find(map, key) is null).ShouldBe(type is null);
    }

    // Of two entries of one place that match, the one equal to the type's key exactly wins.
    [Theory]
    [InlineData("public.Status", "global::App.A")]
    [InlineData("public.status", "global::App.B")]
    [InlineData("public.\"Status\"", "global::App.A")]
    [InlineData("public.STATUS", "global::App.A")] // Neither is exact: the first in the order of the map.
    public void Find_KeysThatDifferByCase_PreferTheExactOne(string key, string type)
    {
        var map = Map(
            Entry("public.Status", "global::App.A", SettingPlace.Query),
            Entry("public.status", "global::App.B", SettingPlace.Query)
        );

        TypeOverrideMap.Find(map, key)!.Type.ShouldBe(type);
    }

    [Theory]
    [InlineData("public.Status", "global::App.A")]
    [InlineData("public.status", null)]
    [InlineData("Public.Status", "global::App.A")] // The schema is plain.
    public void Find_QuotedPart_MatchesItsCaseAlone(string key, string? type)
    {
        var map = Map(Entry("public.\"Status\"", "global::App.A", SettingPlace.Query));

        TypeOverrideMap.Find(map, key)?.Type.ShouldBe(type);
        (TypeOverrideMap.Find(map, key) is null).ShouldBe(type is null);
    }

    [Fact]
    public void Find_EscapedPart_MatchesTheQuotedPartOfItsText()
    {
        var map = TypeOverrideGrammar.ReadList("public.\"Status\"=int", SettingPlace.Query, []);

        TypeOverrideMap.Find(map, "public.U&\"St\\0061tus\"").ShouldBe(map[0]);
        TypeOverrideMap.Find(map, "public.U&\"st\\0061tus\"").ShouldBeNull();
    }

    [Fact]
    public void Find_QuotedAndPlainEntriesEqualIgnoringCase_TakesTheOneThatMatches()
    {
        var map = Map(
            Entry("public.\"Status\"", "global::App.A", SettingPlace.Query),
            Entry("public.status", "global::App.B", SettingPlace.Query)
        );

        TypeOverrideMap.Find(map, "public.STATUS")!.Type.ShouldBe("global::App.B");
    }

    // A query's default of the type's key spelled in another case still takes the type out of the entries below.
    [Theory]
    [InlineData("public.status")]
    [InlineData("public.Status")]
    public void Find_DefaultAboveThatDiffersByCase_FindsNothing(string key)
    {
        var merged = TypeOverrideMap.Over(
            Map(Entry("public.status", null, SettingPlace.Query)),
            Map(Entry("public.Status", "global::App.A", SettingPlace.Property))
        )!;

        TypeOverrideMap.Find(merged.Value, key).ShouldBeNull();
    }

    // The entry of the more specific place wins, though the one below is spelled exactly as the type's key.
    [Theory]
    [InlineData("Metadata", "Property")]
    [InlineData("FileHeader", "Metadata")]
    [InlineData("Query", "FileHeader")]
    [InlineData("Query", "Property")]
    public void Find_PlainEntryAboveAnExactOne_Wins(string higher, string lower)
    {
        var above = Entry("public.User_Status", "global::App.B", Enum.Parse<SettingPlace>(higher));
        var merged = TypeOverrideMap.Over(
            Map(above),
            Map(Entry("public.user_status", "global::App.A", Enum.Parse<SettingPlace>(lower)))
        )!;

        TypeOverrideMap.Find(merged.Value, "public.user_status").ShouldBe(above);
    }

    // A quoted entry above that does not match the type leaves the plain one below in force.
    [Fact]
    public void Find_QuotedEntryAboveThatDoesNotMatch_LeavesTheOneBelow()
    {
        var merged = TypeOverrideMap.Over(
            Map(Entry("public.\"Status\"", "global::App.A", SettingPlace.Query)),
            Map(Entry("public.status", "global::App.B", SettingPlace.Property))
        )!;

        TypeOverrideMap
            .Find(merged.Value, "public.status")
            .ShouldBe(Entry("public.status", "global::App.B", SettingPlace.Property));
        TypeOverrideMap.Find(merged.Value, "public.Status")!.Type.ShouldBe("global::App.A");
    }

    [Theory]
    [InlineData("\"a.b\".c", "global::App.A")]
    [InlineData("a.\"b.c\"", "global::App.B")]
    public void Find_DotInsideQuotes_IsPartOfThePart(string key, string type)
    {
        var map = Map(
            Entry("a.\"b.c\"", "global::App.B", SettingPlace.Query),
            Entry("\"a.b\".c", "global::App.A", SettingPlace.Query)
        );

        TypeOverrideMap.Find(map, key)!.Type.ShouldBe(type);
    }

    [Fact]
    public void Find_QuotedSchemaAndName_IsFound()
    {
        var map = Map(Entry("\"my schema\".\"order-status\"", "global::App.A", SettingPlace.Query));

        TypeOverrideMap.Find(map, "\"my schema\".\"order-status\"")!.Type.ShouldBe("global::App.A");
        TypeOverrideMap.Find(map, "\"My Schema\".\"order-status\"").ShouldBeNull();
    }

    [Fact]
    public void Find_EmptyMap_FindsNothing() =>
        TypeOverrideMap.Find(EquatableArray<TypeOverride>.Empty, "int4").ShouldBeNull();

    // The default that wins is the winner, though Find gives nothing for it.
    [Fact]
    public void Winner_DefaultThatWins_IsGivenWhereFindGivesNull()
    {
        var map = Map(Entry("public._tag", null, SettingPlace.Property));

        TypeOverrideMap.Winner(map, "public._tag")!.Type.ShouldBeNull();
        TypeOverrideMap.Find(map, "public._tag").ShouldBeNull();
    }

    [Fact]
    public void Winner_EntryOfAHigherPlace_WinsOverTheOneBelow()
    {
        var above = Entry("public._tag", null, SettingPlace.Query);
        var merged = TypeOverrideMap.Over(
            Map(above),
            Map(Entry("public._tag", "global::App.A", SettingPlace.Property))
        )!;

        TypeOverrideMap.Winner(merged.Value, "public._tag").ShouldBe(above);
    }

    [Fact]
    public void Winner_NoEntryMatches_IsNull() =>
        TypeOverrideMap
            .Winner(Map(Entry("public.tag[]", "global::App.A", SettingPlace.Property)), "public._tag")
            .ShouldBeNull();

    private static TypeOverride Entry(string key, string? type, SettingPlace place) => new(key, type, place);

    private static EquatableArray<TypeOverride> Map(params TypeOverride[] entries) =>
        new(ImmutableArray.Create(entries));
}
