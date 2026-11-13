using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SqlSource.MutationReport;

// A test method of a slice.  Name is its class and method, without the namespace, and Seconds the time of its rows
// together, or null when its project's run holds no time for it.  Kills is null when no mutation scope ran it.
internal sealed record EvidenceMethod(string Name, int Rows, double? Seconds, bool Skipped, MethodEvidence? Kills);

internal sealed record EvidenceFile(string Path, int Lines, IReadOnlyList<EvidenceMethod> Methods);

// A mutation scope that ran tests of a slice: how many tests it ran, of which folders, and how many mutants it
// tested, of which folders of the source.
internal sealed record EvidenceScope(
    string Name,
    int Tests,
    IReadOnlyList<string> TestFolders,
    int Mutants,
    IReadOnlyList<string> SourceFolders
);

// A mutant that no test fails on, with the full names of the slice's methods that reach it.
internal sealed record EvidenceSurvivor(MatrixMutant Mutant, IReadOnlyList<string> Methods);

// A source file that the slice's tests reach: the lines of its mutants that no test reaches, that do not compile
// and that are static, as ranges, and the lines of those under the slice's tests that ran out of time.
internal sealed record EvidenceSource(
    string File,
    IReadOnlyList<(int From, int To)> Uncovered,
    IReadOnlyList<(int From, int To)> Uncompiled,
    IReadOnlyList<(int From, int To)> Static,
    IReadOnlyList<int> Timeouts
);

internal sealed record EvidenceTime(string Class, double Seconds);

// What the measurements of a run say of one slice.  Project is the slice's test project, from the repository root,
// and ProjectSeconds the time of all its tests, or null when the run did not run it.
internal sealed record SliceEvidence(
    string Name,
    IReadOnlyList<EvidenceFile> Files,
    IReadOnlyList<EvidenceScope> Scopes,
    IReadOnlyList<EvidenceSurvivor> Survivors,
    IReadOnlyList<EvidenceSource> Source,
    string Project,
    double? ProjectSeconds,
    IReadOnlyList<EvidenceTime> Times
);

// Writes a slice's evidence file, the text a reviewer of the test review reads beside the slice's tests.
// .claude/skills/reviewing-test-quality/briefs.md teaches how to read it, so a change here changes that brief.
internal static partial class EvidenceWriter
{
    private const int MostAlone = 10;
    private const int MostCover = 8;
    private const int MostSurvivorMethods = 3;
    private const int LongestReplacement = 60;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    public static void Write(TextWriter writer, SliceEvidence evidence)
    {
        writer.WriteLine($"# Evidence: {evidence.Name}");
        writer.WriteLine();
        writer.WriteLine(
            $"Files: {Number(evidence.Files.Count)}, lines: {Number(evidence.Files.Sum(file => file.Lines))}."
        );
        writer.WriteLine();
        foreach (var file in evidence.Files)
        {
            writer.WriteLine($"- {file.Path} ({Number(file.Lines)})");
        }

        writer.WriteLine();
        writer.WriteLine("## Mutation scopes");
        writer.WriteLine();
        if (evidence.Scopes.Count == 0)
        {
            writer.WriteLine(
                "None: no mutation scope ran these tests, so they are judged by reading and the named-cover rule."
            );
        }
        else
        {
            foreach (var scope in evidence.Scopes)
            {
                writer.WriteLine(
                    $"- {scope.Name}: ran {Number(scope.Tests)} tests of {string.Join(", ", scope.TestFolders)}; "
                        + $"tested {Number(scope.Mutants)} mutants of {string.Join(", ", scope.SourceFolders)}."
                );
            }

            writer.WriteLine();
            writer.WriteLine(
                "A kill by a test outside a scope is not seen.  "
                    + "The rows of a theory are told apart only by their count."
            );
        }

        writer.WriteLine();
        writer.WriteLine("## Tests");
        foreach (var file in evidence.Files)
        {
            writer.WriteLine();
            writer.WriteLine($"### {file.Path}");
            writer.WriteLine();
            if (file.Methods.Count == 0)
            {
                writer.WriteLine("No tests.");
            }

            foreach (var method in file.Methods)
            {
                WriteMethod(writer, method, evidence.Scopes.Count > 0);
            }
        }

        if (evidence.Survivors.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("## Survivors under these tests");
            writer.WriteLine();
            foreach (var (mutant, methods) in evidence.Survivors)
            {
                // A survivor that many methods reach is no statement about one of them, so only a few are named.
                var count = methods.Count == 1 ? "1 method" : $"{Number(methods.Count)} methods";
                var names =
                    methods.Count > MostSurvivorMethods
                        ? ""
                        : ": " + string.Join(", ", methods.Select(name => $"`{Short(name)}`"));
                writer.WriteLine(
                    $"- {Place(mutant)} -> `{Replacement(mutant.Key.Replacement)}`: covered by {count} here{names}"
                );
            }
        }

        if (evidence.Source.Any(file => file.Timeouts.Count > 0))
        {
            writer.WriteLine();
            writer.WriteLine("## Timeouts under these tests");
            writer.WriteLine();
            writer.WriteLine("A timeout is read before it is believed: the report names no test that fails on it.");
            writer.WriteLine();
            foreach (var file in evidence.Source.Where(file => file.Timeouts.Count > 0))
            {
                var lines = string.Join(", ", file.Timeouts.Select(Number));
                var word = file.Timeouts.Count == 1 ? "line" : "lines";
                writer.WriteLine($"- {file.File}: {Number(file.Timeouts.Count)} ({word} {lines})");
            }
        }

        if (evidence.Source.Any(HasGaps))
        {
            writer.WriteLine();
            writer.WriteLine("## Source these tests reach: uncovered and blind");
            writer.WriteLine();
            foreach (var file in evidence.Source.Where(HasGaps))
            {
                var parts = new List<string>();
                if (file.Uncovered.Count > 0)
                {
                    parts.Add($"no test covers lines {Ranges(file.Uncovered)}");
                }

                if (file.Uncompiled.Count > 0)
                {
                    parts.Add($"blind, does not compile: {Ranges(file.Uncompiled)}");
                }

                if (file.Static.Count > 0)
                {
                    parts.Add($"blind, static: {Ranges(file.Static)}");
                }

                writer.WriteLine($"- {file.File}: {string.Join("; ", parts)}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("## Time");
        writer.WriteLine();
        if (evidence.ProjectSeconds is not { } total)
        {
            writer.WriteLine($"Not run: {evidence.Project} has no times in this run.");
        }
        else if (evidence.Times.Count == 0)
        {
            writer.WriteLine("No test of these files ran.");
        }
        else
        {
            foreach (var (name, seconds) in evidence.Times)
            {
                var share = total > 0 ? 100 * seconds / total : 0;
                writer.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"- `{name}`: {Seconds(seconds)}, {share:F1}% of {evidence.Project} ({Seconds(total)})"
                    )
                );
            }
        }

        if (evidence.Project.StartsWith(Condenser.DatabaseProjects, StringComparison.Ordinal))
        {
            writer.WriteLine();
            writer.WriteLine("Lines only the database projects reach: see database-only-lines.md.");
        }
    }

    private static void WriteMethod(TextWriter writer, EvidenceMethod method, bool measured)
    {
        var rows = method.Rows == 1 ? "1 row" : $"{Number(method.Rows)} rows";
        var time = "no time";
        if (method.Skipped)
        {
            time = "skipped";
        }
        else if (method.Seconds is { } seconds)
        {
            time = Seconds(seconds);
        }

        var line = $"- `{method.Name}`{(method.Kills is { Broad: true } ? " (broad)" : "")}: {rows}, {time}";
        if (method.Kills is not { } kills)
        {
            writer.WriteLine(measured ? line + "; not measured: in no scope's run" : line);
            return;
        }

        if (kills.Broad)
        {
            writer.WriteLine($"{line}; kills {Number(kills.Kills)}");
            return;
        }

        if (kills.Kills == 0)
        {
            var reach =
                kills.Covered == 0
                    ? "reaches no tested mutant"
                    : $"covers {Number(kills.Covered)} mutants and kills none";
            var under = kills.Timeouts == 0 ? "" : $"; timeouts under it {Number(kills.Timeouts)}";
            writer.WriteLine($"{line}; {reach}{under}");
            return;
        }

        line +=
            $"; kills {Number(kills.Kills)}, alone {Number(kills.Alone.Count)}, "
            + $"timeouts under it {Number(kills.Timeouts)}";
        if (kills.RowsNeeded < kills.Rows)
        {
            line += $"; rows needed {Number(kills.RowsNeeded)}";
        }

        writer.WriteLine(
            kills.Cover is { } cover
                ? $"{line}; cover: {Listed(cover.Select(name => $"`{Short(name)}`"), ", ", MostCover)}"
                : $"{line}; no cover"
        );
        if (kills.Alone.Count > 0)
        {
            writer.WriteLine($"    - alone: {Listed(kills.Alone.Select(Place), "; ", MostAlone)}");
        }
    }

    private static bool HasGaps(EvidenceSource file) =>
        file.Uncovered.Count > 0 || file.Uncompiled.Count > 0 || file.Static.Count > 0;

    // Where a mutant is and what it does: "src/SqlSource/Parsing/SqlLexer.cs:120:17 Equality mutation".
    internal static string Place(MatrixMutant mutant) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{mutant.Project}/{mutant.Key.File}:{mutant.Key.StartLine}:{mutant.Key.StartColumn} {mutant.Key.Mutator}"
        );

    // A replacement on one line, cut when it is long.
    internal static string Replacement(string replacement)
    {
        var line = WhiteSpace().Replace(replacement, " ").Trim();
        return line.Length <= LongestReplacement ? line : line[..LongestReplacement] + "…";
    }

    // A method's class and name, without the namespace.
    internal static string Short(string fullName)
    {
        var method = fullName.LastIndexOf('.');
        var type = method > 0 ? fullName.LastIndexOf('.', method - 1) : -1;
        return fullName[(type + 1)..];
    }

    private static string Listed(IEnumerable<string> items, string separator, int most)
    {
        var all = items.ToList();
        var shown = string.Join(separator, all.Take(most));
        return all.Count <= most ? shown : $"{shown} and {Number(all.Count - most)} more";
    }

    internal static string Ranges(IEnumerable<(int From, int To)> ranges) =>
        string.Join(
            ", ",
            ranges.Select(range =>
                range.From == range.To
                    ? range.From.ToString(CultureInfo.InvariantCulture)
                    : string.Create(CultureInfo.InvariantCulture, $"{range.From}-{range.To}")
            )
        );

    internal static string Number(int number) => number.ToString("N0", CultureInfo.InvariantCulture);

    internal static string Seconds(double seconds) =>
        seconds.ToString(seconds < 1 ? "F3" : "N1", CultureInfo.InvariantCulture) + " s";
}
