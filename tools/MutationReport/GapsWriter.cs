using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlSource.MutationReport;

// Writes the two files of a run's evidence that belong to no slice: the mutants of each source file, for the gaps
// that no test owns, and the source lines that only the database projects reach.
internal static class GapsWriter
{
    // A table for each mutated project of the source files in the folders where the run tested a mutant, those with
    // most survivors and unreached mutants first.
    public static void Write(TextWriter writer, KillMatrix matrix)
    {
        writer.WriteLine("# Gaps");
        writer.WriteLine();
        writer.WriteLine(
            "The mutants of each source file, in the folders where this run tested one.  A survivor is a mutant that "
                + "no test of its scope fails on, and a blind mutant is one no run can test."
        );
        var tested = matrix
            .Mutants.Where(MutantKinds.IsTested)
            .Select(mutant => FolderOf(Reach.FileOf(mutant)))
            .ToHashSet(StringComparer.Ordinal);
        var projects = matrix
            .Mutants.Where(mutant => tested.Contains(FolderOf(Reach.FileOf(mutant))))
            .GroupBy(mutant => mutant.Project, StringComparer.Ordinal)
            .OrderBy(project => project.Key, StringComparer.Ordinal);
        foreach (var project in projects)
        {
            writer.WriteLine();
            writer.WriteLine($"## {project.Key}");
            writer.WriteLine();
            writer.WriteLine(
                "| File | Tested | Killed | Survived | No coverage | Timeout "
                    + "| Blind, does not compile | Blind, static |"
            );
            writer.WriteLine("|----|----|----|----|----|----|----|----|");
            var files = project
                .GroupBy(Reach.FileOf, StringComparer.Ordinal)
                .Select(file => (File: file.Key, Counts: Count(file)))
                .OrderByDescending(file => file.Counts[2] + file.Counts[3])
                .ThenBy(file => file.File, StringComparer.Ordinal);
            foreach (var (file, counts) in files)
            {
                writer.WriteLine($"| {file} | {string.Join(" | ", counts.Select(EvidenceWriter.Number))} |");
            }
        }
    }

    public static void WriteDatabaseOnly(
        TextWriter writer,
        IReadOnlyList<string> projects,
        IReadOnlyList<(string File, IReadOnlyList<(int From, int To)> Lines)> only
    )
    {
        writer.WriteLine("# Lines only the database projects reach");
        writer.WriteLine();
        writer.WriteLine(
            $"The lines of src/ that a test of {string.Join(", ", projects)} ran in this run and no test of another "
                + "project did."
        );
        writer.WriteLine();
        if (only.Count == 0)
        {
            writer.WriteLine("None.");
        }

        foreach (var (file, lines) in only)
        {
            writer.WriteLine($"- {file}: {EvidenceWriter.Ranges(lines)}");
        }
    }

    // Tested, killed, survived, not covered, timed out, uncompiled and static, in the order of the table.
    private static int[] Count(IEnumerable<MatrixMutant> mutants)
    {
        var counts = new int[7];
        foreach (var mutant in mutants)
        {
            if (MutantKinds.IsTested(mutant))
            {
                counts[0]++;
                counts[ColumnOf(mutant.Status)]++;
            }
            else if (mutant.Status == MutantStatus.CompileError)
            {
                counts[5]++;
            }
            else if (MutantKinds.IsBlind(mutant))
            {
                counts[6]++;
            }
        }

        return counts;
    }

    private static int ColumnOf(MutantStatus tested)
    {
        if (tested == MutantStatus.Killed)
        {
            return 1;
        }

        if (tested == MutantStatus.Survived)
        {
            return 2;
        }

        return tested == MutantStatus.NoCoverage ? 3 : 4;
    }

    private static string FolderOf(string file) => file[..file.LastIndexOf('/')];
}
