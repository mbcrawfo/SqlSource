using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Testing.Realistic;

// The noise of a describer over a corpus: the columns that are not null in truth and that the describer answers
// nullable or unknown.  Noise is safe, since the generated type is wider than it need be, and it is a cost the corpus
// keeps in view: each engine's test holds the count to a budget for each recorded major.
internal static class RealisticNoise
{
    // The noise of one case, from the columns that RealisticDescriptions.ColumnsOf gave for it.
    public static int CountOf(RealisticCase found, IReadOnlyList<SidecarColumn> columns) =>
        found.Columns.Where((truth, index) => !truth.Nullable && columns[index].Nullable != false).Count();

    // What a count over the corpus says against the budget held for that major: over it, the fault and the engine's
    // hint of what to do; under it, that the budget is lowered so the improvement is kept; at it, nothing.
    public static string? Verdict(int count, int held, int major, string overHint)
    {
        if (count > held)
        {
            return $"The noise is {count}, over the budget of {held}: " + overHint;
        }

        if (count < held)
        {
            return $"The noise is {count}, under the budget of {held}: lower the budget of {major} in Budgets to "
                + $"{count} in this change, so the improvement is kept.";
        }

        return null;
    }
}
