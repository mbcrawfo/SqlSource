namespace SqlSource;

/// <summary>
/// The version of the package, as <c>major.minor.patch</c>: what the tool writes as a sidecar's <c>toolVersion</c>
/// and the generator compares with it.
/// </summary>
internal static class PackageVersion
{
    /// <summary>
    /// The first three parts of the assembly's version.  The build sets that to <c>VersionPrefix</c> and the run
    /// number, so a <c>-dev</c> build and a <c>-pr</c> build of one version give one value.
    /// </summary>
    public static string Prefix { get; } = typeof(PackageVersion).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
