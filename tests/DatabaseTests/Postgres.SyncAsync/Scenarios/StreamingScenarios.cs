using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Streaming.sql, whose queries are all streams: a stream has no synchronous method, under method-type=both too,
// so the file has no pair to compare.
internal static class StreamingScenarios
{
    public static IEnumerable<Scenario> All => [];
}
