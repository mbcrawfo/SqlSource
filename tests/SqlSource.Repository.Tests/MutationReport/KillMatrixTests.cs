using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The matrix of several scopes' reports as one, and which mutants were tested and which could not be.
public sealed class KillMatrixTests
{
    private static MatrixMutant At(int line, MutantStatus status, int[] coveredBy, int[] killedBy) =>
        new(
            "src/SqlSource",
            new MutantKey("A.cs", line, 1, line, 5, "m", "r", 0),
            status,
            false,
            "",
            coveredBy,
            killedBy
        );

    private static ReportTest Test(string id) => new(id, "tests/ATests.cs", "N.A." + id);

    [Fact]
    public void Merge_TwoReports_UnitesTestsByIdAndRenumbers()
    {
        var first = new KillMatrix([Test("a"), Test("b")], [At(1, MutantStatus.Killed, [0, 1], [1])]);
        var second = new KillMatrix([Test("b"), Test("c")], [At(2, MutantStatus.Killed, [0, 1], [0])]);

        var merged = KillMatrix.Merge([first, second]);

        merged.Tests.Select(test => test.Id).ShouldBe(["a", "b", "c"]);
        merged.Mutants.Select(mutant => mutant.Key.StartLine).ShouldBe([1, 2]);
        merged.Mutants[1].CoveredBy.ShouldBe([1, 2]);
        merged.Mutants[1].KilledBy.ShouldBe([1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Merge_MutantIgnoredInOneAndTestedInAnother_KeepsTheTested(bool testedFirst)
    {
        var tested = new KillMatrix([Test("a")], [At(1, MutantStatus.Survived, [0], [])]);
        var ignored = new KillMatrix([Test("b")], [At(1, MutantStatus.Ignored, [], [])]);

        var merged = KillMatrix.Merge(testedFirst ? [tested, ignored] : [ignored, tested]);

        var mutant = merged.Mutants.ShouldHaveSingleItem();
        mutant.Status.ShouldBe(MutantStatus.Survived);
        merged.Tests[mutant.CoveredBy.ShouldHaveSingleItem()].Id.ShouldBe("a");
    }

    [Fact]
    public void Merge_MutantOfAnotherProjectWithTheSameKey_IsAnotherMutant()
    {
        var generator = new KillMatrix([Test("a")], [At(1, MutantStatus.Killed, [0], [0])]);
        var tool = new KillMatrix(
            [Test("b")],
            [At(1, MutantStatus.Killed, [0], [0]) with { Project = "src/SqlSource.Tool" }]
        );

        KillMatrix.Merge([generator, tool]).Mutants.Count.ShouldBe(2);
    }

    // Review focus 1 of docs/superpowers/plans/2026-11-12-reviewing-test-quality-skill.md: two scopes that test one
    // mutant would have its kills counted twice.
    [Fact]
    public void Merge_MutantTestedInTwoReports_ThrowsNamingItsFile()
    {
        var first = new KillMatrix([Test("a")], [At(1, MutantStatus.Killed, [0], [0])]);
        var second = new KillMatrix([Test("b")], [At(1, MutantStatus.Survived, [0], [])]);

        Should
            .Throw<InvalidDataException>(() => KillMatrix.Merge([first, second]))
            .Message.ShouldContain("src/SqlSource/A.cs");
    }

    [Theory]
    [InlineData(nameof(MutantStatus.Killed), true)]
    [InlineData(nameof(MutantStatus.Survived), true)]
    [InlineData(nameof(MutantStatus.Timeout), true)]
    [InlineData(nameof(MutantStatus.NoCoverage), true)]
    [InlineData(nameof(MutantStatus.Ignored), false)]
    [InlineData(nameof(MutantStatus.CompileError), false)]
    [InlineData(nameof(MutantStatus.RuntimeError), false)]
    [InlineData(nameof(MutantStatus.Pending), false)]
    public void IsTested_OfEachStatus_IsTrueForTheFourThatARunDecides(string status, bool tested) =>
        MutantKinds.IsTested(At(1, Enum.Parse<MutantStatus>(status), [], [])).ShouldBe(tested);

    [Theory]
    [InlineData(nameof(MutantStatus.CompileError), false, true)]
    [InlineData(nameof(MutantStatus.CompileError), true, true)]
    [InlineData(nameof(MutantStatus.Ignored), true, true)]
    [InlineData(nameof(MutantStatus.Ignored), false, false)]
    [InlineData(nameof(MutantStatus.Killed), true, false)]
    [InlineData(nameof(MutantStatus.Survived), false, false)]
    public void IsBlind_OfEachKind_IsTrueOnlyForUncompiledAndUntestedStatic(string status, bool isStatic, bool blind) =>
        MutantKinds.IsBlind(At(1, Enum.Parse<MutantStatus>(status), [], []) with { Static = isStatic }).ShouldBe(blind);
}
