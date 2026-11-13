using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.MutationReport;

// What a kill matrix says of one test method, all the rows of a theory together, since a report tells the rows apart
// only by their ids.
//   Covered     the tested mutants a row reaches.
//   Kills       the mutants a row fails on.
//   Alone       of those, the ones no other focused method fails on, by file and place.
//   Cover       other focused methods that together fail on all it fails on, the one that fails on most first: the
//               smallest set found, which may not be the smallest.  Null when it kills nothing or has a kill alone.
//   RowsNeeded  how many of its rows together fail on all its rows fail on, found the same way; 0 when it kills
//               nothing.
//   Timeouts    the mutants a row reaches whose tests ran out of time, for which a report names no killer.
//   Survivors   the mutants a row reaches that no test of the run fails on, by file and place.
// A focused method is one that is not broad.
internal sealed record MethodEvidence(
    string File,
    string Name,
    int Rows,
    bool Broad,
    int Covered,
    int Kills,
    IReadOnlyList<MatrixMutant> Alone,
    IReadOnlyList<string>? Cover,
    int RowsNeeded,
    int Timeouts,
    IReadOnlyList<MatrixMutant> Survivors
);

// Condenses a kill matrix to what each test method kills.  A broad test, one that checks a whole output at once, is
// never another method's cover and never the other killer that stops a kill being a method's alone: its failure says
// that something changed, not which rule broke.
internal static class KillEvidence
{
    // One entry for each test method of the matrix, by file and then name.  isBroad is asked a method's full name.
    public static IReadOnlyList<MethodEvidence> Of(KillMatrix matrix, Func<string, bool> isBroad)
    {
        // The methods, each the tests of one file and name.
        var methodOf = new int[matrix.Tests.Count];
        var methods = new List<Method>();
        var index = new Dictionary<(string File, string Name), int>();
        for (var test = 0; test < methodOf.Length; test++)
        {
            var (_, file, name) = matrix.Tests[test];
            if (!index.TryGetValue((file, name), out var method))
            {
                method = methods.Count;
                index.Add((file, name), method);
                methods.Add(new Method(file, name, isBroad(name)));
            }

            methodOf[test] = method;
            methods[method].Rows.Add(test);
        }

        // The focused methods that kill each mutant, which the covers are found among.
        var focusedKillers = new Dictionary<int, int[]>();
        for (var at = 0; at < matrix.Mutants.Count; at++)
        {
            var mutant = matrix.Mutants[at];
            if (!MutantKinds.IsTested(mutant))
            {
                continue;
            }

            var killers = MethodsOf(mutant.KilledBy, methodOf);
            foreach (var method in MethodsOf([.. mutant.CoveredBy, .. mutant.KilledBy], methodOf))
            {
                methods[method].Covered++;
                if (mutant.Status == MutantStatus.Timeout)
                {
                    methods[method].Timeouts++;
                }
                else if (mutant.Status == MutantStatus.Survived)
                {
                    methods[method].Survivors.Add(mutant);
                }
            }

            if (mutant.Status != MutantStatus.Killed)
            {
                continue;
            }

            var focused = killers.Where(method => !methods[method].Broad).ToArray();
            focusedKillers.Add(at, focused);
            foreach (var method in killers)
            {
                _ = methods[method].Kills.Add(at);
                if (focused.All(other => other == method))
                {
                    methods[method].Alone.Add(mutant);
                }
            }
        }

        return
        [
            .. methods
                .Select(method => new MethodEvidence(
                    method.File,
                    method.Name,
                    method.Rows.Count,
                    method.Broad,
                    method.Covered,
                    method.Kills.Count,
                    [.. method.Alone.Order(ByPlace)],
                    method.Kills.Count == 0 || method.Alone.Count > 0
                        ? null
                        : CoverOf(methods.IndexOf(method), method, focusedKillers, methods),
                    RowsNeeded(method, matrix),
                    method.Timeouts,
                    [.. method.Survivors.Order(ByPlace)]
                ))
                .OrderBy(method => method.File, StringComparer.Ordinal)
                .ThenBy(method => method.Name, StringComparer.Ordinal),
        ];
    }

    // The order of mutants by project, file and place.
    internal static readonly Comparer<MatrixMutant> ByPlace = Comparer<MatrixMutant>.Create(
        (left, right) =>
        {
            var order = string.CompareOrdinal(left.Project, right.Project);
            if (order == 0)
            {
                order = string.CompareOrdinal(left.Key.File, right.Key.File);
            }

            if (order == 0)
            {
                order = left.Key.StartLine.CompareTo(right.Key.StartLine);
            }

            if (order == 0)
            {
                order = left.Key.StartColumn.CompareTo(right.Key.StartColumn);
            }

            return order == 0 ? left.Key.Ordinal.CompareTo(right.Key.Ordinal) : order;
        }
    );

    private static int[] MethodsOf(int[] tests, int[] methodOf) => [.. tests.Select(test => methodOf[test]).Distinct()];

    // The other focused methods that kill every mutant the method kills, each of which has one, since none of its
    // kills is its alone.
    private static List<string> CoverOf(
        int self,
        Method method,
        Dictionary<int, int[]> focusedKillers,
        List<Method> methods
    )
    {
        var candidates = new Dictionary<int, HashSet<int>>();
        foreach (var mutant in method.Kills)
        {
            foreach (var killer in focusedKillers[mutant])
            {
                if (killer == self)
                {
                    continue;
                }

                if (!candidates.TryGetValue(killer, out var kills))
                {
                    kills = [];
                    candidates.Add(killer, kills);
                }

                _ = kills.Add(mutant);
            }
        }

        return
        [
            .. Greedy(
                    method.Kills,
                    candidates,
                    (left, right) => string.CompareOrdinal(methods[left].Name, methods[right].Name)
                )
                .Select(candidate => methods[candidate].Name),
        ];
    }

    private static int RowsNeeded(Method method, KillMatrix matrix)
    {
        if (method.Kills.Count == 0)
        {
            return 0;
        }

        var rows = method.Rows.ToHashSet();
        var candidates = new Dictionary<int, HashSet<int>>();
        foreach (var mutant in method.Kills)
        {
            foreach (var test in matrix.Mutants[mutant].KilledBy)
            {
                if (!rows.Contains(test))
                {
                    continue;
                }

                if (!candidates.TryGetValue(test, out var kills))
                {
                    kills = [];
                    candidates.Add(test, kills);
                }

                _ = kills.Add(mutant);
            }
        }

        return Greedy(
            method.Kills,
            candidates,
            (left, right) => string.CompareOrdinal(matrix.Tests[left].Id, matrix.Tests[right].Id)
        ).Count;
    }

    // A set cover, greedily: the candidate that kills most of what is left, the first by the order on a tie, until
    // nothing is left.  Every mutant of all is killed by a candidate.
    private static List<int> Greedy(HashSet<int> all, Dictionary<int, HashSet<int>> candidates, Comparison<int> order)
    {
        var left = new HashSet<int>(all);
        var taken = new List<int>();
        while (left.Count > 0)
        {
            var best = -1;
            var most = 0;
            foreach (var (candidate, kills) in candidates)
            {
                var count = kills.Count(left.Contains);
                if (count > most || (count == most && count > 0 && order(candidate, best) < 0))
                {
                    (best, most) = (candidate, count);
                }
            }

            taken.Add(best);
            left.ExceptWith(candidates[best]);
            _ = candidates.Remove(best);
        }

        return taken;
    }

    private sealed class Method(string file, string name, bool broad)
    {
        public string File { get; } = file;
        public string Name { get; } = name;
        public bool Broad { get; } = broad;
        public List<int> Rows { get; } = [];
        public HashSet<int> Kills { get; } = [];
        public List<MatrixMutant> Alone { get; } = [];
        public List<MatrixMutant> Survivors { get; } = [];
        public int Covered { get; set; }
        public int Timeouts { get; set; }
    }
}
