using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SqlSource.Repository.Tests.MutationReport;

// A mutant of a report that a test writes: its place in its file's source, by lines and columns from 1 as Stryker
// gives them, its status, and whether Stryker marked it static.  CoveredBy and KilledBy are the ids of the tests that
// reach it and that fail on it, t1 and t2, and t1, unless they are given, and Reason is its statusReason, which a
// report holds only for some mutants.
internal sealed record ReportMutant(
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string Status,
    bool Static = false,
    string Mutator = "Boolean mutation",
    string Replacement = "true",
    string[]? CoveredBy = null,
    string[]? KilledBy = null,
    string? Reason = null
);

internal sealed record ReportFile(string Path, string Source, params ReportMutant[] Mutants);

// A file of tests of a report that a test writes, each test an id and a full name.  The rows of a theory are tests
// of one name.
internal sealed record ReportTestFile(string Path, params (string Id, string Name)[] Tests);

// Writes a mutation report as Stryker writes one, for the tests of tools/MutationReport, with the lists of the tests
// that cover and kill each mutant, which the readers skip.
internal static class Reports
{
    private static readonly string[] CoveredBy = ["t1", "t2"];
    private static readonly string[] KilledBy = ["t1"];

    // The tests of a report that names none: the two that its mutants' lists hold.
    private static readonly ReportTestFile[] TestFiles = [new("/tests/ATests.cs", ("t1", "A.Test"), ("t2", "A.Other"))];

    // A report whose mutants are each at columns 1 to 5 of a line of a file.
    public static Stream Json(
        string projectRoot,
        params (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) => Json(projectRoot, Files(mutants));

    public static Stream Json(string projectRoot, params ReportFile[] files) => Json(projectRoot, files, TestFiles);

    public static Stream Json(string projectRoot, ReportFile[] files, ReportTestFile[] testFiles) =>
        new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(Report(projectRoot, files, testFiles)));

    public static void Write(
        string path,
        string projectRoot,
        params (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) => Write(path, projectRoot, Files(mutants));

    public static void Write(string path, string projectRoot, params ReportFile[] files) =>
        Write(path, projectRoot, files, TestFiles);

    public static void Write(string path, string projectRoot, ReportFile[] files, ReportTestFile[] testFiles) =>
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(Report(projectRoot, files, testFiles)));

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

    private static Dictionary<string, object> Report(
        string projectRoot,
        ReportFile[] files,
        ReportTestFile[] testFiles
    ) =>
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
            ["testFiles"] = testFiles.ToDictionary(
                file => file.Path,
                file =>
                    (object)
                        new
                        {
                            source = "class ATests { }",
                            tests = Array.ConvertAll(file.Tests, test => new { id = test.Id, name = test.Name }),
                        }
            ),
        };

    private static Dictionary<string, object> Mutant(ReportMutant mutant, ReportFile file)
    {
        var json = new Dictionary<string, object>
        {
            ["id"] = Array.IndexOf(file.Mutants, mutant).ToString(CultureInfo.InvariantCulture),
            ["mutatorName"] = mutant.Mutator,
            ["replacement"] = mutant.Replacement,
            ["location"] = new
            {
                start = new { line = mutant.StartLine, column = mutant.StartColumn },
                end = new { line = mutant.EndLine, column = mutant.EndColumn },
            },
            ["status"] = mutant.Status,
            ["static"] = mutant.Static,
            ["coveredBy"] = mutant.CoveredBy ?? CoveredBy,
            ["killedBy"] = mutant.KilledBy ?? KilledBy,
        };
        if (mutant.Reason is { } reason)
        {
            json["statusReason"] = reason;
        }

        return json;
    }
}
