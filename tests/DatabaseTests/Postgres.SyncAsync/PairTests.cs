using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// Each scenario's synchronous method beside its asynchronous twin, and the check that every pair has its scenarios.
[Collection(UsesPostgis.Name)]
public sealed class PairTests(PostgresDatabases databases)
{
    private static readonly IReadOnlyList<MethodPair> Pairs = MethodPairs.Find(typeof(PairTests).Assembly);

    public static TheoryData<string> Names => [.. AllScenarios.List.Select(static scenario => scenario.Name)];

    // Passes when the two outcomes are the same, is skipped with the name and reason of each known difference that made
    // them the same, and fails with the fields that still differ.
    [Theory]
    [MemberData(nameof(Names))]
    public async Task RunAsync_Scenario_SyncMatchesAsync(string name)
    {
        var scenario = AllScenarios.Named(name);
        var pair = Pairs.Single(pair =>
            pair.Host == scenario.Host && pair.Method == scenario.Method && pair.Kind == scenario.Kind
        );
        await using var engine = new PostgresEngine(databases);
        var verdict = await PairRunner.RunAsync(engine, pair, scenario, KnownDifferences.All);
        switch (verdict.Agreement)
        {
            case Agreement.Known:
                Assert.Skip(string.Join("\n", verdict.Applied.Select(static known => $"{known.Name}: {known.Reason}")));
                break;
            case Agreement.Different:
                Assert.Fail($"{name}\n{verdict.Report}");
                break;
            case Agreement.Same:
            default:
                break;
        }
    }

    [Fact]
    public void Find_GeneratedPairs_EachHasItsScenarios() =>
        string.Join("\n", ScenarioCoverage.GapsOf(Pairs, AllScenarios.List)).ShouldBeEmpty();
}
