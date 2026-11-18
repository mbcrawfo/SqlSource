using System;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using Xunit;
using static SqlSource.Tests.Settings.TestPlaces;

namespace SqlSource.Tests.Settings.Libraries;

public sealed class LibrarySettingsTests
{
    private static readonly SettingPlace Near = SettingPlace.OfSettingsFile("/app/Queries/sqlsource.json", 2);

    [Fact]
    public void Over_BothNull_IsNull() => LibrarySettings.Over(null, null).ShouldBeNull();

    // Equality alone would pass a copy of the other: measured on the current thread, after a first call of each, the
    // two calls allocate nothing, which a merge into a new array would.
    [Fact]
    public void Over_OneNull_IsTheOtherItself()
    {
        var entries = new EquatableArray<LibrarySetting>([Entry(LibraryState.On, SettingsFile)]);
        _ = LibrarySettings.Over(entries, null);
        _ = LibrarySettings.Over(null, entries);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var nearAlone = LibrarySettings.Over(entries, null);
        var farAlone = LibrarySettings.Over(null, entries);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        nearAlone.ShouldBe(entries);
        farAlone.ShouldBe(entries);
        allocated.ShouldBe(0);
    }

    // Each level names a library the other does not: the merge holds both, in the order of the libraries, and the
    // nearer level's entry for the library both name, whichever of the two is nearer.
    [Fact]
    public void Over_NearAndFarLevels_ReplacesOnlyTheLibrariesTheNearLevelNames()
    {
        var offAndJson = new EquatableArray<LibrarySetting>([
            Entry(SqlLibrary.NodaTime, LibraryState.Off, Near),
            Entry(SqlLibrary.Json, LibraryState.Off, Near),
        ]);
        var onAndNetTopologySuite = new EquatableArray<LibrarySetting>([
            Entry(SqlLibrary.NodaTime, LibraryState.On, SettingsFile),
            Entry(SqlLibrary.NetTopologySuite, LibraryState.On, SettingsFile),
        ]);

        LibrarySettings
            .Over(offAndJson, onAndNetTopologySuite)
            .ShouldNotBeNull()
            .ShouldBe([
                Entry(SqlLibrary.NodaTime, LibraryState.Off, Near),
                Entry(SqlLibrary.NetTopologySuite, LibraryState.On, SettingsFile),
                Entry(SqlLibrary.Json, LibraryState.Off, Near),
            ]);
        LibrarySettings
            .Over(onAndNetTopologySuite, offAndJson)
            .ShouldNotBeNull()
            .ShouldBe([
                Entry(SqlLibrary.NodaTime, LibraryState.On, SettingsFile),
                Entry(SqlLibrary.NetTopologySuite, LibraryState.On, SettingsFile),
                Entry(SqlLibrary.Json, LibraryState.Off, Near),
            ]);
    }

    [Fact]
    public void Over_ANearDefaultEntry_IsKept()
    {
        var near = new EquatableArray<LibrarySetting>([Entry(LibraryState.Default, Near)]);
        var far = new EquatableArray<LibrarySetting>([Entry(LibraryState.On, SettingsFile)]);

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
    public void TryReadEntry_AnEntry_ReadsNamesAndValuesAsAChoice(
        string entry,
        bool valid,
        string? library,
        string? state
    )
    {
        LibrarySettings.TryReadEntry(entry.AsSpan(), out var readLibrary, out var readState).ShouldBe(valid);

        if (valid)
        {
            readLibrary.ToString().ShouldBe(library);
            readState.ToString().ShouldBe(state);
        }
    }

    private static LibrarySetting Entry(LibraryState state, SettingPlace place) =>
        Entry(SqlLibrary.NodaTime, state, place);

    private static LibrarySetting Entry(SqlLibrary library, LibraryState state, SettingPlace place) =>
        new(library, state, place);
}
