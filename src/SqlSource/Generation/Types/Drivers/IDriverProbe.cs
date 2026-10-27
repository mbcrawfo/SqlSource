using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace SqlSource.Generation.Types.Drivers;

/// <summary>
/// One diagnostic that a probe finds in the project's reference to a driver: the descriptor, and the one argument its
/// message takes, which is the probe's <c>VersionText</c>: it names the version the project references, or says that
/// it is unknown.
/// </summary>
/// <param name="Descriptor">The diagnostic, reported with <c>Location.None</c>.</param>
/// <param name="Argument">The text that fills the message's one argument.</param>
internal readonly record struct DriverFinding(DiagnosticDescriptor Descriptor, string Argument);

/// <summary>
/// What a driver's probe gives the step that reports it: the diagnostics of the project's reference to the driver.
/// The step reports whatever the probe finds and decides nothing about the driver, so a probe that learns of another
/// condition of its driver adds a flag and a finding, and <c>ReportDriver</c> is unchanged.
/// </summary>
internal interface IDriverProbe
{
    /// <summary>
    /// The diagnostics of the project's reference to the driver, each once, in the order they are reported: too old,
    /// then newer than known.  Empty when the project does not reference the driver or references one that the map
    /// knows.  Computed from the probe's members, never a member of the probe, so that it takes no part in the probe's
    /// equality.
    /// </summary>
    IEnumerable<DriverFinding> Findings { get; }
}

/// <summary>What the two probes share of reading a compilation.</summary>
internal static class DriverProbes
{
    /// <summary>Whether <paramref name="compilation" /> has a type named <paramref name="metadataName" />.</summary>
    public static bool Has(Compilation compilation, string metadataName) =>
        !compilation.GetTypesByMetadataName(metadataName).IsEmpty;
}
