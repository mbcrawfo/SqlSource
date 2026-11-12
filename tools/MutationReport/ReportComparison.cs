using System.Collections.Generic;

namespace SqlSource.MutationReport;

// A mutant that was detected before, Killed or Timeout, and is not shown to be detected after.  After is null when the
// second report does not hold the mutant.
internal sealed record Change(MutantKey Key, MutantStatus Before, MutantStatus? After);

// What two reports of the same source say of the mutants the first detected.
internal sealed record Comparison(int Detected, IReadOnlyList<Change> Lost, IReadOnlyList<Change> NotComparable)
{
    public bool Holds => Lost.Count == 0 && NotComparable.Count == 0;
}

// Compares two reports of the same source, mutant for mutant.  A mutant that the first run did not detect is left
// out, whatever the second made of it: the guard is that nothing detected stops being detected.
internal static class ReportComparison
{
    public static Comparison Compare(IReadOnlyList<Mutant> before, IReadOnlyList<Mutant> after)
    {
        var statusAfter = new Dictionary<MutantKey, MutantStatus>(after.Count);
        foreach (var mutant in after)
        {
            statusAfter[mutant.Key] = mutant.Status;
        }

        var detected = 0;
        var lost = new List<Change>();
        var notComparable = new List<Change>();
        foreach (var mutant in before)
        {
            if (!IsDetected(mutant.Status))
            {
                continue;
            }

            detected++;
            if (!statusAfter.TryGetValue(mutant.Key, out var status))
            {
                notComparable.Add(new Change(mutant.Key, mutant.Status, null));
            }
            else if (status is MutantStatus.Survived or MutantStatus.NoCoverage)
            {
                lost.Add(new Change(mutant.Key, mutant.Status, status));
            }
            else if (!IsDetected(status))
            {
                notComparable.Add(new Change(mutant.Key, mutant.Status, status));
            }
        }

        lost.Sort(ByPosition);
        notComparable.Sort(ByPosition);
        return new Comparison(detected, lost, notComparable);
    }

    // A mutant whose tests ran out of time counts as detected, as Stryker's score counts it.
    private static bool IsDetected(MutantStatus status) => status is MutantStatus.Killed or MutantStatus.Timeout;

    private static int ByPosition(Change left, Change right)
    {
        var (a, b) = (left.Key, right.Key);
        var order = string.CompareOrdinal(a.File, b.File);
        if (order == 0)
        {
            order = a.StartLine.CompareTo(b.StartLine);
        }

        if (order == 0)
        {
            order = a.StartColumn.CompareTo(b.StartColumn);
        }

        return order == 0 ? a.Ordinal.CompareTo(b.Ordinal) : order;
    }
}
