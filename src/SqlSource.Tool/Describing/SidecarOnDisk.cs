using System;
using System.IO;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The sidecar beside a <c>.sql</c> file as a run found it.
/// </summary>
/// <param name="Path">The full path of the sidecar.</param>
/// <param name="Exists">
/// Whether something is at the path: a file, one the system will not open included, or a directory.
/// </param>
/// <param name="Text">The text of the file, or null when there is none that can be opened.</param>
/// <param name="Sidecar">
/// The sidecar, when it was read in this version of the format, whichever version of the tool wrote it.
/// <c>--check</c> compares it; a run that writes keeps entries of <see cref="Usable" /> alone.
/// </param>
/// <param name="FormatVersion">The format version of the file, or null when it is absent or malformed.</param>
/// <param name="OpenFailure">
/// The reason the system gave, when something is at the path and it could not be opened.  Null otherwise.
/// </param>
internal sealed record SidecarOnDisk(
    string Path,
    bool Exists,
    string? Text,
    Sidecar? Sidecar,
    int? FormatVersion,
    string? OpenFailure
)
{
    /// <summary>
    /// The sidecar, when this version of the tool wrote it in this version of the format.  Only such a one has
    /// entries a run keeps: an entry from an older tool never stands beside a new one.
    /// </summary>
    public Sidecar? Usable => Sidecar is { } sidecar && sidecar.IsWrittenBy(PackageVersion.Prefix) ? sidecar : null;

    /// <summary>The format version of the file, when it is higher than the tool's.</summary>
    public int? NewerFormat => FormatVersion is { } version && version > SidecarFormat.Version ? version : null;

    /// <summary>
    /// Reads the sidecar of a <c>.sql</c> file.  One that is absent, that the system will not open, that is
    /// malformed, or that has a lower format version or another tool version is not usable, and is written again.
    /// </summary>
    public static SidecarOnDisk Read(string sqlPath)
    {
        var path = SidecarFormat.PathFor(sqlPath);
        string text;
        try
        {
            // Without a byte order mark, as the reader asks.
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // No file, a directory, or a file that is locked: writing it will say so if it cannot be mended.
            var exists = File.Exists(path) || Directory.Exists(path);
            return new SidecarOnDisk(path, exists, null, null, null, exists ? exception.Message : null);
        }

        var read = SidecarReader.Read(text);
        return new SidecarOnDisk(path, Exists: true, text, read.Sidecar, read.FormatVersion, OpenFailure: null);
    }
}
