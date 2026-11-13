using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The reader of a Stryker report, tested on small reports that Reports writes.
public sealed class ReportReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadAsync_ReportOfOneFile_GivesEachMutantItsKeyAndStatus()
    {
        var mutants = await ReportReader.ReadAsync(
            Reports.Json("/repo/src/SqlSource", ("/repo/src/SqlSource/A.cs", 7, "Boolean mutation", "true", "Killed")),
            Token
        );

        mutants.ShouldBe([
            new Mutant(new MutantKey("A.cs", 7, 1, 7, 5, "Boolean mutation", "true", 0), MutantStatus.Killed),
        ]);
    }

    [Theory]
    [InlineData("/one/src/SqlSource", "/one/src/SqlSource/Parsing/A.cs")]
    [InlineData("/two/elsewhere", "/two/elsewhere/Parsing/A.cs")]
    [InlineData(@"C:\repo\src\SqlSource", @"C:\repo\src\SqlSource\Parsing\A.cs")]
    public async Task ReadAsync_FileUnderTheProjectRoot_IsKeyedByItsPathFromTheRoot(string root, string file) =>
        (await ReportReader.ReadAsync(Reports.Json(root, (file, 1, "m", "r", "Killed")), Token))
            .Single()
            .Key.File.ShouldBe("Parsing/A.cs");

    [Fact]
    public async Task ReadAsync_MutantsWithOneKey_AreNumberedInReportOrder() =>
        (
            await ReportReader.ReadAsync(
                Reports.Json("/r", ("/r/A.cs", 1, "m", "r", "Killed"), ("/r/A.cs", 1, "m", "r", "Survived")),
                Token
            )
        )
            .Select(mutant => (mutant.Key.Ordinal, mutant.Status))
            .ShouldBe([(0, MutantStatus.Killed), (1, MutantStatus.Survived)]);

    [Fact]
    public async Task ReadAsync_StatusItDoesNotKnow_ThrowsNamingIt() =>
        (
            await Should.ThrowAsync<InvalidDataException>(async () =>
                await ReportReader.ReadAsync(Reports.Json("/r", ("/r/A.cs", 1, "m", "r", "Zombie")), Token)
            )
        ).Message.ShouldContain("Zombie");

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task ReadAsync_TextThatIsNoReport_Throws(string text) =>
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await ReportReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)), Token)
        );
}
