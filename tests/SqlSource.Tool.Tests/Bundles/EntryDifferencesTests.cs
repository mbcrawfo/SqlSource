using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

// What differs between the entry a bundle expects and the one its replay gave, member by member, as the sidecar writes
// them.
public sealed class EntryDifferencesTests
{
    private const string Path = "/work/report.zip";

    private static SidecarEntry Expected =>
        BundleReplay
            .ExpectedOf(BundleReplays.OfCase("postgres", "select-left-join"), Path)
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
    public void Of_MemberOnlyOneSideHas_IsNone()
    {
        var expected = Expected;
        var column = expected.Columns.ShouldNotBeNull()[0];
        var replayed = WithColumn(expected, 0, column with { Origin = null });

        EntryDifferences
            .Of(expected, replayed)
            .ShouldBe(["columns[0].origin: {\"schema\":\"public\",\"table\":\"users\",\"column\":\"id\"} -> null"]);
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
