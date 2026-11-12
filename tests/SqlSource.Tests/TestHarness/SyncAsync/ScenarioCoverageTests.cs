using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.Tests.TestHarness.SyncAsync;

public sealed class ScenarioCoverageTests
{
    private static MethodPair PairOf(string method, MethodKind kind) =>
        new(
            typeof(FakePairs.Queries),
            method,
            kind,
            false,
            typeof(FakePairs.Queries)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == method),
            typeof(FakePairs.Queries).GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == method)
        );

    private static Scenario ScenarioOf(
        string name,
        string method,
        MethodKind kind,
        ScenarioTag tag = ScenarioTag.Ordinary
    ) => new(name, typeof(FakePairs.Queries), method, kind, tag, new Setup());

    private static readonly MethodPair QueryPair = PairOf("Fail", MethodKind.Query);
    private static readonly MethodPair CollectionPair = PairOf("Get", MethodKind.Collection);

    private static List<Scenario> CollectionScenarios(params ScenarioTag[] tags) =>
        [.. tags.Select(tag => ScenarioOf("Host.Get: " + tag, "Get", MethodKind.Collection, tag))];

    [Fact]
    public void GapsOf_PairWithoutScenario_NamesIt() =>
        ScenarioCoverage.GapsOf([QueryPair], []).ShouldHaveSingleItem().ShouldContain("Queries.Fail (Query)");

    [Fact]
    public void GapsOf_ScenarioWithoutPair_NamesIt() =>
        ScenarioCoverage
            .GapsOf([], [ScenarioOf("Host.Other: x", "Other", MethodKind.Query)])
            .ShouldHaveSingleItem()
            .ShouldContain("Host.Other: x");

    [Fact]
    public void GapsOf_CollectionPairWithoutChunkBoundary_NamesIt() =>
        ScenarioCoverage
            .GapsOf([CollectionPair], CollectionScenarios(ScenarioTag.Empty, ScenarioTag.LaterChunkFailure))
            .ShouldHaveSingleItem()
            .ShouldContain("ChunkBoundary");

    [Fact]
    public void GapsOf_DuplicateName_NamesIt() =>
        ScenarioCoverage
            .GapsOf(
                [QueryPair],
                [
                    ScenarioOf("Host.Fail: x", "Fail", MethodKind.Query),
                    ScenarioOf("Host.Fail: x", "Fail", MethodKind.Query),
                ]
            )
            .ShouldHaveSingleItem()
            .ShouldContain("Host.Fail: x");

    [Fact]
    public void GapsOf_Covered_IsEmpty() =>
        ScenarioCoverage
            .GapsOf(
                [QueryPair, CollectionPair],
                [
                    ScenarioOf("Host.Fail: x", "Fail", MethodKind.Query),
                    .. CollectionScenarios(ScenarioTag.Empty, ScenarioTag.ChunkBoundary, ScenarioTag.LaterChunkFailure),
                ]
            )
            .ShouldBeEmpty();
}
