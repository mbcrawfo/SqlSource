using System;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using Xunit;

namespace SqlSource.Tests.Settings.Libraries;

public sealed class LibrarySettingsTests
{
    private static readonly SettingPlace Near = SettingPlace.OfSettingsFile("/app/Queries/sqlsource.json", 2);

    private static readonly SettingPlace Far = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

    [Fact]
    public void Over_BothNull_IsNull() => LibrarySettings.Over(null, null).ShouldBeNull();

    [Fact]
    public void Over_OneNull_IsTheOtherItself()
    {
        var entries = new EquatableArray<LibrarySetting>([Entry(LibraryState.On, Far)]);

        LibrarySettings.Over(entries, null).ShouldBe(entries);
        LibrarySettings.Over(null, entries).ShouldBe(entries);
    }

    [Fact]
    public void Over_ReplacesOnlyTheLibrariesTheNearLevelNames()
    {
        var off = new EquatableArray<LibrarySetting>([Entry(LibraryState.Off, Near)]);
        var on = new EquatableArray<LibrarySetting>([Entry(LibraryState.On, Far)]);

        LibrarySettings.Over(off, on).ShouldNotBeNull().ShouldBe([Entry(LibraryState.Off, Near)]);
        LibrarySettings.Over(on, off).ShouldNotBeNull().ShouldBe([Entry(LibraryState.On, Far)]);
    }

    [Fact]
    public void Over_KeepsADefaultEntry()
    {
        var near = new EquatableArray<LibrarySetting>([Entry(LibraryState.Default, Near)]);
        var far = new EquatableArray<LibrarySetting>([Entry(LibraryState.On, Far)]);

        var merged = LibrarySettings.Over(near, far).ShouldNotBeNull();

        LibrarySettings.Find(merged, SqlLibrary.NodaTime).ShouldBe(Entry(LibraryState.Default, Near));
    }

    [Fact]
    public void Find_LibraryTheEntriesDoNotName_IsNull() =>
        LibrarySettings.Find(EquatableArray<LibrarySetting>.Empty, SqlLibrary.NodaTime).ShouldBeNull();

    [Theory]
    [InlineData("NodaTime=on", true, "NodaTime", "On")]
    [InlineData("nodaTime = OFF", true, "NodaTime", "Off")]
    [InlineData("noda time=default", true, "NodaTime", "Default")]
    [InlineData("  noda-time\t=\tDefault  ", true, "NodaTime", "Default")]
    [InlineData("netTopologySuite=on", true, "NetTopologySuite", "On")]
    [InlineData("net topology-suite = off", true, "NetTopologySuite", "Off")]
    // netTopologySuites is no name: a choice is matched whole.
    [InlineData("netTopologySuites=on", false, null, null)]
    // node-time is no name: it is node, not noda.
    [InlineData("node-time = OFF", false, null, null)]
    [InlineData("nodatime=", false, null, null)]
    [InlineData("nodatime", false, null, null)]
    [InlineData("nodatime=maybe", false, null, null)]
    [InlineData("=on", false, null, null)]
    [InlineData("json=on", true, "Json", "On")]
    [InlineData("  JSON = off ", true, "Json", "Off")]
    [InlineData("jsn=on", false, null, null)]
    public void TryReadEntry_ReadsNamesAndValuesAsAChoice(string entry, bool valid, string? library, string? state)
    {
        LibrarySettings.TryReadEntry(entry.AsSpan(), out var readLibrary, out var readState).ShouldBe(valid);

        if (valid)
        {
            readLibrary.ToString().ShouldBe(library);
            readState.ToString().ShouldBe(state);
        }
    }

    private static LibrarySetting Entry(LibraryState state, SettingPlace place) =>
        new(SqlLibrary.NodaTime, state, place);
}
