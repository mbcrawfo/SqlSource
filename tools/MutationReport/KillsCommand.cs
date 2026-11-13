using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// The kills command: what one test method kills in one scope's report, mutant by mutant, with the other methods that
// kill each, and the mutants it reaches that ran out of time or that no test fails on.  A verifier of the test
// review reads it before believing that a test is redundant, since a slice's evidence gives only the counts.  A broad
// test is listed among the other killers like any other: the reader knows which they are.
internal static class KillsCommand
{
    private const int MostKillers = 10;

    // The name is a method's full name, or the end of one after a '.', as "ATests.Reads".  0: the kills are printed.
    // 1: the report holds no such test, or several that end so.
    public static async Task<int> RunAsync(KillMatrix matrix, string name, TextWriter output, TextWriter error)
    {
        var names = matrix.Tests.Select(test => test.Name).Distinct(StringComparer.Ordinal).ToList();
        if (!names.Contains(name, StringComparer.Ordinal))
        {
            var ends = names
                .Where(full => full.EndsWith("." + name, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToList();
            if (ends.Count != 1)
            {
                await error
                    .WriteLineAsync(
                        ends.Count == 0
                            ? $"The report holds no test named '{name}': a scope's report holds its own tests."
                            : $"Several tests end in '{name}': {string.Join(", ", ends)}"
                    )
                    .ConfigureAwait(false);
                return 1;
            }

            name = ends[0];
        }

        var rows = Enumerable.Range(0, matrix.Tests.Count).Where(test => matrix.Tests[test].Name == name).ToHashSet();
        var (kills, alone, timeouts, survivors) = (0, 0, 0, 0);
        foreach (var mutant in matrix.Mutants.Order(KillEvidence.ByPlace))
        {
            var place = $"{EvidenceWriter.Place(mutant)} -> `{EvidenceWriter.Replacement(mutant.Key.Replacement)}`";
            if (mutant.Status == MutantStatus.Timeout && mutant.CoveredBy.Any(rows.Contains))
            {
                timeouts++;
                await output.WriteLineAsync($"TIMEOUT {place}").ConfigureAwait(false);
            }
            else if (mutant.Status == MutantStatus.Survived && mutant.CoveredBy.Any(rows.Contains))
            {
                survivors++;
                await output.WriteLineAsync($"SURVIVED {place}").ConfigureAwait(false);
            }

            if (mutant.Status != MutantStatus.Killed || !mutant.KilledBy.Any(rows.Contains))
            {
                continue;
            }

            kills++;
            await output.WriteLineAsync(place).ConfigureAwait(false);
            var others = mutant
                .KilledBy.Where(test => !rows.Contains(test))
                .Select(test => matrix.Tests[test].Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            if (others.Count == 0)
            {
                alone++;
                await output.WriteLineAsync("    killed by it alone").ConfigureAwait(false);
                continue;
            }

            var more = others.Count > MostKillers ? $" and {Number(others.Count - MostKillers)} more" : "";
            var named = string.Join(", ", others.Take(MostKillers));
            await output
                .WriteLineAsync($"    also killed by {Number(others.Count)} methods: {named}{more}")
                .ConfigureAwait(false);
        }

        await output
            .WriteLineAsync(
                $"{name}: {Number(rows.Count)} rows, {Number(kills)} kills, {Number(alone)} alone, "
                    + $"{Number(timeouts)} timeouts under it, {Number(survivors)} survivors under it"
            )
            .ConfigureAwait(false);
        return 0;
    }

    private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
}
