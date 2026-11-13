using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.Tests.TestHarness.SyncAsync;

public sealed class MethodPairsTests
{
    private static Scenario ScenarioOf(Type host, string method, object? parameters) =>
        new($"{host.Name}.{method}", host, method, MethodKind.Query, ScenarioTag.Ordinary, new Setup(), parameters);

    private static MethodPair PairOf(string method, MethodKind kind = MethodKind.Query) =>
        MethodPairs
            .Find(typeof(MethodPairsTests).Assembly)
            .Single(pair => pair.Host == typeof(FakePairs.Queries) && pair.Method == method && pair.Kind == kind);

    [Fact]
    public void Find_FakePairs_FindsQueryCollectionAndRepository()
    {
        var pairs = MethodPairs
            .Find(typeof(MethodPairsTests).Assembly)
            .Where(static pair => pair.Host.DeclaringType == typeof(FakePairs))
            .ToList();

        pairs
            .Select(static pair => (pair.Host.Name, pair.Method, pair.Kind, pair.IsInstance))
            .ShouldBe([
                (nameof(FakePairs.Queries), "Fail", MethodKind.Query, false),
                (nameof(FakePairs.Queries), "Get", MethodKind.Query, false),
                (nameof(FakePairs.Queries), "Get", MethodKind.Collection, false),
                (nameof(FakePairs.Repository), "Count", MethodKind.Query, true),
            ]);
        pairs.ShouldAllBe(static pair => pair.Async.Name == pair.Method + "Async");
    }

    [Fact]
    public void Find_MethodWithoutAsyncTwin_IsNotAPair() =>
        MethodPairs.Find(typeof(MethodPairsTests).Assembly).ShouldNotContain(static pair => pair.Method == "Orphan");

    [Fact]
    public async Task InvokeAsync_AsyncMethod_ReturnsTaskResult()
    {
        var pair = PairOf("Get");

        var value = await pair.InvokeAsync(
            pair.Async,
            null,
            default!,
            null,
            ScenarioOf(typeof(FakePairs.Queries), "Get", new FakePairs.Args(41))
        );

        value.ShouldBe(42);
    }

    [Fact]
    public async Task InvokeAsync_SyncMethodOfACollection_ReturnsItsValue()
    {
        var pair = PairOf("Get", MethodKind.Collection);

        var value = await pair.InvokeAsync(
            pair.Sync,
            null,
            default!,
            null,
            ScenarioOf(typeof(FakePairs.Queries), "Get", new[] { new FakePairs.Args(1), new FakePairs.Args(2) })
        );

        value.ShouldBe(2);
    }

    [Fact]
    public async Task InvokeAsync_InstanceMethod_UsesTheInstance()
    {
        var pair = MethodPairs
            .Find(typeof(MethodPairsTests).Assembly)
            .Single(static pair => pair.Host == typeof(FakePairs.Repository));

        var value = await pair.InvokeAsync(
            pair.Async,
            new FakePairs.Repository(7),
            default!,
            null,
            ScenarioOf(typeof(FakePairs.Repository), "Count", new FakePairs.Args(1))
        );

        value.ShouldBe(7);
    }

    [Theory]
    [InlineData(true, "async failure")]
    [InlineData(false, "sync failure")]
    public async Task InvokeAsync_MethodThatThrows_ThrowsItsException(bool useAsync, string message)
    {
        var pair = PairOf("Fail");

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            pair.InvokeAsync(
                useAsync ? pair.Async : pair.Sync,
                null,
                default!,
                null,
                ScenarioOf(typeof(FakePairs.Queries), "Fail", new FakePairs.Args(1))
            )
        );

        exception.Message.ShouldBe(message);
    }
}
