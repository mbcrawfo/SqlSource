using SqlSource.Snapshot;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// What replaying a bundle gave, or what the bundle says describing its query gave: an entry, a refusal, or a question
/// that the bundle's capture does not answer.
/// </summary>
internal abstract record ReplayOutcome
{
    // Only the records below derive from it.
    private ReplayOutcome() { }

    /// <summary>The query was described.</summary>
    /// <param name="Entry">The entry, with no <see cref="SidecarEntry.NameSpan" />.</param>
    internal sealed record Described(SidecarEntry Entry) : ReplayOutcome;

    /// <summary>The describer refused the query, or the session.</summary>
    /// <param name="Id">The id of the failure's diagnostic, such as <c>SQLSRC226</c>.</param>
    /// <param name="Step">
    /// The step as <c>DescribeStepName.Of</c> names it, such as <c>describe parameters</c>, or null for a session that
    /// could not be opened.
    /// </param>
    internal sealed record Refused(string Id, string? Step) : ReplayOutcome;

    /// <summary>The describer asked what the capture holds no answer to.</summary>
    /// <param name="Method">
    /// The call that has no answer, such as <c>explain</c>, or <c>BundleReplay.UnknownQuestion</c> for a miss that
    /// names none.
    /// </param>
    /// <param name="Reason">The miss's message, which says what was asked or why the answer cannot be read.</param>
    internal sealed record Missed(string Method, string Reason) : ReplayOutcome;
}
