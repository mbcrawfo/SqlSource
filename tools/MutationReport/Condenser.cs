using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// The condense command: turns what a run of the test review measured into one evidence file for each slice, small
// enough for a reviewer to read whole.  It reads, in the run folder,
//   slices/index.txt and slices/<name>.txt                the slices, as the slices command writes them;
//   mutation/<scope>/reports/mutation-report.json         the report of each mutation scope, and the file "failed"
//                                                          beside reports/ when the scope's run failed;
//   tests/<project>/results.xunit.xml                     each test project's tests with their times;
//   tests/<project>/coverage.cobertura.xml                each test project's line coverage;
// where <project> is the project's folder from the repository's tests/ folder.  It writes evidence/<slice>.md,
// evidence/gaps.md when a scope was run, and evidence/database-only-lines.md when a database project was.
internal static class Condenser
{
    // The folder of the projects that run against a real database, which take no part in a mutation run.
    public const string DatabaseProjects = "tests/DatabaseTests/";

    public const string ResultsFile = "results.xunit.xml";
    public const string CoverageFile = "coverage.cobertura.xml";

    // broad holds the prefixes of the full names of the broad tests.  0: the evidence is written, and output names
    // each file.  2: the run folder cannot be read, a scope failed or tested a static mutant, or two scopes tested
    // one mutant; error says which.
    public static async Task<int> RunAsync(
        string repositoryRoot,
        string runFolder,
        IReadOnlyList<string> broad,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellation
    )
    {
        // The reports name their files in full, so a root given from the working folder is made whole to match them.
        var root = Path.GetFullPath(repositoryRoot);
        Measurements measured;
        try
        {
            measured = await Measurements.ReadAsync(root, runFolder, cancellation).ConfigureAwait(false);
        }
        catch (InvalidDataException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }

        var evidence = Path.Combine(runFolder, "evidence");
        _ = Directory.CreateDirectory(evidence);
        foreach (var slice in SliceEvidences.Of(measured, name => broad.Any(name.StartsWith)))
        {
            var writer = new StreamWriter(Path.Combine(evidence, slice.Name + ".md")) { NewLine = "\n" };
            await using (writer.ConfigureAwait(false))
            {
                EvidenceWriter.Write(writer, slice);
            }

            await output.WriteLineAsync($"evidence/{slice.Name}.md").ConfigureAwait(false);
        }

        if (measured.Scopes.Count > 0)
        {
            var writer = new StreamWriter(Path.Combine(evidence, "gaps.md")) { NewLine = "\n" };
            await using (writer.ConfigureAwait(false))
            {
                GapsWriter.Write(writer, measured.Matrix);
            }

            await output.WriteLineAsync("evidence/gaps.md").ConfigureAwait(false);
        }

        var database = measured.Coverage.Where(project => IsDatabase(project.Key)).ToList();
        if (database.Count > 0)
        {
            var only = CoverageReader.Only(
                [.. database.Select(project => project.Value)],
                [.. measured.Coverage.Where(project => !IsDatabase(project.Key)).Select(project => project.Value)]
            );
            var writer = new StreamWriter(Path.Combine(evidence, "database-only-lines.md")) { NewLine = "\n" };
            await using (writer.ConfigureAwait(false))
            {
                GapsWriter.WriteDatabaseOnly(writer, [.. database.Select(project => project.Key).Order()], only);
            }

            await output.WriteLineAsync("evidence/database-only-lines.md").ConfigureAwait(false);
        }

        return 0;
    }

    private static bool IsDatabase(string project) => project.StartsWith(DatabaseProjects, StringComparison.Ordinal);
}

// A mutation scope of a run: its folder's name and the matrix of its report.
internal sealed record MeasuredScope(string Name, KillMatrix Matrix);

// A slice of a run: its name and its files, each with its lines.
internal sealed record MeasuredSlice(string Name, IReadOnlyList<(string Path, int Lines)> Files);

// What a run measured, read from its folder.  Matrix is the scopes' matrices as one, and Times and Coverage are by
// test project, each its folder from the repository root.  ReadAsync throws InvalidDataException, with the words
// the command prints, for a run folder that cannot be condensed.
internal sealed record Measurements(
    string RepositoryRoot,
    IReadOnlyList<MeasuredSlice> Slices,
    IReadOnlyList<MeasuredScope> Scopes,
    KillMatrix Matrix,
    IReadOnlyDictionary<string, IReadOnlyList<TestTime>> Times,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlySet<int>>> Coverage
)
{
    public static async Task<Measurements> ReadAsync(
        string repositoryRoot,
        string runFolder,
        CancellationToken cancellation
    )
    {
        var slices = await Reading("the slices", () => SlicesAsync(repositoryRoot, runFolder, cancellation))
            .ConfigureAwait(false);

        var scopes = new List<MeasuredScope>();
        var mutation = Path.Combine(runFolder, "mutation");
        if (Directory.Exists(mutation))
        {
            foreach (var folder in Directory.GetDirectories(mutation).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(folder);
                if (File.Exists(Path.Combine(folder, "failed")))
                {
                    throw new InvalidDataException(
                        $"The scope {name} failed, so its report is not evidence: run it again or remove its folder."
                    );
                }

                var report = Path.Combine(folder, "reports", "mutation-report.json");
                var (matrix, statics) = await Reading(
                        $"the report of the scope {name}",
                        () => ScopeAsync(report, cancellation)
                    )
                    .ConfigureAwait(false);
                if (statics > 0)
                {
                    var count = string.Create(CultureInfo.InvariantCulture, $"{statics} static mutants");
                    throw new InvalidDataException(
                        $"The scope {name} tested {count}, so its kills cannot be trusted: run it again."
                    );
                }

                scopes.Add(new MeasuredScope(name, matrix));
            }
        }

        var merged = KillMatrix.Merge([.. scopes.Select(scope => scope.Matrix)]);

        var times = new Dictionary<string, IReadOnlyList<TestTime>>(StringComparer.Ordinal);
        var coverage = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlySet<int>>>(StringComparer.Ordinal);
        var tests = Path.Combine(runFolder, "tests");
        if (Directory.Exists(tests))
        {
            foreach (
                var file in Directory
                    .EnumerateFiles(tests, "*.xml", SearchOption.AllDirectories)
                    .Order(StringComparer.Ordinal)
            )
            {
                var project = "tests/" + Path.GetRelativePath(tests, Path.GetDirectoryName(file)!).Replace('\\', '/');
                var name = Path.GetFileName(file);
                if (name == Condenser.ResultsFile)
                {
                    times[project] = await Reading(
                            file,
                            () => Task.FromResult(Open(file, stream => TestTimeReader.Read(stream, repositoryRoot)))
                        )
                        .ConfigureAwait(false);
                }
                else if (name == Condenser.CoverageFile)
                {
                    coverage[project] = await Reading(
                            file,
                            () => Task.FromResult(Open(file, stream => CoverageReader.Read(stream, repositoryRoot)))
                        )
                        .ConfigureAwait(false);
                }
            }
        }

        return new Measurements(repositoryRoot, slices, scopes, merged, times, coverage);
    }

    private static T Open<T>(string path, Func<Stream, T> read)
    {
        using var stream = File.OpenRead(path);
        return read(stream);
    }

    private static async Task<IReadOnlyList<MeasuredSlice>> SlicesAsync(
        string repositoryRoot,
        string runFolder,
        CancellationToken cancellation
    )
    {
        var folder = Path.Combine(runFolder, "slices");
        var slices = new List<MeasuredSlice>();
        foreach (
            var line in await File.ReadAllLinesAsync(Path.Combine(folder, "index.txt"), cancellation)
                .ConfigureAwait(false)
        )
        {
            var name = line.Split(' ')[0];
            var files = await File.ReadAllLinesAsync(Path.Combine(folder, name + ".txt"), cancellation)
                .ConfigureAwait(false);
            slices.Add(
                new MeasuredSlice(
                    name,
                    [.. files.Select(file => (file, File.ReadLines(Path.Combine(repositoryRoot, file)).Count()))]
                )
            );
        }

        return slices;
    }

    // A scope's matrix, and how many static mutants its run tested, which tools/mutation-test.sh checks too: the
    // kills of a run that tested one are not real.
    private static async Task<(KillMatrix Matrix, int StaticTested)> ScopeAsync(
        string report,
        CancellationToken cancellation
    )
    {
        var stream = File.OpenRead(report);
        await using (stream.ConfigureAwait(false))
        {
            var statics = await StaticMutantReader.ReadAsync(stream, cancellation).ConfigureAwait(false);
            stream.Position = 0;
            return (await KillMatrixReader.ReadAsync(stream, cancellation).ConfigureAwait(false), statics.Tested);
        }
    }

    private static async Task<T> Reading<T>(string what, Func<Task<T>> read)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new InvalidDataException($"Cannot read {what}: {exception.Message}", exception);
        }
    }
}
