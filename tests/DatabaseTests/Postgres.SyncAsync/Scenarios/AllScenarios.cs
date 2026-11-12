using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Every scenario of the project, file by file.
internal static class AllScenarios
{
    public static IReadOnlyList<Scenario> List { get; } =
    [
        .. AccountsScenarios.All,
        .. MethodsScenarios.All,
        .. ShapesScenarios.All,
        .. ListsScenarios.All,
        .. NotesScenarios.All,
        .. CollectionsScenarios.All,
        .. LocationsScenarios.All,
        .. CubeScenarios.All,
        .. ConstantsScenarios.All,
        .. RowBuffersScenarios.All,
        .. StreamingScenarios.All,
    ];

    public static Scenario Named(string name) =>
        List.SingleOrDefault(scenario => scenario.Name == name)
        ?? throw new ArgumentException($"No scenario is named '{name}'.", nameof(name));
}
