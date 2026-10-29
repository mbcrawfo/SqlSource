using System.Linq;
using Shouldly;
using SqlSource.Generation.Models;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation.Models;

public sealed class ModelAssignmentTests
{
    [Fact]
    public void FirstOf_TypeWithSeveralAssignments_GivesTheIndexOfItsFirst()
    {
        var sorted = Sorted("App.A", "App.B", "App.B", "App.B", "App.C");

        ModelAssignment.FirstOf(sorted, "App.B").ShouldBe(1);
    }

    [Theory]
    [InlineData("App.A", 0)]
    [InlineData("App.C", 4)]
    public void FirstOf_TypeAtAnEnd_GivesItsIndex(string claimingType, int expected) =>
        ModelAssignment.FirstOf(Sorted("App.A", "App.B", "App.B", "App.B", "App.C"), claimingType).ShouldBe(expected);

    [Theory]
    [InlineData("App.0")]
    [InlineData("App.AA")]
    [InlineData("App.D")]
    public void FirstOf_TypeWithoutAssignments_GivesMinusOne(string claimingType) =>
        ModelAssignment.FirstOf(Sorted("App.A", "App.B", "App.C"), claimingType).ShouldBe(-1);

    [Fact]
    public void FirstOf_NoAssignments_GivesMinusOne() =>
        ModelAssignment.FirstOf(EquatableArray<ModelAssignment>.Empty, "App.A").ShouldBe(-1);

    private static EquatableArray<ModelAssignment> Sorted(params string[] claimingTypes) =>
        TestSidecars.Of([
            .. claimingTypes.Select(
                (claimingType, index) =>
                    new ModelAssignment(
                        claimingType,
                        "app/Users.sql",
                        index,
                        ModelRole.Output,
                        "global::App.Dto",
                        ModelKind.Record
                    )
            ),
        ]);
}
