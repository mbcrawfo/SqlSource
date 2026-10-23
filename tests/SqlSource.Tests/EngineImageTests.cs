using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Shouldly;
using SqlSource.Testing;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tests;

// The gates on a server's version that the containers of both engines share.  They are tested here, with no server:
// each takes the version it is asked about, so a test names it.
public sealed class EngineImageTests
{
    [Theory]
    [InlineData(18, 18)]
    [InlineData(17, 17)]
    public void SkipBelow_ServerOfThatMajor_DoesNotSkip(int server, int major) =>
        Should.NotThrow(() => EngineImage.SkipBelow(server, major, "a reason"));

    [Theory]
    [InlineData(17, 18)]
    [InlineData(14, 17)]
    public void SkipBelow_ServerBeforeThatMajor_SkipsWithTheReason(int server, int major) =>
        Should
            .Throw<SkipException>(() => EngineImage.SkipBelow(server, major, "a reason"))
            .Message.ShouldEndWith("a reason");

    [Theory]
    [InlineData(nameof(EngineImage.Postgres), 14)]
    [InlineData(nameof(EngineImage.Postgres), 18)]
    [InlineData(nameof(EngineImage.SqlServer), 14)]
    [InlineData(nameof(EngineImage.SqlServer), 17)]
    public void SkipUnlessRecordedMajor_RecordedMajor_DoesNotSkip(string engine, int server) =>
        Should.NotThrow(() => EngineImageNamed(engine).SkipUnlessRecordedMajor(server, "The things"));

    [Theory]
    [InlineData(nameof(EngineImage.Postgres), "The things are pinned for PostgreSQL 14 and 18 alone.")]
    [InlineData(nameof(EngineImage.SqlServer), "The things are pinned for SQL Server 14 and 17 alone.")]
    public void SkipUnlessRecordedMajor_OtherMajor_SkipsNamingTheRecordedOnes(string engine, string message) =>
        Should
            .Throw<SkipException>(() => EngineImageNamed(engine).SkipUnlessRecordedMajor(16, "The things"))
            .Message.ShouldEndWith(message);

    [Theory]
    [InlineData("postgres:18.6", "18.6")]
    [InlineData("mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04", "2025-CU9-GDR1-ubuntu-24.04")]
    public void TagOf_PinnedImage_IsWhatFollowsTheLastColon(string image, string tag) =>
        EngineImage.TagOf(image).ShouldBe(tag);

    [Fact]
    public void Default_OfEachEngine_IsItsDefaultImage()
    {
        EngineImage.Postgres.Default.ShouldBe(DefaultImages.Postgres);
        EngineImage.SqlServer.Default.ShouldBe(DefaultImages.SqlServer);
    }

    [Fact]
    public async Task StartOnceAsync_StartThatThrows_DisposesTheContainerAndRethrows()
    {
        await using var container = new FakeContainer(new InvalidOperationException("It did not start."));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            EngineImage.StartOnceAsync(() => container, TestContext.Current.CancellationToken)
        );

        exception.Message.ShouldBe("It did not start.");
        container.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task StartOnceAsync_StartThatSucceeds_ReturnsTheContainerUndisposed()
    {
        await using var container = new FakeContainer(null);

        var started = await EngineImage.StartOnceAsync(() => container, TestContext.Current.CancellationToken);

        started.ShouldBeSameAs(container);
        container.Started.ShouldBeTrue();
        container.Disposals.ShouldBe(0);
    }

    private static EngineImage EngineImageNamed(string engine) =>
        engine switch
        {
            nameof(EngineImage.Postgres) => EngineImage.Postgres,
            nameof(EngineImage.SqlServer) => EngineImage.SqlServer,
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "No such engine."),
        };

    // A container whose start throws the exception it is given, or succeeds when given none.
    private sealed class FakeContainer(Exception? failure) : IStartingContainer
    {
        public bool Started { get; private set; }

        public int Disposals { get; private set; }

        public string Image => "fake:1";

        public TestcontainersStates State => TestcontainersStates.Undefined;

        public Task StartAsync(CancellationToken token)
        {
            Started = true;
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }

        public Task<(string Stdout, string Stderr)> GetLogsAsync() => Task.FromResult((string.Empty, string.Empty));

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
