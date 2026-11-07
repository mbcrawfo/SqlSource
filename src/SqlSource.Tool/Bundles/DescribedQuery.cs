using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// What describing the one query of <c>diagnose</c> gave, which <see cref="DiagnoseBundles.Raw" /> makes a bundle of.
/// </summary>
/// <param name="Capture">What the run kept of its session, which opened and closed.</param>
/// <param name="Entry">The query's entry, when it was described.</param>
/// <param name="Failure">The describer's refusal, when it was not.</param>
internal sealed record DescribedQuery(RunCapture Capture, SidecarEntry? Entry, DescribeFailure? Failure);
