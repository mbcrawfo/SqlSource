using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// FixtureCaseFiles writes a case where FixtureCases looks for it, with the files a recording writes.
public sealed class FixtureCaseFilesTests
{
    [Fact]
    public async Task WriteRecordingAsync_Case_IsReadBackAsItsThreeFiles()
    {
        using var folder = new TempFolder();
        var token = TestContext.Current.CancellationToken;
        var fixture = FixtureCaseFiles.Create(folder.Path, SidecarValues.Engine.Postgres, "typed", "SELECT 1;");

        await FixtureCaseFiles.WriteRecordingAsync(
            fixture,
            new DescribeCapture(SidecarValues.Engine.Postgres),
            "{}",
            token
        );

        var read = FixtureCases.Of(SidecarValues.Engine.Postgres, folder.Path).ShouldHaveSingleItem();
        read.Name.ShouldBe(fixture.Name);
        read.Query.ShouldBe("SELECT 1;");
        read.Expected.ShouldBe("{}");
        CaptureJson.Read(read.Capture).Value.ShouldNotBeNull().Calls.ShouldBeEmpty();
    }
}
