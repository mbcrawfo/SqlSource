using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class SettingPlacesTests
{
    // A settings file is more specific the nearer its folder is to the query's, and every one of them is below the
    // file's own header and above the attribute.
    [Fact]
    public void Specificity_OfEachPlace_RanksQueryThenHeaderThenSettingsFilesThenAttribute()
    {
        var ranks = new[]
        {
            SettingPlace.Query,
            SettingPlace.FileHeader,
            SettingPlace.OfSettingsFile("/a/b/sqlsource.json", 2),
            SettingPlace.OfSettingsFile("/a/sqlsource.json", 1),
            SettingPlace.OfSettingsFile("/sqlsource.json", 0),
            SettingPlace.Attribute,
        };

        var specificities = new int[ranks.Length];
        for (var at = 0; at < ranks.Length; at++)
        {
            specificities[at] = SettingPlaces.Specificity(ranks[at]);
        }

        for (var at = 1; at < specificities.Length; at++)
        {
            specificities[at - 1].ShouldBeGreaterThan(specificities[at]);
        }
    }

    // Neither the type override nor the column types has an attribute level, which ranks below every settings file.
    [Fact]
    public void Specificity_SettingsFiles_RankNearerAboveFartherAndBelowTheMarkers()
    {
        var root = SettingPlace.OfSettingsFile("/sqlsource.json", 0);
        var nearer = SettingPlace.OfSettingsFile("/a/b/sqlsource.json", 2);
        SettingPlaces.Specificity(root).ShouldBe(1);
        SettingPlaces.Specificity(SettingPlace.Attribute).ShouldBe(0);
        SettingPlaces.Specificity(nearer).ShouldBeGreaterThan(SettingPlaces.Specificity(root));
        SettingPlaces.Specificity(SettingPlace.FileHeader).ShouldBeGreaterThan(SettingPlaces.Specificity(nearer));
    }

    [Fact]
    public void Specificity_OfADeepSettingsFile_StaysBelowTheFileHeader() =>
        SettingPlaces
            .Specificity(SettingPlace.OfSettingsFile("/deep/sqlsource.json", 10_000))
            .ShouldBeLessThan(SettingPlaces.Specificity(SettingPlace.FileHeader));

    [Fact]
    public void OfSettingsFile_Path_IsTheSettingsFileKindWithItsPathAndDepth()
    {
        var place = SettingPlace.OfSettingsFile("/app/sqlsource.json", 3);

        place.Kind.ShouldBe(SettingPlaceKind.SettingsFile);
        place.Path.ShouldBe("/app/sqlsource.json");
        place.Depth.ShouldBe(3);
    }

    // Only a settings file has a file of its own.
    [Fact]
    public void Places_OfTheOtherKinds_HaveNoPathAndNoDepth()
    {
        foreach (var place in new[] { SettingPlace.Attribute, SettingPlace.FileHeader, SettingPlace.Query })
        {
            place.Path.ShouldBeNull();
            place.Depth.ShouldBe(0);
        }
    }

    // A place is a value: entries that carry it compare by it.
    [Fact]
    public void OfSettingsFile_SamePathAndDepth_AreEqual() =>
        SettingPlace
            .OfSettingsFile("/app/sqlsource.json", 1)
            .ShouldBe(SettingPlace.OfSettingsFile("/app/sqlsource.json", 1));
}
