using System;
using System.Collections.Immutable;
using System.Globalization;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// The checks of a sidecar itself, before any of its entries is checked against its query: whether it can be read, its
/// format, and the versions of the tool that wrote it and each of its entries.
/// </summary>
internal static class SidecarFileChecks
{
    /// <summary>
    /// The sidecar of <paramref name="file" /> when it was read and is of this format, and null otherwise, with the
    /// error that says why.  A sidecar of another version of the tool is read, with a warning.
    /// </summary>
    public static Sidecar? Read(SidecarFile file, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var result = file.Result;
        if (result.Sidecar is { } sidecar)
        {
            if (OtherToolVersion(sidecar, file) is { } other)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.SidecarToolVersionDiffers,
                        other.Location,
                        other.Version,
                        PackageVersion.Prefix
                    )
                );
            }

            return sidecar;
        }

        if (result.Error is { } error)
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.SidecarMalformed,
                    file.ErrorLocation ?? file.Start,
                    ReasonOf(error)
                )
            );
            return null;
        }

        var version = result.FormatVersion ?? 0;
        diagnostics.Add(
            DiagnosticInfo.Create(
                version > SidecarFormat.Version
                    ? SqlDiagnostics.SidecarFormatTooNew
                    : SqlDiagnostics.SidecarFormatTooOld,
                file.Start,
                version.ToString(CultureInfo.InvariantCulture),
                SidecarFormat.Version.ToString(CultureInfo.InvariantCulture)
            )
        );
        return null;
    }

    // The first version of another tool, at the file's start when it is the file's own, and otherwise at the key of
    // the first entry that a git merge brought from another: one warning for the sidecar.
    private static (string Version, LocationInfo Location)? OtherToolVersion(Sidecar sidecar, SidecarFile file)
    {
        if (!IsThisTools(sidecar.ToolVersion))
        {
            return (sidecar.ToolVersion, file.Start);
        }

        for (var index = 0; index < sidecar.Queries.Count; index++)
        {
            var version = sidecar.Queries[index].ToolVersion;
            if (!IsThisTools(version))
            {
                return (version, file.EntryLocations[index]);
            }
        }

        return null;
    }

    // A version's suffix, as -dev, is not compared.
    private static bool IsThisTools(string toolVersion) =>
        string.Equals(PackageVersion.PrefixOf(toolVersion), PackageVersion.Prefix, StringComparison.Ordinal);

    /// <summary>
    /// What the message of <c>SQLSRC237</c> says is wrong.
    /// </summary>
    internal static string ReasonOf(SidecarError error) =>
        error.Kind switch
        {
            SidecarErrorKind.InvalidJson => "it is not one JSON object",
            SidecarErrorKind.MissingKey => $"'{error.Argument}' is missing",
            SidecarErrorKind.WrongType => $"'{error.Argument}' has a value of the wrong type",
            SidecarErrorKind.DuplicateKey => $"'{error.Argument}' is written twice",
            SidecarErrorKind.OrdinalMismatch => $"the ordinal {error.Argument} is not the position of its element",
            SidecarErrorKind.ColumnsWithoutRows => "it has columns for a query without rows",
            SidecarErrorKind.UnknownResultKind => $"'{error.Argument}' is not a result kind",
            _ => throw UnexpectedEnumValueException.Of(error.Kind),
        };
}
