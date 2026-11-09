using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

// What differs between the entry a bundle expects and the one its replay gave, member by member, as the sidecar writes
// them.
public sealed class EntryDifferencesTests
{
    private static SidecarEntry Expected =>
        BundleReplay
            .ExpectedOf(BundleReplays.OfCase("postgres", "select-left-join"), BundleInfos.Path)
            .Value.ShouldBeOfType<ReplayOutcome.Described>()
            .Entry;

    private static SidecarEntry WithColumn(SidecarEntry entry, int index, SidecarColumn column)
    {
        var columns = entry.Columns.ShouldNotBeNull().ToArray();
        columns[index] = column;
        return entry with { Columns = new EquatableArray<SidecarColumn>([.. columns]) };
    }

    [Fact]
    public void Of_SameEntry_IsEmpty() => EntryDifferences.Of(Expected, Expected).ShouldBeEmpty();

    // A bundle is replayed by another version of the tool than the one that made it, and each writes its own.
    [Fact]
    public void Of_EntriesOfTwoToolVersions_IsEmpty()
    {
        var entry = TestSidecars.Entry("GetUser", toolVersion: "1.4.0");

        EntryDifferences.Of(entry, entry with { ToolVersion = "0.0.1" }).ShouldBeEmpty();
    }

    [Fact]
    public void Of_ColumnNullabilityChanged_NamesItsPath()
    {
        var expected = Expected;
        var column = expected.Columns.ShouldNotBeNull()[2];
        var replayed = WithColumn(expected, 2, column with { Nullable = false, NullableSource = "catalog" });

        EntryDifferences
            .Of(expected, replayed)
            .ShouldBe([
                "columns[2].nullable: true -> false",
                "columns[2].nullableSource: \"outer-join\" -> \"catalog\"",
            ]);
    }

    [Fact]
    public void Of_MemberNullOnOneSide_IsNull()
    {
        var expected = Expected;
        var column = expected.Columns.ShouldNotBeNull()[0];
        var replayed = WithColumn(expected, 0, column with { Origin = null });

        EntryDifferences
            .Of(expected, replayed)
            .ShouldBe(["columns[0].origin: {\"schema\":\"public\",\"table\":\"users\",\"column\":\"id\"} -> null"]);
    }

    [Fact]
    public void Of_MemberOnlyOneSideWrites_IsNone()
    {
        var named = BundleReplay
            .ExpectedOf(BundleReplays.OfCase("postgres", "insert-returning"), BundleInfos.Path)
            .Value.ShouldBeOfType<ReplayOutcome.Described>()
            .Entry;
        var parameters = named.Parameters.ToArray();
        var type = parameters[0].Type.ShouldNotBeNull();
        type.BindName.ShouldBe("integer");
        parameters[0] = parameters[0] with { Type = type with { BindName = null } };
        var unnamed = named with { Parameters = new EquatableArray<SidecarParameter>([.. parameters]) };

        EntryDifferences.Of(unnamed, named).ShouldBe(["parameters[0].type.bindName: (none) -> \"integer\""]);
        EntryDifferences.Of(named, unnamed).ShouldBe(["parameters[0].type.bindName: \"integer\" -> (none)"]);
    }

    [Fact]
    public void Of_ColumnRemoved_NamesItAsNone()
    {
        var expected = Expected;
        var replayed = expected with
        {
            Columns = new EquatableArray<SidecarColumn>([.. expected.Columns.ShouldNotBeNull().ToArray()[..3]]),
        };

        var differences = EntryDifferences.Of(expected, replayed);

        differences.Count.ShouldBe(1);
        differences[0].ShouldStartWith("columns[3]: {\"ordinal\":3,\"name\":\"n\",");
        differences[0].ShouldEndWith("} -> (none)");
    }

    [Fact]
    public void Of_OtherName_IsTheKey() =>
        EntryDifferences
            .Of(Expected, Expected with { Name = "Other" })
            .ShouldBe(["(key): \"GetUserOrders\" -> \"Other\""]);
}
