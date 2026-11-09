using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;

namespace SqlSource.Tool.Tests;

// The bundle path and the bundle.json that the tests of bundles use when they care for neither.
internal static class BundleInfos
{
    // Where a bundle's errors would be.  Nothing is read there.
    public const string Path = "/work/report.zip";

    // A bundle.json of the engine, as a tool 1.4.0 on a Linux machine writes it for an entry.
    public static BundleInfo Sample(
        string engine = "postgres",
        bool renamed = false,
        string? driverVersion = null,
        BundleOutcome? outcome = null
    ) =>
        new(
            BundleVersion: 1,
            Renamed: renamed,
            ToolVersion: "1.4.0",
            FormatVersion: SidecarFormat.Version,
            Engine: engine,
            ServerVersion: "18.6",
            DriverVersion: driverVersion,
            Runtime: ".NET 8.0.11",
            OperatingSystem: "Linux 6.8",
            Outcome: outcome ?? BundleOutcome.Entry
        );
}
