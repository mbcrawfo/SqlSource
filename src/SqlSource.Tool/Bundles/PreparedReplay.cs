using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// What replaying a bundle needs, as the steps up to the replay gave it.
/// </summary>
/// <param name="Query">The query, as <c>BundleReplay.Plan</c> gives it.</param>
/// <param name="Dialect">Its dialect, as <c>BundleReplay.Plan</c> gives it.</param>
/// <param name="Capture">The calls to answer from, as <c>BundleReplay.CaptureOf</c> gives them.</param>
/// <param name="Describer">The describer of the dialect.</param>
/// <param name="Expected">
/// What the bundle says describing its query gave, or null, as <c>BundleReplay.ExpectedOf</c> gives it.
/// </param>
internal sealed record PreparedReplay(
    PlannedQuery Query,
    SqlDialect Dialect,
    DescribeCapture Capture,
    IQueryDescriber Describer,
    ReplayOutcome? Expected
);
