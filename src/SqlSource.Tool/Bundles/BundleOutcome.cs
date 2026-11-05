namespace SqlSource.Tool.Bundles;

/// <summary>
/// What describing the bundle's query gave.
/// </summary>
internal enum BundleOutcomeKind
{
    /// <summary>An entry, which the bundle's <c>expected.json</c> holds.</summary>
    Entry,

    /// <summary>A failure, named by its id and its step.</summary>
    Failure,
}

/// <summary>
/// What describing the bundle's query gave: an entry, or a failure with its diagnostic's id and the step it was
/// reported at.
/// </summary>
/// <param name="Kind">Which of the two.</param>
/// <param name="Id">The id of the failure's diagnostic, such as <c>SQLSRC226</c>; null for an entry.</param>
/// <param name="Step">
/// The step of the failure as <c>DescribeStepName.Of</c> names it, such as <c>describe parameters</c>; null for an
/// entry, and for a failure to open the session, which is at no step.
/// </param>
internal sealed record BundleOutcome(BundleOutcomeKind Kind, string? Id, string? Step)
{
    /// <summary>The query was described.</summary>
    public static BundleOutcome Entry { get; } = new(BundleOutcomeKind.Entry, Id: null, Step: null);

    /// <summary>The query was refused, with this id, at this step or at none.</summary>
    public static BundleOutcome Failure(string id, string? step) => new(BundleOutcomeKind.Failure, id, step);
}
