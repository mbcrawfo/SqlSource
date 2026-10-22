using System;
using System.Collections.Generic;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.Tool.Tests;

// The engines and the case that the tests of tests/SqlSource.Tool.Testing/Realistic build on: PostgreSQL's, with its
// recorded majors, 14 and 18, so that a checkedFrom is read as the PostgreSQL corpus reads it, and SQL Server's, for
// what a case of another engine says.
internal static class RealisticTestCases
{
    public static RealisticEngine Postgres { get; } = new(SidecarValues.Engine.Postgres, "PostgreSQL", [14, 18]);

    public static RealisticEngine SqlServer { get; } = new(SidecarValues.Engine.SqlServer, "SQL Server", [14, 17]);

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
