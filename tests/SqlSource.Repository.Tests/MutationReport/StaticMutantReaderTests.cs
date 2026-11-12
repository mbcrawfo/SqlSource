using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The reader of a report's static mutants, which a run keeps out: where each is in its file's source, as the
// exclusion Stryker's --mutate takes, and how many of them the run tested.
public sealed class StaticMutantReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadAsync_StaticMutant_IsExcludedByItsPlaceInTheSource()
    {
        var mutants = await ReadAsync(
            new ReportFile("/r/A.cs", "ab\ncdef\n", new ReportMutant(2, 2, 2, 4, "Ignored", Static: true))
        );

        mutants.Exclusions.ShouldBe(["!A.cs{4..6}"]);
    }

    [Fact]
    public async Task ReadAsync_SourceWithWindowsLineEndings_CountsEachReturn() =>
        (
            await ReadAsync(
                new ReportFile("/r/A.cs", "ab\r\ncdef\r\ngh", new ReportMutant(2, 1, 3, 2, "Ignored", Static: true))
            )
        ).Exclusions.ShouldBe(["!A.cs{4..11}"]);

    [Fact]
    public async Task ReadAsync_SpansThatOverlapOrTouch_AreOneSpan() =>
        (
            await ReadAsync(
                new ReportFile(
                    "/r/A.cs",
                    "0123456789012345678901234567890",
                    new ReportMutant(1, 21, 1, 25, "Ignored", Static: true),
                    new ReportMutant(1, 3, 1, 7, "Ignored", Static: true),
                    new ReportMutant(1, 5, 1, 9, "Ignored", Static: true),
                    new ReportMutant(1, 9, 1, 11, "Ignored", Static: true),
                    new ReportMutant(1, 3, 1, 7, "Ignored", Static: true)
                )
            )
        ).Exclusions.ShouldBe(["!A.cs{2..10}{20..24}"]);

    [Fact]
    public async Task ReadAsync_MutantThatIsNotStatic_IsLeftOut()
    {
        var mutants = await ReadAsync(new ReportFile("/r/A.cs", "abcdef", new ReportMutant(1, 1, 1, 3, "Killed")));

        mutants.Exclusions.ShouldBeEmpty();
        mutants.Tested.ShouldBe(0);
    }

    [Fact]
    public async Task ReadAsync_SeveralFiles_GiveAnExclusionEachByPathFromTheRootInOrder() =>
        (
            await ReadAsync(
                new ReportFile("/r/Parsing/B.cs", "abcdef", new ReportMutant(1, 1, 1, 3, "Ignored", Static: true)),
                new ReportFile("/r/None.cs", "abcdef", new ReportMutant(1, 1, 1, 3, "Killed")),
                new ReportFile("/r/A.cs", "abcdef", new ReportMutant(1, 2, 1, 4, "Ignored", Static: true))
            )
        ).Exclusions.ShouldBe(["!A.cs{1..3}", "!Parsing/B.cs{0..2}"]);

    [Theory]
    [InlineData("Ignored", 0)]
    [InlineData("CompileError", 0)]
    [InlineData("Killed", 1)]
    [InlineData("Survived", 1)]
    [InlineData("Timeout", 1)]
    [InlineData("NoCoverage", 1)]
    [InlineData("RuntimeError", 1)]
    [InlineData("Pending", 1)]
    public async Task ReadAsync_StaticMutant_IsCountedAsTestedUnlessTheRunLeftItOut(string status, int tested) =>
        (
            await ReadAsync(new ReportFile("/r/A.cs", "abcdef", new ReportMutant(1, 1, 1, 3, status, Static: true)))
        ).Tested.ShouldBe(tested);

    [Fact]
    public async Task ReadAsync_PlaceOutsideTheSource_Throws() =>
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await ReadAsync(new ReportFile("/r/A.cs", "ab\ncd", new ReportMutant(5, 1, 5, 2, "Ignored", Static: true)))
        );

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task ReadAsync_TextThatIsNoReport_Throws(string text) =>
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await StaticMutantReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)), Token)
        );

    private static async Task<StaticMutants> ReadAsync(params ReportFile[] files) =>
        await StaticMutantReader.ReadAsync(Reports.Json("/r", files), Token);
}
