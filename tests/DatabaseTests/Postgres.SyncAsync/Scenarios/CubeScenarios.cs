using System.Collections.Generic;
using NpgsqlTypes;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Cube.sql: a cube sent and read back.
internal static class CubeScenarios
{
    public static IEnumerable<Scenario> All =>
        [
            .. OpenAndClosed(
                typeof(CubeQueriesExtensions),
                "EchoCube",
                new EchoCubeArgs(new NpgsqlCube([1.5, -2.25, 3], [4, 5.5, -6]))
            ),
            Of(
                typeof(CubeQueriesExtensions),
                "EchoCube",
                ScenarioTag.Ordinary,
                "a point, in a transaction",
                InTransaction,
                new EchoCubeArgs(new NpgsqlCube(7))
            ),
        ];
}
