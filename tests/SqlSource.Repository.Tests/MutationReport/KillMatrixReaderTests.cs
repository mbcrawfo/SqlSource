using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The reader of a report's kill matrix: which tests reach each mutant and which fail on it.
public sealed class KillMatrixReaderTests
{
    private const string Root = "/repo/src/SqlSource";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly ReportTestFile[] TwoTests =
    [
        new("/repo/tests/SqlSource.Tests/ATests.cs", ("t1", "N.A.One"), ("t2", "N.A.Two")),
    ];

    [Fact]
    public async Task ReadAsync_Report_GivesEachMutantItsTestsByIndex()
    {
        var matrix = await ReadAsync(
            TwoTests,
            new ReportMutant(7, 1, 7, 5, "Killed", CoveredBy: ["t1", "t2"], KilledBy: ["t2"])
        );

        matrix.Tests.ShouldBe([
            new ReportTest("t1", "tests/SqlSource.Tests/ATests.cs", "N.A.One"),
            new ReportTest("t2", "tests/SqlSource.Tests/ATests.cs", "N.A.Two"),
        ]);
        var mutant = matrix.Mutants.ShouldHaveSingleItem();
        mutant.Project.ShouldBe("src/SqlSource");
        mutant.Key.ShouldBe(new MutantKey("Parsing/A.cs", 7, 1, 7, 5, "Boolean mutation", "true", 0));
        mutant.Status.ShouldBe(MutantStatus.Killed);
        mutant.Static.ShouldBeFalse();
        mutant.CoveredBy.ShouldBe([0, 1]);
        mutant.KilledBy.ShouldBe([1]);
    }

    // A report lists every file of the test project, and gives tests only to those whose tests the run ran.
    [Fact]
    public async Task ReadAsync_TestFileWithNoTests_AddsNone()
    {
        var matrix = await ReadAsync(
            [.. TwoTests, new ReportTestFile("/repo/tests/SqlSource.Tests/BTests.cs")],
            new ReportMutant(7, 1, 7, 5, "Killed", CoveredBy: ["t1"], KilledBy: ["t1"])
        );

        matrix.Tests.Select(test => test.Id).ShouldBe(["t1", "t2"]);
    }

    [Fact]
    public async Task ReadAsync_TestThatReachesNoMutant_IsStillATest()
    {
        var matrix = await ReadAsync(
            TwoTests,
            new ReportMutant(7, 1, 7, 5, "Survived", CoveredBy: ["t2"], KilledBy: [])
        );

        matrix.Tests.Select(test => test.Id).ShouldBe(["t2", "t1"], ignoreOrder: true);
        matrix.Tests[matrix.Mutants[0].CoveredBy[0]].Id.ShouldBe("t2");
    }

    [Fact]
    public async Task ReadAsync_MutantWithAStatusReason_KeepsItAndWhetherItIsStatic()
    {
        var matrix = await ReadAsync(
            TwoTests,
            new ReportMutant(
                1,
                1,
                1,
                5,
                "Ignored",
                Static: true,
                CoveredBy: [],
                KilledBy: [],
                Reason: "Removed by mutate filter"
            ),
            new ReportMutant(2, 1, 2, 5, "Survived", CoveredBy: ["t1"], KilledBy: [])
        );

        matrix
            .Mutants.Select(mutant => (mutant.Static, mutant.Reason))
            .ShouldBe([(true, "Removed by mutate filter"), (false, "")]);
    }

    [Fact]
    public async Task ReadAsync_IdThatNoTestFileHolds_ThrowsNamingIt() =>
        (
            await Should.ThrowAsync<InvalidDataException>(async () =>
                await ReadAsync(TwoTests, new ReportMutant(7, 1, 7, 5, "Killed", CoveredBy: ["t9"], KilledBy: []))
            )
        ).Message.ShouldContain("t9");

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task ReadAsync_TextThatIsNoReport_Throws(string text) =>
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await KillMatrixReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)), Token)
        );

    private static async Task<KillMatrix> ReadAsync(ReportTestFile[] tests, params ReportMutant[] mutants) =>
        await KillMatrixReader.ReadAsync(
            Reports.Json(Root, [new ReportFile(Root + "/Parsing/A.cs", "class A { }", mutants)], tests),
            Token
        );
}
