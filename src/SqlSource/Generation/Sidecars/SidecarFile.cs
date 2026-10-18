using SqlSource.Diagnostics;
using SqlSource.Snapshot.Reading;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// One sidecar of a claimed file, read, with every position a check of it reports at.  It holds no text, so no step
/// after the read needs the file.
/// </summary>
/// <param name="SqlPath">The normalised path of its <c>.sql</c> file, spelled as the claimed paths spell it.</param>
/// <param name="Path">The sidecar's path, as the compiler gives it.</param>
/// <param name="Result">What the read gave.</param>
/// <param name="Start">The empty span at the start of the file.</param>
/// <param name="ErrorLocation">Where the error of a malformed file is, and null otherwise.</param>
/// <param name="EntryLocations">
/// Where the key of each entry of a file that was read is, quotes included: the <c>i</c>th is the key of
/// <c>Result.Sidecar.Queries[i]</c>.  Empty otherwise.
/// </param>
internal sealed record SidecarFile(
    string SqlPath,
    string Path,
    SidecarReadResult Result,
    LocationInfo Start,
    LocationInfo? ErrorLocation,
    EquatableArray<LocationInfo> EntryLocations
);
