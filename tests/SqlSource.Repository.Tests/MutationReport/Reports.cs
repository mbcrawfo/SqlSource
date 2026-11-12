using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SqlSource.Repository.Tests.MutationReport;

// Writes a mutation report as Stryker writes one, for the tests of tools/MutationReport: each mutant at columns 1 to 5
// of its line, with the source of its file and the lists of the tests that cover and kill it, which the reader skips.
internal static class Reports
{
    private static readonly string[] CoveredBy = ["t1", "t2"];
    private static readonly string[] KilledBy = ["t1"];

    public static Stream Json(
        string projectRoot,
        params (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) => new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(Report(projectRoot, mutants)));

    public static void Write(
        string path,
        string projectRoot,
        params (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(Report(projectRoot, mutants)));

    private static Dictionary<string, object> Report(
        string projectRoot,
        (string File, int Line, string Mutator, string Replacement, string Status)[] mutants
    ) =>
        new()
        {
            ["schemaVersion"] = "2",
            ["thresholds"] = new { high = 80, low = 60 },
            ["projectRoot"] = projectRoot,
            ["files"] = mutants
                .GroupBy(mutant => mutant.File)
                .ToDictionary(
                    file => file.Key,
                    file =>
                        (object)
                            new
                            {
                                language = "cs",
                                source = "class A { }",
                                mutants = file.Select(
                                        (mutant, index) =>
                                            new
                                            {
                                                id = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                mutatorName = mutant.Mutator,
                                                replacement = mutant.Replacement,
                                                location = new
                                                {
                                                    start = new { line = mutant.Line, column = 1 },
                                                    end = new { line = mutant.Line, column = 5 },
                                                },
                                                status = mutant.Status,
                                                @static = false,
                                                coveredBy = CoveredBy,
                                                killedBy = KilledBy,
                                            }
                                    )
                                    .ToArray(),
                            }
                ),
            ["testFiles"] = new Dictionary<string, object>
            {
                ["/tests/ATests.cs"] = new { tests = new[] { new { id = "t1", name = "A.Test" } } },
            },
        };
}
