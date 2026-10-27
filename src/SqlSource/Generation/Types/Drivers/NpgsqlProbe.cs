using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Types.Drivers;

/// <summary>
/// What a project references of Npgsql: whether it references the driver at all, and which of the versions that the
/// base map tells apart.  Each flag is the presence of a type that first appeared in that version.  The value is
/// equal from one edit to the next, so nothing after the probe runs again.
/// </summary>
/// <param name="Referenced">Whether <c>Npgsql.NpgsqlConnection</c>, which every version has, is found.</param>
/// <param name="AtLeast8">
/// Whether <c>Npgsql.NpgsqlSlimDataSourceBuilder</c>, first in 8.0 on every target framework, is found.
/// </param>
/// <param name="AtLeast10">Whether <c>NpgsqlTypes.NpgsqlCube</c>, first in 10.0, is found.</param>
/// <param name="Version">
/// The version of the referenced assembly named <c>Npgsql</c>, or null when there is none.  For the diagnostics
/// alone: the flags choose the column.
/// </param>
internal sealed record NpgsqlProbe(bool Referenced, bool AtLeast8, bool AtLeast10, Version? Version) : IDriverProbe
{
    private const string AssemblyName = "Npgsql";

    // The newest major whose types the map knows.
    private const int NewestKnownMajor = 10;

    /// <summary>The column of the base map that the driver chooses.</summary>
    public NpgsqlColumn Column =>
        (AtLeast10, Referenced) switch
        {
            (true, _) => NpgsqlColumn.Npgsql10,
            (_, true) => NpgsqlColumn.Npgsql8,
            _ => NpgsqlColumn.None,
        };

    /// <summary>Whether the project references an Npgsql older than 8.0, which is <c>SQLSRC247</c>.</summary>
    public bool IsBelowTheFloor => Referenced && !AtLeast8;

    /// <summary>
    /// Whether the project references an Npgsql of a major newer than the map knows, which is <c>SQLSRC248</c>.
    /// </summary>
    public bool IsNewerThanKnown => Referenced && Version is { Major: > NewestKnownMajor };

    /// <inheritdoc />
    public IEnumerable<DriverFinding> Findings
    {
        get
        {
            if (IsBelowTheFloor)
            {
                yield return new DriverFinding(SqlDiagnostics.NpgsqlTooOld, VersionText);
            }

            if (IsNewerThanKnown)
            {
                yield return new DriverFinding(SqlDiagnostics.NpgsqlNewerThanKnown, VersionText);
            }
        }
    }

    /// <summary>The version as a diagnostic names it: its first three parts, as a package's version has.</summary>
    public string VersionText =>
        Version is null
            ? "of an unknown version"
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.{2}",
                Version.Major,
                Version.Minor,
                Math.Max(Version.Build, 0)
            );

    /// <summary>
    /// Reads the probe from <paramref name="compilation" />: three type lookups and the identities of its references.
    /// Of two references named <c>Npgsql</c>, the newer gives the version.  Never throws.
    /// </summary>
    public static NpgsqlProbe Read(Compilation compilation)
    {
        Version? version = null;
        foreach (var identity in compilation.ReferencedAssemblyNames)
        {
            if (
                string.Equals(identity.Name, AssemblyName, StringComparison.OrdinalIgnoreCase)
                && (version is null || identity.Version > version)
            )
            {
                version = identity.Version;
            }
        }

        return new NpgsqlProbe(
            DriverProbes.Has(compilation, "Npgsql.NpgsqlConnection"),
            DriverProbes.Has(compilation, "Npgsql.NpgsqlSlimDataSourceBuilder"),
            DriverProbes.Has(compilation, "NpgsqlTypes.NpgsqlCube"),
            version
        );
    }
}
