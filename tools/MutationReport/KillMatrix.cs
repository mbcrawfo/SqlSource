using System.Collections.Generic;
using System.IO;

namespace SqlSource.MutationReport;

// A test of a report.  Id is Stryker's, one for each row of a theory.  File is the test's path from the repository
// root, with '/', and Name its full name, which the rows of a theory share.
internal sealed record ReportTest(string Id, string File, string Name);

// A mutant with the tests that reach it and the tests that fail on it, each the index of a test in KillMatrix.Tests.
// Project is the folder of the mutated project from the repository root, "src/SqlSource", and Reason the report's
// statusReason, or "" when it gives none.
internal sealed record MatrixMutant(
    string Project,
    MutantKey Key,
    MutantStatus Status,
    bool Static,
    string Reason,
    int[] CoveredBy,
    int[] KilledBy
);

// Which tests reach and kill each mutant, of one report or of several as one.
internal sealed record KillMatrix(IReadOnlyList<ReportTest> Tests, IReadOnlyList<MatrixMutant> Mutants)
{
    // The matrix of several scopes' reports: a test is one by its id, and a mutant by its project and key.  Every
    // report holds every mutant of its project, most of them left out, so of a mutant in several the one that was
    // tested is kept.  Throws InvalidDataException for a mutant that two reports tested, since its kills would be
    // counted twice.
    public static KillMatrix Merge(IReadOnlyList<KillMatrix> matrices)
    {
        var tests = new List<ReportTest>();
        var testIndex = new Dictionary<string, int>();
        var mutants = new List<MatrixMutant>();
        var mutantIndex = new Dictionary<(string Project, MutantKey Key), int>();
        foreach (var matrix in matrices)
        {
            var renumbered = new int[matrix.Tests.Count];
            for (var index = 0; index < renumbered.Length; index++)
            {
                var test = matrix.Tests[index];
                if (!testIndex.TryGetValue(test.Id, out var merged))
                {
                    merged = tests.Count;
                    testIndex.Add(test.Id, merged);
                    tests.Add(test);
                }

                renumbered[index] = merged;
            }

            foreach (var mutant in matrix.Mutants)
            {
                var identity = (mutant.Project, mutant.Key);
                if (!mutantIndex.TryGetValue(identity, out var at))
                {
                    mutantIndex.Add(identity, mutants.Count);
                    mutants.Add(Renumber(mutant, renumbered));
                }
                else if (MutantKinds.IsTested(mutant))
                {
                    if (MutantKinds.IsTested(mutants[at]))
                    {
                        throw new InvalidDataException(
                            $"Two reports tested a mutant of {mutant.Project}/{mutant.Key.File}: their scopes overlap."
                        );
                    }

                    mutants[at] = Renumber(mutant, renumbered);
                }
            }
        }

        return new KillMatrix(tests, mutants);
    }

    private static MatrixMutant Renumber(MatrixMutant mutant, int[] renumbered) =>
        mutant with
        {
            CoveredBy = [.. Renumbered(mutant.CoveredBy, renumbered)],
            KilledBy = [.. Renumbered(mutant.KilledBy, renumbered)],
        };

    private static IEnumerable<int> Renumbered(int[] tests, int[] renumbered)
    {
        foreach (var test in tests)
        {
            yield return renumbered[test];
        }
    }
}

// What a run made of a mutant, for the evidence of the test review.
internal static class MutantKinds
{
    // The run decided the mutant: a test failed on it, none did, its tests ran out of time or none reaches it.
    public static bool IsTested(MatrixMutant mutant) =>
        mutant.Status
            is MutantStatus.Killed
                or MutantStatus.Survived
                or MutantStatus.Timeout
                or MutantStatus.NoCoverage;

    // No run can decide the mutant: it does not compile, or it is static, which tools/mutation-test.sh keeps out.
    // The code it is in has no mutation evidence.
    public static bool IsBlind(MatrixMutant mutant) =>
        mutant.Status is MutantStatus.CompileError || (mutant.Static && !IsTested(mutant));
}
