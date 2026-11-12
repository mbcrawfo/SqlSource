using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// The differences between a PostgreSQL method and its twin that are Npgsql's limits, each with its reason, in the order
// they are applied.
internal static class KnownDifferences
{
    // site/postgresql.md, "A synchronous method needs a data source without multiplexing": the synchronous method
    // throws NotSupportedException and runs nothing where its asynchronous twin runs.
    public static KnownDifference NpgsqlMultiplexingRefusesSyncIo { get; } =
        new(
            nameof(NpgsqlMultiplexingRefusesSyncIo),
            "With Multiplexing=true in the data source's connection string, a synchronous method given a connection "
                + "that has no transaction open throws NotSupportedException and runs nothing (site/postgresql.md, \"A "
                + "synchronous method needs a data source without multiplexing\").",
            static (scenario, asyncRun, syncRun) =>
                scenario.Setup.Source == "multiplexing"
                && syncRun.Failure?.Type == typeof(NotSupportedException).FullName
                    ? (asyncRun, asyncRun)
                    : (asyncRun, syncRun)
        );

    public static IReadOnlyList<KnownDifference> All { get; } = [NpgsqlMultiplexingRefusesSyncIo];
}
