using System;
using Shouldly;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodsInputTests
{
    // The order is part of the pipeline value, so a project whose libraries do not change plans no method again.
    [Fact]
    public void BelowTheFloorOf_EveryLibraryBelow_IsInTheOrderOfLibraryThenEngine() =>
        MethodsInput
            .BelowTheFloorOf(
                new LibraryProbe(
                    true,
                    true,
                    new Version(3, 0, 0),
                    NetTopologySuite: true,
                    NetTopologySuiteAtFloor: false,
                    SqlServerBytes: true,
                    SqlServerBytesVersion: "2.0.0"
                )
            )
            .ShouldBe([
                new LibraryUse(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql),
                new LibraryUse(SqlLibrary.NodaTime, DatabaseEngine.SqlServer),
                new LibraryUse(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql),
                new LibraryUse(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer),
            ]);
}
