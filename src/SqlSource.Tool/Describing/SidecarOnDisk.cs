using System;
using System.IO;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The sidecar beside a <c>.sql</c> file as a run found it.
/// </summary>
/// <param name="Path">The full path of the sidecar.</param>
/// <param name="Text">The text of the file, or null when there is none that can be opened.</param>
/// <param name="Usable">
/// The sidecar, when it was read and this version of the tool wrote it in this version of the format.  Only such a
/// one has entries a run keeps: an entry from an older tool never stands beside a new one.
/// </param>
/// <param name="NewerFormat">The format version of the file, when it is higher than the tool's.</param>
internal sealed record SidecarOnDisk(string Path, string? Text, Sidecar? Usable, int? NewerFormat)
{
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
            return new SidecarOnDisk(path, null, null, null);
        }

        var read = SidecarReader.Read(text);
        if (read.Sidecar is { } sidecar)
        {
            return new SidecarOnDisk(path, text, sidecar.IsWrittenBy(PackageVersion.Prefix) ? sidecar : null, null);
        }

        return new SidecarOnDisk(
            path,
            text,
            null,
            read.FormatVersion is { } version && version > SidecarFormat.Version ? version : null
        );
    }
}
