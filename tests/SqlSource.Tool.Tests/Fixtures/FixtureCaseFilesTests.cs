using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
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
            new ConnectionSecret("Host=db;Password=hunter22"),
            token
        );

        var read = FixtureCases.Of(SidecarValues.Engine.Postgres, folder.Path).ShouldHaveSingleItem();
        read.Name.ShouldBe(fixture.Name);
        read.Query.ShouldBe("SELECT 1;");
        read.Expected.ShouldBe("{}");
        CaptureJson.Read(read.Capture).Value.ShouldNotBeNull().Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteRecordingAsync_CaptureThatHoldsTheValue_FailsAndWritesNothing()
    {
        using var folder = new TempFolder();
        var token = TestContext.Current.CancellationToken;
        var fixture = FixtureCaseFiles.Create(folder.Path, SidecarValues.Engine.Postgres, "typed", "SELECT 1;");
        var capture = new DescribeCapture(SidecarValues.Engine.Postgres);
        capture.Add(
            new CaptureCall(
                "open",
                CaptureJson.ToElement(new { }),
                CaptureJson.ToElement(new { Error = "bad Host=db;Password=hunter22" })
            )
        );

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            FixtureCaseFiles.WriteRecordingAsync(
                fixture,
                capture,
                "{}",
                new ConnectionSecret("Host=db;Password=hunter22"),
                token
            )
        );

        thrown.Message.ShouldContain("The capture holds the connection's value or a password of it.");
        File.Exists(Path.Combine(fixture.Folder, FixtureCase.CaptureFile)).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Folder, FixtureCase.ExpectedFile)).ShouldBeFalse();
    }
}
