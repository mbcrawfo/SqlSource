using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// Reads the kill matrix of a report: each mutant with the tests that reach it and the tests that fail on it.  Those
// lists are most of a report, hundreds of megabytes of 64-character ids for a scope with many tests, so each id is
// turned into a number as it is read and no list of strings is held.
internal static partial class KillMatrixReader
{
    // The tests are those the run ran, which the report's testFiles list.  Throws InvalidDataException for text that
    // is no report, a status it does not know or a test id that testFiles does not hold.
    public static async ValueTask<KillMatrix> ReadAsync(Stream json, CancellationToken cancellation)
    {
        var ids = new TestIdsConverter();
        var context = new MatrixJsonContext(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { ids } }
        );
        ReportJson? report;
        try
        {
            report = await JsonSerializer
                .DeserializeAsync(json, context.ReportJson, cancellation)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"It is no mutation report: {exception.Message}", exception);
        }

        if (report?.ProjectRoot is not { } root || report.Files is not { } files)
        {
            throw new InvalidDataException("It is no mutation report: it has no projectRoot or no files.");
        }

        var (repository, project) = Split(root);
        var tests = new ReportTest?[ids.Count];
        var later = new List<ReportTest>();
        foreach (var (path, file) in report.TestFiles ?? [])
        {
            var name = ReportReader.FromRoot(repository, path);
            foreach (var test in file.Tests ?? [])
            {
                if (test.Id is not { } id || test.Name is not { } full)
                {
                    throw new InvalidDataException($"A test of {name} has no id or name.");
                }

                // A test that reaches no mutant has no number yet, and takes one after those that do.
                var read = new ReportTest(id, name, full);
                if (ids.IndexOf(id) is { } index)
                {
                    tests[index] = read;
                }
                else
                {
                    later.Add(read);
                }
            }
        }

        var all = new List<ReportTest>(tests.Length + later.Count);
        for (var index = 0; index < tests.Length; index++)
        {
            all.Add(
                tests[index]
                    ?? throw new InvalidDataException(
                        $"A mutant names the test '{ids.IdOf(index)}', which the report's testFiles do not hold."
                    )
            );
        }

        all.AddRange(later);

        var mutants = new List<MatrixMutant>();
        var seen = new Dictionary<MutantKey, int>();
        foreach (var (path, file) in files)
        {
            var name = ReportReader.FromRoot(root, path);
            foreach (var mutant in file.Mutants ?? [])
            {
                var key = ReportReader.KeyOf(name, mutant.MutatorName, mutant.Replacement, mutant.Location, seen);
                if (mutant.Status is not { } status)
                {
                    throw new InvalidDataException($"A mutant of {name} has no mutatorName, location or status.");
                }

                mutants.Add(
                    new MatrixMutant(
                        project,
                        key,
                        ReportReader.StatusOf(status, name),
                        mutant.Static,
                        mutant.StatusReason ?? "",
                        mutant.CoveredBy?.Tests ?? [],
                        mutant.KilledBy?.Tests ?? []
                    )
                );
            }
        }

        return new KillMatrix(all, mutants);
    }

    // The repository's root and the project's folder from it, "src/SqlSource": a mutated project is two folders
    // below the root.  A root with fewer folders is its own project, and its report's paths stay as they are.
    private static (string Repository, string Project) Split(string projectRoot)
    {
        var root = projectRoot.Replace('\\', '/').TrimEnd('/');
        var project = root.LastIndexOf('/');
        var parent = project > 0 ? root.LastIndexOf('/', project - 1) : -1;
        return parent < 0 ? ("", root) : (root[..parent], root[(parent + 1)..]);
    }

    // The ids of a mutant's coveredBy or killedBy, as numbers.
    internal sealed record TestIds(int[] Tests);

    // Numbers the test ids in the order a report first names them.  One converter reads one report.
    private sealed class TestIdsConverter : JsonConverter<TestIds>
    {
        private readonly Dictionary<string, int> _indexes = [];
        private readonly List<string> _ids = [];

        public int Count => _ids.Count;

        public string IdOf(int index) => _ids[index];

        public int? IndexOf(string id) => _indexes.TryGetValue(id, out var index) ? index : null;

        public override TestIds Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("A list of test ids is no array.");
            }

            var lookup = _indexes.GetAlternateLookup<ReadOnlySpan<char>>();
            Span<char> buffer = stackalloc char[128];
            var tests = new List<int>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("A test id is no string.");
                }

                // An id is copied to the stack to be looked up, so that only the first sight of it makes a string.
                var length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
                if (length > buffer.Length)
                {
                    tests.Add(Number(reader.GetString()!));
                    continue;
                }

                var id = buffer[..reader.CopyString(buffer)];
                tests.Add(lookup.TryGetValue(id, out var index) ? index : Number(id.ToString()));
            }

            return new TestIds([.. tests]);
        }

        public override void Write(Utf8JsonWriter writer, TestIds value, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        private int Number(string id)
        {
            if (!_indexes.TryGetValue(id, out var index))
            {
                index = _ids.Count;
                _indexes.Add(id, index);
                _ids.Add(id);
            }

            return index;
        }
    }

    private sealed record ReportJson(
        string? ProjectRoot,
        Dictionary<string, FileJson>? Files,
        Dictionary<string, TestFileJson>? TestFiles
    );

    private sealed record FileJson(List<MutantJson>? Mutants);

    private sealed record MutantJson(
        string? MutatorName,
        string? Replacement,
        ReportReader.LocationJson? Location,
        string? Status,
        bool Static,
        string? StatusReason,
        TestIds? CoveredBy,
        TestIds? KilledBy
    );

    private sealed record TestFileJson(List<TestJson>? Tests);

    private sealed record TestJson(string? Id, string? Name);

    [JsonSerializable(typeof(ReportJson))]
    private sealed partial class MatrixJsonContext : JsonSerializerContext;
}
