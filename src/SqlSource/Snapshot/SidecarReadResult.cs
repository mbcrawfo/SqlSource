namespace SqlSource.Snapshot;

/// <summary>
/// What reading a sidecar gave, in one of three states: read, with the sidecar and the format version of this
/// reader; of another format version, with that version alone; or malformed, with the error alone.
/// </summary>
/// <param name="Sidecar">The sidecar, when it was read.</param>
/// <param name="FormatVersion">The file's format version, unless the file is malformed.</param>
/// <param name="Error">Why the file is malformed.</param>
internal sealed record SidecarReadResult(Sidecar? Sidecar, int? FormatVersion, SidecarError? Error)
{
    public static SidecarReadResult Of(Sidecar sidecar) => new(sidecar, sidecar.FormatVersion, null);

    public static SidecarReadResult OfAnotherVersion(int formatVersion) => new(null, formatVersion, null);

    public static SidecarReadResult Malformed(SidecarError error) => new(null, null, error);
}
