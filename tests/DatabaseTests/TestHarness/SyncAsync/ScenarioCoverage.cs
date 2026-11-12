using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

internal static class ScenarioCoverage
{
    private static readonly ScenarioTag[] CollectionTags =
    [
        ScenarioTag.Empty,
        ScenarioTag.ChunkBoundary,
        ScenarioTag.LaterChunkFailure,
    ];

    // Lines naming each pair with no scenario, each scenario with no pair, each collection pair missing one of
    // Empty, ChunkBoundary, LaterChunkFailure, and each duplicate scenario name.  Empty when covered.
    public static IReadOnlyList<string> GapsOf(IReadOnlyList<MethodPair> pairs, IReadOnlyList<Scenario> scenarios)
    {
        var gaps = new List<string>();
        foreach (var pair in pairs)
        {
            var own = scenarios.Where(scenario => Matches(pair, scenario)).ToList();
            if (own.Count == 0)
            {
                gaps.Add($"No scenario for {NameOf(pair)}.");
                continue;
            }

            if (pair.Kind == MethodKind.Collection)
            {
                gaps.AddRange(
                    CollectionTags
                        .Where(tag => own.TrueForAll(scenario => scenario.Tag != tag))
                        .Select(tag => $"No {tag} scenario for {NameOf(pair)}.")
                );
            }
        }

        gaps.AddRange(
            scenarios
                .Where(scenario => !pairs.Any(pair => Matches(pair, scenario)))
                .Select(scenario => $"No pair for scenario '{scenario.Name}'.")
        );
        gaps.AddRange(
            scenarios
                .GroupBy(static scenario => scenario.Name, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => $"Duplicate scenario name '{group.Key}'.")
        );
        return gaps;
    }

    private static bool Matches(MethodPair pair, Scenario scenario) =>
        pair.Host == scenario.Host && pair.Method == scenario.Method && pair.Kind == scenario.Kind;

    private static string NameOf(MethodPair pair) => $"{pair.Host.Name}.{pair.Method} ({pair.Kind})";
}
