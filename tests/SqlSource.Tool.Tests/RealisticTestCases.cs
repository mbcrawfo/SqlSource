using System;
using System.Collections.Generic;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.Tool.Tests;

// The engines and the case that the tests of tests/SqlSource.Tool.Testing/Realistic build on: PostgreSQL's, with its
// recorded majors, so that a checkedFrom is read as the PostgreSQL corpus reads it, and SQL Server's, for what a case
// of another engine says.
internal static class RealisticTestCases
{
    public static RealisticEngine Postgres { get; } =
        new(SidecarValues.Engine.Postgres, EngineImage.Postgres.Product, EngineImage.Postgres.RecordedMajors);

    public static RealisticEngine SqlServer { get; } =
        new(SidecarValues.Engine.SqlServer, EngineImage.SqlServer.Product, EngineImage.SqlServer.RecordedMajors);

    // A case named keyed, with no columns and one empty sample set, which a test changes by with.
    public static RealisticCase Keyed() =>
        new(
            Postgres,
            "keyed",
            "keyed",
            null,
            null,
            [],
            [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)]
        );
}
