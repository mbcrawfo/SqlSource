using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// What a kill matrix says of each test method: what it kills, what it alone kills and which other tests kill the
// same.  Each test builds its matrix of tests named for their method, A to D, and of mutants at a line each.
public sealed class KillEvidenceTests
{
    private const string File = "tests/ATests.cs";

    // The tests of a matrix, by id: "A" is the one row of the method N.T.A, and "A#2" a second row of it.
    private readonly List<ReportTest> _tests = [];
    private readonly List<MatrixMutant> _mutants = [];

    [Fact]
    public void Of_TheoryRows_AreOneMethodWithTheirKillsUnited()
    {
        Killed(1, "A#1");
        Killed(2, "A#2");
        Killed(3, "A#1", "A#2");

        var method = Evidence().ShouldHaveSingleItem();

        method.Name.ShouldBe("N.T.A");
        method.File.ShouldBe(File);
        method.Rows.ShouldBe(2);
        method.Kills.ShouldBe(3);
        method.Covered.ShouldBe(3);
    }

    [Fact]
    public void Of_KillThatAnotherFocusedMethodMakes_IsNotAlone()
    {
        Killed(1, "A", "B");

        Of("A").Alone.ShouldBeEmpty();
    }

    [Fact]
    public void Of_KillThatNoOtherMethodMakes_IsAlone()
    {
        Killed(1, "A", "B");
        Killed(2, "A");

        Of("A").Alone.Select(mutant => mutant.Key.StartLine).ShouldBe([2]);
    }

    // Review focus 3 of docs/superpowers/plans/2026-11-12-reviewing-test-quality-skill.md: a broad test is no cover.
    [Fact]
    public void Of_KillThatOnlyABroadTestShares_IsAlone()
    {
        Killed(1, "A", "B");

        var method = Of("A", broad: "N.T.B");

        method.Alone.Select(mutant => mutant.Key.StartLine).ShouldBe([1]);
        method.Cover.ShouldBeNull();
    }

    [Fact]
    public void Of_BroadTest_IsMarkedAndIsInNoCover()
    {
        Killed(1, "A", "B", "C");
        Killed(2, "A", "B");
        Killed(2, "A", "C");

        Of("B", broad: "N.T.B").Broad.ShouldBeTrue();
        var method = Of("A", broad: "N.T.B");
        method.Broad.ShouldBeFalse();
        method.Cover.ShouldBeNull();
        method.Alone.Count.ShouldBe(1);
    }

    [Fact]
    public void Of_MethodWhoseKillsOthersMake_NamesTheSmallestCoverFound()
    {
        Killed(1, "A", "B", "D");
        Killed(2, "A", "B");
        Killed(3, "A", "C");

        Of("A").Cover.ShouldBe(["N.T.B", "N.T.C"]);
    }

    [Fact]
    public void Of_CoversThatKillAsMuch_TakeTheFirstByName()
    {
        Killed(1, "A", "C", "B");

        Of("A").Cover.ShouldBe(["N.T.B"]);
    }

    [Fact]
    public void Of_MethodWithAKillOfItsOwn_HasNoCover()
    {
        Killed(1, "A", "B");
        Killed(2, "A");

        Of("A").Cover.ShouldBeNull();
    }

    [Fact]
    public void Of_MethodThatKillsNothing_HasNoCoverAndCountsWhatItCovers()
    {
        Killed(1, covered: ["A", "B"], killed: ["B"]);
        Add(2, MutantStatus.Survived, ["A"], []);
        Add(3, MutantStatus.NoCoverage, [], []);

        var method = Of("A");

        method.Kills.ShouldBe(0);
        method.Covered.ShouldBe(2);
        method.Cover.ShouldBeNull();
        method.Alone.ShouldBeEmpty();
        method.RowsNeeded.ShouldBe(0);
    }

    [Fact]
    public void Of_TheoryWhoseRowsRepeatKills_CountsTheRowsNeeded()
    {
        Killed(1, "A#1", "A#2");
        Killed(2, "A#1", "A#3");
        Killed(3, "A#4", "A#3");

        var method = Of("A");

        method.Rows.ShouldBe(4);
        method.RowsNeeded.ShouldBe(2);
    }

    [Fact]
    public void Of_MethodOfOneRowThatKills_NeedsItsRow()
    {
        Killed(1, "A");

        Of("A").RowsNeeded.ShouldBe(1);
    }

    [Fact]
    public void Of_TimeoutUnderAMethod_IsCountedAndIsNoKill()
    {
        Add(1, MutantStatus.Timeout, ["A", "B"], []);

        var method = Of("A");

        method.Timeouts.ShouldBe(1);
        method.Kills.ShouldBe(0);
        method.Covered.ShouldBe(1);
    }

    [Fact]
    public void Of_SurvivorUnderAMethod_IsListedByFileAndPlace()
    {
        Add(9, MutantStatus.Survived, ["A"], []);
        Add(2, MutantStatus.Survived, ["A", "B"], []);
        Add(5, MutantStatus.Survived, ["B"], []);

        Of("A").Survivors.Select(mutant => mutant.Key.StartLine).ShouldBe([2, 9]);
    }

    [Fact]
    public void Of_MutantThatWasNotTested_CountsForNothing()
    {
        Add(1, MutantStatus.Ignored, ["A"], []);
        Add(2, MutantStatus.CompileError, ["A"], []);
        Killed(3, "A");

        var method = Of("A");

        method.Covered.ShouldBe(1);
        method.Kills.ShouldBe(1);
    }

    [Fact]
    public void Of_Methods_AreOrderedByFileThenName()
    {
        _tests.Add(new ReportTest("z", "tests/B.cs", "N.T.A"));
        _tests.Add(new ReportTest("y", "tests/A.cs", "N.T.C"));
        _tests.Add(new ReportTest("x", "tests/A.cs", "N.T.B"));

        Evidence()
            .Select(method => (method.File, method.Name))
            .ShouldBe([("tests/A.cs", "N.T.B"), ("tests/A.cs", "N.T.C"), ("tests/B.cs", "N.T.A")]);
    }

    private void Killed(int line, params string[] killers) => Add(line, MutantStatus.Killed, killers, killers);

    private void Killed(int line, string[] covered, string[] killed) => Add(line, MutantStatus.Killed, covered, killed);

    private void Add(int line, MutantStatus status, string[] covered, string[] killed) =>
        _mutants.Add(
            new MatrixMutant(
                "src/SqlSource",
                new MutantKey(
                    "A.cs",
                    line,
                    1,
                    line,
                    5,
                    "m",
                    "r",
                    _mutants.Count(mutant => mutant.Key.StartLine == line)
                ),
                status,
                false,
                "",
                [.. covered.Select(Index)],
                [.. killed.Select(Index)]
            )
        );

    // The number of a test, which is added when a mutant first names it.
    private int Index(string id)
    {
        var index = _tests.FindIndex(test => test.Id == id);
        if (index < 0)
        {
            index = _tests.Count;
            _tests.Add(new ReportTest(id, File, "N.T." + id.Split('#')[0]));
        }

        return index;
    }

    private IReadOnlyList<MethodEvidence> Evidence(string? broad = null) =>
        KillEvidence.Of(new KillMatrix(_tests, _mutants), name => name == broad);

    private MethodEvidence Of(string method, string? broad = null) =>
        Evidence(broad).Single(evidence => evidence.Name == "N.T." + method);
}
