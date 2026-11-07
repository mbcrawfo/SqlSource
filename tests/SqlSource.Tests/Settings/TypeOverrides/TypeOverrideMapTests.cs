using System;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Tests.Settings.TestPlaces;

namespace SqlSource.Tests.Settings.TypeOverrides;

public sealed class TypeOverrideMapTests
{
    [Fact]
    public void Over_Higher_ReplacesOnlyTheKeysItNames()
    {
        var lower = TestSidecars.Of(
            Entry("int4", "int", RootSettingsFile),
            Entry("timestamptz", "global::System.DateTimeOffset", RootSettingsFile)
        );
        var higher = TestSidecars.Of(Entry("timestamptz", "global::System.DateTime", SettingPlace.Query));

        TypeOverrideMap
            .Over(higher, lower)
            .ShouldBe(
                TestSidecars.Of(
                    Entry("int4", "int", RootSettingsFile),
                    Entry("timestamptz", "global::System.DateTime", SettingPlace.Query)
                )
            );
    }

    [Fact]
    public void Over_KeysThatDifferByCase_AreTwoKeys()
    {
        var lower = TestSidecars.Of(
            Entry("bool", "bool", RootSettingsFile),
            Entry("public.Status", "global::App.S", RootSettingsFile)
        );
        var higher = TestSidecars.Of(
            Entry("int4", "long", SettingsFile),
            Entry("PUBLIC.status", "global::App.T", SettingsFile),
            Entry("text", "string", SettingsFile)
        );

        TypeOverrideMap
            .Over(higher, lower)
            .ShouldBe(
                TestSidecars.Of(
                    Entry("bool", "bool", RootSettingsFile),
                    Entry("int4", "long", SettingsFile),
                    Entry("PUBLIC.status", "global::App.T", SettingsFile),
                    Entry("public.Status", "global::App.S", RootSettingsFile),
                    Entry("text", "string", SettingsFile)
                )
            );
    }

    [Fact]
    public void Over_QuotedAndPlainSpellingOfOneText_AreOneKey() =>
        TypeOverrideMap
            .Over(
                TestSidecars.Of(Entry("public.\"status\"", "long", SettingPlace.Query)),
                TestSidecars.Of(Entry("public.status", "int", RootSettingsFile))
            )
            .ShouldBe(TestSidecars.Of(Entry("public.\"status\"", "long", SettingPlace.Query)));

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
                TestSidecars.Of(
                    Entry("public.tag[]", "C", SettingPlace.Query),
                    Entry("public.x", "D", SettingPlace.Query)
                ),
                TestSidecars.Of(Entry("public", "A", RootSettingsFile), Entry("public.tag", "B", RootSettingsFile))
            )!
            .Value;

        merged.Select(entry => entry.Key).ShouldBe(["public", "public.tag", "public.tag[]", "public.x"]);
        TypeOverrideMap.Find(merged, key)!.Type.ShouldBe(type);
    }

    [Fact]
    public void Over_DefaultAbove_HidesTheKeyBelowAndFindFindsNothing()
    {
        var lower = TestSidecars.Of(
            Entry("int4", "long", RootSettingsFile),
            Entry("timestamptz", "global::System.DateTimeOffset", RootSettingsFile)
        );
        var higher = TestSidecars.Of(Entry("timestamptz", null, SettingPlace.FileHeader));

        var merged = TypeOverrideMap.Over(higher, lower)!.Value;

        merged.ShouldBe(
            TestSidecars.Of(
                Entry("int4", "long", RootSettingsFile),
                Entry("timestamptz", null, SettingPlace.FileHeader)
            )
        );
        TypeOverrideMap.Find(merged, "timestamptz").ShouldBeNull();
        TypeOverrideMap.Find(merged, "int4")!.Type.ShouldBe("long");
    }

    // A default kept in a merged map still hides a key of a level merged under it later.
    [Fact]
    public void Over_DefaultMergedOverALowerLevel_StillHidesItsKey()
    {
        var farther = TestSidecars.Of(Entry("int4", "long", RootSettingsFile));
        var nearer = TestSidecars.Of(Entry("int4", null, SettingsFile));
        var markers = TestSidecars.Of(Entry("text", "string", SettingPlace.Query));

        var merged = TypeOverrideMap.Over(markers, TypeOverrideMap.Over(nearer, farther))!.Value;

        merged.ShouldBe(
            TestSidecars.Of(Entry("int4", null, SettingsFile), Entry("text", "string", SettingPlace.Query))
        );
        TypeOverrideMap.Find(merged, "int4").ShouldBeNull();
    }

    [Fact]
    public void Over_NullOverNull_IsNull() => TypeOverrideMap.Over(null, null).ShouldBeNull();

    [Fact]
    public void Over_OneSideNull_IsTheOther()
    {
        var map = TestSidecars.Of(Entry("int4", "long", RootSettingsFile));

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
        var map = TestSidecars.Of(
            Entry("int4", "long", RootSettingsFile),
            Entry("public.USER_STATUS", "global::App.Status", RootSettingsFile),
            Entry("text", "string", RootSettingsFile)
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
        var map = TestSidecars.Of(
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
        var map = TestSidecars.Of(Entry("public.\"Status\"", "global::App.A", SettingPlace.Query));

        TypeOverrideMap.Find(map, key)?.Type.ShouldBe(type);
        (TypeOverrideMap.Find(map, key) is null).ShouldBe(type is null);
    }

    [Fact]
    public void Find_EscapedPart_MatchesTheQuotedPartOfItsText()
    {
        TypeOverrideGrammar
            .TryReadEntry(
                "public.\"Status\"=int".AsSpan(),
                SettingPlace.Query,
                DatabaseEngine.PostgreSql,
                out var entry
            )
            .ShouldBeTrue();
        var map = TestSidecars.Of(entry!);

        TypeOverrideMap.Find(map, "public.U&\"St\\0061tus\"").ShouldBe(map[0]);
        TypeOverrideMap.Find(map, "public.U&\"st\\0061tus\"").ShouldBeNull();
    }

    [Fact]
    public void Find_QuotedAndPlainEntriesEqualIgnoringCase_TakesTheOneThatMatches()
    {
        var map = TestSidecars.Of(
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
            TestSidecars.Of(Entry("public.status", null, SettingPlace.Query)),
            TestSidecars.Of(Entry("public.Status", "global::App.A", RootSettingsFile))
        )!;

        TypeOverrideMap.Find(merged.Value, key).ShouldBeNull();
    }

    // The entry of the more specific place wins, though the one below is spelled exactly as the type's key.
    [Theory]
    [InlineData("SettingsFile", "RootSettingsFile")]
    [InlineData("FileHeader", "SettingsFile")]
    [InlineData("Query", "FileHeader")]
    [InlineData("Query", "RootSettingsFile")]
    public void Find_PlainEntryAboveAnExactOne_Wins(string higher, string lower)
    {
        var above = Entry("public.User_Status", "global::App.B", PlaceNamed(higher));
        var merged = TypeOverrideMap.Over(
            TestSidecars.Of(above),
            TestSidecars.Of(Entry("public.user_status", "global::App.A", PlaceNamed(lower)))
        )!;

        TypeOverrideMap.Find(merged.Value, "public.user_status").ShouldBe(above);
    }

    // A quoted entry above that does not match the type leaves the plain one below in force.
    [Fact]
    public void Find_QuotedEntryAboveThatDoesNotMatch_LeavesTheOneBelow()
    {
        var merged = TypeOverrideMap.Over(
            TestSidecars.Of(Entry("public.\"Status\"", "global::App.A", SettingPlace.Query)),
            TestSidecars.Of(Entry("public.status", "global::App.B", RootSettingsFile))
        )!;

        TypeOverrideMap
            .Find(merged.Value, "public.status")
            .ShouldBe(Entry("public.status", "global::App.B", RootSettingsFile));
        TypeOverrideMap.Find(merged.Value, "public.Status")!.Type.ShouldBe("global::App.A");
    }

    [Theory]
    [InlineData("\"a.b\".c", "global::App.A")]
    [InlineData("a.\"b.c\"", "global::App.B")]
    public void Find_DotInsideQuotes_IsPartOfThePart(string key, string type)
    {
        var map = TestSidecars.Of(
            Entry("a.\"b.c\"", "global::App.B", SettingPlace.Query),
            Entry("\"a.b\".c", "global::App.A", SettingPlace.Query)
        );

        TypeOverrideMap.Find(map, key)!.Type.ShouldBe(type);
    }

    [Fact]
    public void Find_QuotedSchemaAndName_IsFound()
    {
        var map = TestSidecars.Of(Entry("\"my schema\".\"order-status\"", "global::App.A", SettingPlace.Query));

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
        var map = TestSidecars.Of(Entry("public._tag", null, RootSettingsFile));

        TypeOverrideMap.Winner(map, "public._tag")!.Type.ShouldBeNull();
        TypeOverrideMap.Find(map, "public._tag").ShouldBeNull();
    }

    [Fact]
    public void Winner_EntryOfAHigherPlace_WinsOverTheOneBelow()
    {
        var above = Entry("public._tag", null, SettingPlace.Query);
        var merged = TypeOverrideMap.Over(
            TestSidecars.Of(above),
            TestSidecars.Of(Entry("public._tag", "global::App.A", RootSettingsFile))
        )!;

        TypeOverrideMap.Winner(merged.Value, "public._tag").ShouldBe(above);
    }

    [Fact]
    public void Winner_NoEntryMatches_IsNull() =>
        TypeOverrideMap
            .Winner(TestSidecars.Of(Entry("public.tag[]", "global::App.A", RootSettingsFile)), "public._tag")
            .ShouldBeNull();

    private static SettingPlace PlaceNamed(string name) =>
        name switch
        {
            "SettingsFile" => SettingsFile,
            "RootSettingsFile" => RootSettingsFile,
            "FileHeader" => SettingPlace.FileHeader,
            "Query" => SettingPlace.Query,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such place in this test."),
        };

    private static TypeOverride Entry(string key, string? type, SettingPlace place) => new(key, type, place);
}
