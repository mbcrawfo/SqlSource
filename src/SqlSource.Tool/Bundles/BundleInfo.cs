namespace SqlSource.Tool.Bundles;

/// <summary>
/// The bundle's <c>bundle.json</c>: what made it, against what, and what describing its query gave.
/// </summary>
/// <param name="BundleVersion">The version of the bundle's form, <see cref="BundleJson.Version" /> when written.</param>
/// <param name="Renamed">Whether the bundle's names were renamed, so that it may be posted.</param>
/// <param name="ToolVersion">The version of <c>sqlsource</c> that made it.</param>
/// <param name="FormatVersion">The version of the sidecar's format of that <c>sqlsource</c>.</param>
/// <param name="Engine">The engine of the database, as the capture names it.</param>
/// <param name="ServerVersion">The version of the database's server.</param>
/// <param name="DriverVersion">The version of the driver that reached it, or null when it is not known.</param>
/// <param name="Runtime">The .NET that ran the tool.</param>
/// <param name="OperatingSystem">The system the tool ran on.</param>
/// <param name="Outcome">What describing the query gave.</param>
internal sealed record BundleInfo(
    int BundleVersion,
    bool Renamed,
    string ToolVersion,
    int FormatVersion,
    string Engine,
    string ServerVersion,
    string? DriverVersion,
    string Runtime,
    string OperatingSystem,
    BundleOutcome Outcome
);
