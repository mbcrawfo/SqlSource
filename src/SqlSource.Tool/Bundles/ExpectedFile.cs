using System;
using System.Threading;
using SqlSource.Snapshot;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The <c>expected.json</c> of a bundle: a sidecar of its one entry, with the hash a replay computes for its query.
/// </summary>
internal static class ExpectedFile
{
    /// <summary>
    /// The sidecar of the entry alone, with the hash <see cref="BundleReplay.Plan" /> gives the query, or the failure
    /// of that plan.  The sidecar holds no position of the query's name, so the hash is all that the query changes.
    /// </summary>
    /// <param name="query">The bundle's <c>query.sql</c>.</param>
    /// <param name="engine">The name of the bundle's engine, as the plan reads it.</param>
    /// <param name="entry">The entry, whose hash is replaced.</param>
    /// <param name="formatVersion">The sidecar's format version.</param>
    /// <param name="toolVersion">The sidecar's tool version.</param>
    /// <param name="token">Ends the plan of the query.</param>
    /// <exception cref="InvalidOperationException">The planned query has no hash.</exception>
    public static Outcome<string, ToolDiagnostic> Of(
        string query,
        string engine,
        SidecarEntry entry,
        int formatVersion,
        string toolVersion,
        CancellationToken token
    )
    {
        var bundle = new Bundle(null, query, string.Empty, null, []);
        var planned = BundleReplay.Plan(bundle, engine, BundleReplay.NoPath, token);
        if (!planned.Succeeded)
        {
            return Outcome<string, ToolDiagnostic>.Failed(planned.Failure);
        }

        var hash =
            planned.Value.Query.Hash
            ?? throw new InvalidOperationException($"The query.sql of the query '{entry.Name}' has no hash.");
        return Outcome<string, ToolDiagnostic>.Of(
            SidecarWriter.Write(
                new Sidecar(formatVersion, toolVersion, new EquatableArray<SidecarEntry>([entry with { Hash = hash }]))
            )
        );
    }
}
