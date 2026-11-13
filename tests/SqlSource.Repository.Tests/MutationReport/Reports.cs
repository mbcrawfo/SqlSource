using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SqlSource.Repository.Tests.MutationReport;

// A mutant of a report that a test writes: its place in its file's source, by lines and columns from 1 as Stryker
// gives them, its status, and whether Stryker marked it static.
internal sealed record ReportMutant(
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string Status,
    bool Static = false,
    string Mutator = "Boolean mutation",
    string Replacement = "true"
);

internal sealed record ReportFile(string Path, string Source, params ReportMutant[] Mutants);

// Writes a mutation report as Stryker writes one, for the tests of tools/MutationReport, with the lists of the tests
// that cover and kill each mutant, which the readers skip.
internal static class Reports
{
    private static readonly string[] CoveredBy = ["t1", "t2"];
    private static readonly string[] KilledBy = ["t1"];

    // A report whose mutants are each at columns 1 to 5 of a line of a file.
    public static Stream Json(
        string projectRoot,
        params (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) => Json(projectRoot, Files(mutants));

    public static Stream Json(string projectRoot, params ReportFile[] files) =>
        new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(Report(projectRoot, files)));

    public static void Write(
        string path,
        string projectRoot,
        params (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) => Write(path, projectRoot, Files(mutants));

    public static void Write(string path, string projectRoot, params ReportFile[] files) =>
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(Report(projectRoot, files)));

    private static ReportFile[] Files(
        (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) =>
        [
            .. mutants
                .GroupBy(mutant => mutant.File)
                .Select(file => new ReportFile(
                    file.Key,
                    "class A { }",
                    [
                        .. file.Select(mutant => new ReportMutant(
                            mutant.Line,
                            1,
                            mutant.Line,
                            5,
                            mutant.Status,
                            Mutator: mutant.Mutator,
                            Replacement: mutant.Replacement
                        )),
                    ]
                )),
        ];

    private static Dictionary<string, object> Report(string projectRoot, ReportFile[] files) =>
        new()
        {
            ["schemaVersion"] = "2",
            ["thresholds"] = new { high = 80, low = 60 },
            ["projectRoot"] = projectRoot,
            ["files"] = files.ToDictionary(
                file => file.Path,
                file =>
                    (object)
                        new
                        {
                            language = "cs",
                            source = file.Source,
                            mutants = Array.ConvertAll(file.Mutants, mutant => Mutant(mutant, file)),
                        }
            ),
            ["testFiles"] = new Dictionary<string, object>
            {
                ["/tests/ATests.cs"] = new { tests = new[] { new { id = "t1", name = "A.Test" } } },
            },
        };

    private static object Mutant(ReportMutant mutant, ReportFile file) =>
        new
        {
            id = Array.IndexOf(file.Mutants, mutant).ToString(CultureInfo.InvariantCulture),
            mutatorName = mutant.Mutator,
            replacement = mutant.Replacement,
            location = new
            {
                start = new { line = mutant.StartLine, column = mutant.StartColumn },
                end = new { line = mutant.EndLine, column = mutant.EndColumn },
            },
            status = mutant.Status,
            @static = mutant.Static,
            coveredBy = CoveredBy,
            killedBy = KilledBy,
        };
}
