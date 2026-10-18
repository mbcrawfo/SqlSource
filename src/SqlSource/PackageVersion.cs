namespace SqlSource;

/// <summary>
/// The version of the package, as <c>major.minor.patch</c>: what the tool writes as a sidecar's <c>toolVersion</c>
/// and compares a committed sidecar's with, and what the generator compares the prefix of a sidecar's with.
/// </summary>
internal static class PackageVersion
{
    private static readonly char[] SuffixStarts = ['-', '+'];

    /// <summary>
    /// The first three parts of the assembly's version.  The build sets that to <c>VersionPrefix</c> and the run
    /// number, so a <c>-dev</c> build and a <c>-pr</c> build of one version give one value.
    /// </summary>
    public static string Prefix { get; } = typeof(PackageVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// The text of <paramref name="version" /> before its first <c>-</c> or <c>+</c>: the version without a
    /// prerelease suffix or build metadata, to compare with <see cref="Prefix" />.
    /// </summary>
    public static string PrefixOf(string version)
    {
        var end = version.IndexOfAny(SuffixStarts);
        return end < 0 ? version : version.Substring(0, end);
    }
}
