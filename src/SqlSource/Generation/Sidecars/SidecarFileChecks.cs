using System;
using System.Collections.Immutable;
using System.Globalization;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// The checks of a sidecar itself, before any of its entries: whether it can be read, its format, and the version of
/// the tool that wrote it.
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
            if (
                !string.Equals(
                    PackageVersion.PrefixOf(sidecar.ToolVersion),
                    PackageVersion.Prefix,
                    StringComparison.Ordinal
                )
            )
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.SidecarToolVersionDiffers,
                        file.Start,
                        sidecar.ToolVersion,
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

    /// <summary>
    /// What the message of <c>SQLSRC237</c> says is wrong.  A kind that is no member gives a reason that names its
    /// number, never another kind's.
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
            _ => "it has an error of kind " + ((int)error.Kind).ToString(CultureInfo.InvariantCulture),
        };
}
