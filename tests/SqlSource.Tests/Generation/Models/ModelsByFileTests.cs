using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Models;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Models;

public sealed class ModelsByFileTests
{
    [Fact]
    public void Arrange_ModelsOfTwoFiles_GivesEachFileItsSlotsInPathOrder()
    {
        var onB = Model("App.OnB");
        var onA = Model("App.OnA");
        var set = Set((onB, Place("b/B.sql", 0, ModelRole.Output)), (onA, Place("a/A.sql", 1, ModelRole.Input)));

        var byFile = ModelsByFile.Arrange(set, TestModels.Array<string>());

        byFile.Files.Select(file => file.NormalizedPath).ShouldBe(["a/A.sql", "b/B.sql"]);
        var slotsOfA = byFile.Files[0].Slots;
        slotsOfA.Count.ShouldBe(4);
        slotsOfA[2].ShouldBe([onA]);
        slotsOfA[0].ShouldBeEmpty();
        slotsOfA[1].ShouldBeEmpty();
        slotsOfA[3].ShouldBeEmpty();
        var slotsOfB = byFile.Files[1].Slots;
        slotsOfB.Count.ShouldBe(2);
        slotsOfB[0].ShouldBeEmpty();
        slotsOfB[1].ShouldBe([onB]);
        byFile.OfRepeatedPaths.ShouldBeEmpty();
    }

    [Fact]
    public void Arrange_TwoTypesClaimOneQuery_PutsBothModelsInItsSlotByClaimingType()
    {
        var byZ = Model("App.ByZ");
        var byA = Model("App.ByA");
        var set = Set(
            (byZ, Place("a/A.sql", 0, ModelRole.Output, "App.Z")),
            (byA, Place("a/A.sql", 0, ModelRole.Output, "App.A"))
        );

        var byFile = ModelsByFile.Arrange(set, TestModels.Array<string>());

        byFile.Files.Count.ShouldBe(1);
        byFile.Files[0].Slots[1].ShouldBe([byA, byZ]);
    }

    [Fact]
    public void Arrange_ModelsOfOneQueryAndRole_KeepTheSetsOrderWhenTheirPlacesAreEqual()
    {
        var first = Model("App.First");
        var second = Model("App.Second");
        var place = Place("a/A.sql", 0, ModelRole.Input);

        var byFile = ModelsByFile.Arrange(Set((first, place), (second, place)), TestModels.Array<string>());

        byFile.Files[0].Slots[0].ShouldBe([first, second]);
    }

    [Fact]
    public void Arrange_PathsThatDifferByCase_AreOneFile()
    {
        var lower = Model("App.Lower");
        var upper = Model("App.Upper");
        var set = Set((upper, Place("a/A.sql", 1, ModelRole.Input)), (lower, Place("a/a.sql", 0, ModelRole.Input)));

        var byFile = ModelsByFile.Arrange(set, TestModels.Array<string>());

        byFile.Files.Count.ShouldBe(1);
        byFile.Files[0].Slots[0].ShouldBe([lower]);
        byFile.Files[0].Slots[2].ShouldBe([upper]);
    }

    [Fact]
    public void Arrange_RepeatedPath_LeavesItsModelsToTheCollectedOutput()
    {
        var onA = Model("App.OnA");
        var onB = Model("App.OnB");
        var set = Set((onA, Place("a/A.sql", 0, ModelRole.Output)), (onB, Place("b/B.sql", 0, ModelRole.Output)));

        var byFile = ModelsByFile.Arrange(set, TestModels.Array("A/a.sql"));

        byFile.Files.Select(file => file.NormalizedPath).ShouldBe(["b/B.sql"]);
        byFile.OfRepeatedPaths.ShouldBe([onA]);
    }

    [Fact]
    public void Arrange_NoModels_IsEmpty()
    {
        var byFile = ModelsByFile.Arrange(Set(), TestModels.Array("a/A.sql"));

        byFile.Files.ShouldBeEmpty();
        byFile.OfRepeatedPaths.ShouldBeEmpty();
    }

    [Fact]
    public void SlotsOf_PathWithoutModels_IsEmpty()
    {
        var byFile = ModelsByFile.Arrange(Set((Model("App.OnA"), Place("a/A.sql", 0, ModelRole.Input))), default);

        ModelsByFile.SlotsOf("b/B.sql", byFile).ShouldBeEmpty();
    }

    [Fact]
    public void SlotsOf_PathIgnoringCase_FindsItsSlots()
    {
        var onA = Model("App.OnA");
        var byFile = ModelsByFile.Arrange(Set((onA, Place("a/A.sql", 0, ModelRole.Input))), default);

        ModelsByFile.SlotsOf("A/a.SQL", byFile).ShouldBe(byFile.Files[0].Slots);
        ModelsByFile.SlotsOf("A/a.SQL", byFile)[0].ShouldBe([onA]);
    }

    [Fact]
    public void Arrange_EqualSets_AreEqual()
    {
        static ModelsByFile Arrange() =>
            ModelsByFile.Arrange(
                Set(
                    (Model("App.OnB"), Place("b/B.sql", 0, ModelRole.Output)),
                    (Model("App.OnA"), Place("a/A.sql", 1, ModelRole.Input)),
                    (Model("App.OnRepeated"), Place("c/C.sql", 0, ModelRole.Input))
                ),
                TestModels.Array("c/C.sql")
            );

        var (first, second) = (Arrange(), Arrange());

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    private static ModelSet Set(params (Model Model, ModelPlace Place)[] models) =>
        new(
            TestModels.Array([.. models.Select(model => model.Model)]),
            TestModels.Array([.. models.Select(model => model.Place)]),
            TestModels.Array<DiagnosticInfo>()
        );

    private static ModelPlace Place(string path, int queryIndex, ModelRole role, string claimingType = "App.Repo") =>
        new(path, queryIndex, role, claimingType);

    private static Model Model(string fullName) =>
        new(
            fullName + ".g.cs",
            "App",
            fullName[(fullName.LastIndexOf('.') + 1)..],
            ModelRole.Output,
            ModelKind.Record,
            true,
            TestModels.Array<ModelProperty>(),
            string.Empty,
            false
        );
}
