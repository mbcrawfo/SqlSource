using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Constants.sql, which is only SQL: its query has a constant and no method, so it has no pair to compare.
internal static class ConstantsScenarios
{
    public static IEnumerable<Scenario> All => [];
}
