using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// A plan, and what is wrong with it.  The planner reports nothing itself.
/// </summary>
/// <param name="Plan">The plan.  A file or a query with an error is in it, marked.</param>
/// <param name="Errors">The errors, in the order they are to be reported.</param>
internal sealed record RunPlanResult(RunPlan Plan, EquatableArray<ToolDiagnostic> Errors);
