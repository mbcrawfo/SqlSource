namespace SqlSource.Tool.Bundles;

/// <summary>
/// The names of a bundle's files.  A zip holds each at its root, and an unzipped bundle, or a fixture's folder, holds
/// them in the folder.
/// </summary>
internal static class BundleFiles
{
    /// <summary>What made the bundle and what it gave, as <see cref="BundleJson" /> writes it.</summary>
    public const string Info = "bundle.json";

    /// <summary>The one query, as a complete SqlSource file.</summary>
    public const string Query = "query.sql";

    /// <summary>The calls of describing the query, as <c>CaptureJson</c> writes them.</summary>
    public const string Capture = "capture.json";

    /// <summary>A sidecar of the one entry, when the query was described.</summary>
    public const string Expected = "expected.json";

    /// <summary>The settings of the planner the capture was made with, one <c>name=value</c> a line.</summary>
    public const string ExplainSettings = "explain-settings.txt";

    /// <summary>What the name of the bundle's mapping file adds to the bundle's; the mapping is never inside it.</summary>
    public const string MappingSuffix = ".mapping.json";
}
