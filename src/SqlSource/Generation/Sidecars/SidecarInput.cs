using Microsoft.CodeAnalysis;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// A <c>.sql.json</c> file whose <c>.sql</c> file a type claims: a sidecar the generator reads.
/// </summary>
/// <param name="File">
/// The file.  Compared by reference, as <see cref="FileParseInput.File" /> is: the compiler hands out the same object
/// until the file changes.
/// </param>
/// <param name="SqlPath">
/// The normalised path of its <c>.sql</c> file, spelled as the claimed paths spell it, whatever the case of the
/// sidecar's own path.
/// </param>
internal sealed record SidecarInput(AdditionalText File, string SqlPath)
{
    /// <summary>
    /// The input of <paramref name="file" />, or null when no type claims its <c>.sql</c> file, as for a sidecar with
    /// no <c>.sql</c> file beside it: such a sidecar is never read.
    /// </summary>
    public static SidecarInput? Create(AdditionalText file, EquatableArray<string> claimedPaths)
    {
        if (Paths.SqlPath.SqlPathOfSidecar(file.Path) is not { } sqlPath)
        {
            return null;
        }

        var index = Paths.SqlPath.IndexOf(claimedPaths, sqlPath, static path => path);
        return index < 0 ? null : new SidecarInput(file, claimedPaths[index]);
    }
}
