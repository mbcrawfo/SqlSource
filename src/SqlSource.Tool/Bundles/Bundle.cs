using System.Collections.Generic;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The files of a bundle, as text.  A text file is UTF-8, and is kept as it was read: <see cref="Query" /> keeps its
/// line ends.
/// </summary>
/// <param name="Info">The bundle's <c>bundle.json</c>; null for a folder without one, such as a fixture's.</param>
/// <param name="Query">The text of <c>query.sql</c>.</param>
/// <param name="Capture">The text of <c>capture.json</c>.</param>
/// <param name="Expected">The text of <c>expected.json</c>, or null when there is none.</param>
/// <param name="ExplainSettings">
/// The lines of <c>explain-settings.txt</c>, trimmed and without the blank ones; empty when there is none.
/// </param>
internal sealed record Bundle(
    BundleInfo? Info,
    string Query,
    string Capture,
    string? Expected,
    IReadOnlyList<string> ExplainSettings
);
