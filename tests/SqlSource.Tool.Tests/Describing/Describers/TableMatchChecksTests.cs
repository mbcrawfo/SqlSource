using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The one order of the table match's checks, the sidecar format's, and the rule that the first check that fails is
// the answer and that no check after it runs.
public sealed class TableMatchChecksTests
{
    private static bool Passes() => true;

    private static bool Fails() => false;

    private static bool MustNotRun() => throw new InvalidOperationException("A check ran after one had failed.");

    [Fact]
    public void Order_OfTheChecks_IsTheFormatsOrder() =>
        TableMatchChecks.Order.ShouldBe([
            SidecarValues.TableMatch.NoOrigin,
            SidecarValues.TableMatch.SeveralTables,
            SidecarValues.TableMatch.NamesDiffer,
            SidecarValues.TableMatch.ColumnsDiffer,
            SidecarValues.TableMatch.OrderDiffers,
            SidecarValues.TableMatch.NullabilityDiffers,
        ]);

    [Fact]
    public void OfTheOrigins_BothFail_IsNoOrigin() =>
        TableMatchChecks.OfTheOrigins(Fails, MustNotRun).ShouldBe(SidecarValues.TableMatch.NoOrigin);

    [Fact]
    public void OfTheOrigins_SecondFails_IsSeveralTables() =>
        TableMatchChecks.OfTheOrigins(Passes, Fails).ShouldBe(SidecarValues.TableMatch.SeveralTables);

    [Fact]
    public void OfTheOrigins_BothPass_IsNull() => TableMatchChecks.OfTheOrigins(Passes, Passes).ShouldBeNull();

    [Fact]
    public void OfTheTable_EveryCheckFails_IsNamesDiffer() =>
        TableMatchChecks
            .OfTheTable(Fails, MustNotRun, MustNotRun, MustNotRun)
            .ShouldBe(SidecarValues.TableMatch.NamesDiffer);

    [Fact]
    public void OfTheTable_EachCheckThatFailsWithTheOnesBeforeItPassing_IsItsOwnValue()
    {
        TableMatchChecks
            .OfTheTable(Passes, Fails, MustNotRun, MustNotRun)
            .ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        TableMatchChecks.OfTheTable(Passes, Passes, Fails, MustNotRun).ShouldBe(SidecarValues.TableMatch.OrderDiffers);
    }

    [Fact]
    public void OfTheTable_LastFails_IsNullabilityDiffers() =>
        TableMatchChecks
            .OfTheTable(Passes, Passes, Passes, Fails)
            .ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);

    [Fact]
    public void OfTheTable_EveryCheckPasses_IsMatched() =>
        TableMatchChecks.OfTheTable(Passes, Passes, Passes, Passes).ShouldBe(SidecarValues.TableMatch.Matched);

    [Fact]
    public void OfTheTable_CheckThatFails_RunsNoCheckAfterIt()
    {
        var ran = 0;

        TableMatchChecks
            .OfTheTable(
                Passes,
                () =>
                {
                    ran++;
                    return false;
                },
                MustNotRun,
                MustNotRun
            )
            .ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        ran.ShouldBe(1);
    }
}
