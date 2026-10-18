using System.Collections.Immutable;
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
    public void Over_KeysOfBoth_AreMergedInTheOrderOfTheKeysIgnoringCase()
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
                    Entry("text", "string", SettingPlace.Metadata)
                )
            );
    }

    [Fact]
    public void Over_DefaultAbove_HidesTheKeyBelowAndInEffectDropsIt()
    {
        var lower = Map(
            Entry("int4", "long", SettingPlace.Property),
            Entry("timestamptz", "global::System.DateTimeOffset", SettingPlace.Property)
        );
        var higher = Map(Entry("timestamptz", null, SettingPlace.FileHeader));

        var merged = TypeOverrideMap.Over(higher, lower);

        merged.ShouldBe(
            Map(Entry("int4", "long", SettingPlace.Property), Entry("timestamptz", null, SettingPlace.FileHeader))
        );
        TypeOverrideMap.InEffect(merged).ShouldBe(Map(Entry("int4", "long", SettingPlace.Property)));
    }

    // A default kept in a merged map still hides a key of a level merged under it later.
    [Fact]
    public void Over_DefaultMergedOverALowerLevel_StillHidesItsKey()
    {
        var property = Map(Entry("int4", "long", SettingPlace.Property));
        var metadata = Map(Entry("int4", null, SettingPlace.Metadata));
        var markers = Map(Entry("text", "string", SettingPlace.Query));

        TypeOverrideMap
            .InEffect(TypeOverrideMap.Over(markers, TypeOverrideMap.Over(metadata, property)))
            .ShouldBe(Map(Entry("text", "string", SettingPlace.Query)));
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

    [Fact]
    public void InEffect_Null_IsEmpty() => TypeOverrideMap.InEffect(null).ShouldBe(EquatableArray<TypeOverride>.Empty);

    [Fact]
    public void InEffect_OnlyDefaults_IsEmpty() =>
        TypeOverrideMap
            .InEffect(Map(Entry("int4", null, SettingPlace.Query), Entry("text", null, SettingPlace.Query)))
            .ShouldBe(EquatableArray<TypeOverride>.Empty);

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
        var map = TypeOverrideMap.InEffect(
            Map(
                Entry("int4", "long", SettingPlace.Property),
                Entry("public.USER_STATUS", "global::App.Status", SettingPlace.Property),
                Entry("text", "string", SettingPlace.Property)
            )
        );

        TypeOverrideMap.Find(map, key)?.Type.ShouldBe(type);
        (TypeOverrideMap.Find(map, key) is null).ShouldBe(type is null);
    }

    [Fact]
    public void Find_EmptyMap_FindsNothing() =>
        TypeOverrideMap.Find(EquatableArray<TypeOverride>.Empty, "int4").ShouldBeNull();

    private static TypeOverride Entry(string key, string? type, SettingPlace place) => new(key, type, place);

    private static EquatableArray<TypeOverride> Map(params TypeOverride[] entries) =>
        new(ImmutableArray.Create(entries));
}
