using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Linq;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// The names PostgreSQL's parser and planner make up and a plan prints: the aliases of a set operation and a
/// <c>VALUES</c> list, the columns of one, a window with no name, a sub-plan's words and the like, found by the spike
/// of phase 10 in the source of 14 to 18 and in their plans.
/// </summary>
/// <remarks>
/// A name is compared as folded.  A form whose letters are upper case, such as <c>*SELECT* 1</c>, is quoted where a
/// plan prints it, so it is compared as printed; every other form is lower case.
/// </remarks>
internal static class PostgresPlannerNames
{
    // The forms that are one text.
    private static readonly FrozenSet<string> Whole = FrozenSet.ToFrozenSet(
        [
            "*SELECT*",
            "*VALUES*",
            "*RESULT*",
            "*ANTI*",
            "*MERGE*",
            "*GROUP*",
            "subplan",
            "initplan",
            "hashed",
            "cte",
            "unnamed_subquery",
            "any_subquery",
            "unnamed_join",
            "excluded",
            "worktable",
            "old",
            "new",
        ],
        StringComparer.Ordinal
    );

    // The forms that are a text and a number, one or more ASCII digits.
    private static readonly string[] Numbered = ["*SELECT* ", "*VALUES*_", "column", "col", "w"];

    private static readonly SearchValues<char> AsciiDigits = SearchValues.Create("0123456789");

    /// <summary>Whether a folded name is one the planner makes.</summary>
    /// <param name="folded">The name, as <see cref="PostgresWords.Fold" /> gives it.</param>
    public static bool Is(string folded) =>
        Whole.Contains(folded)
        || Numbered.Any(prefix =>
            folded.StartsWith(prefix, StringComparison.Ordinal) && IsNumber(folded.AsSpan(prefix.Length))
        );

    private static bool IsNumber(ReadOnlySpan<char> text) => !text.IsEmpty && !text.ContainsAnyExcept(AsciiDigits);
}
