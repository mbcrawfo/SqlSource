using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// Reads the mutants of a report in the mutation-testing report schema, which Stryker's json reporter writes.  A report
// of a whole project is hundreds of megabytes, most of it each file's source and each mutant's lists of the tests that
// cover and kill it: the types below declare none of those, so the stream is read without holding them.
internal static partial class ReportReader
{
    // Throws InvalidDataException for text that is no report or a status it does not know.
    public static async ValueTask<IReadOnlyList<Mutant>> ReadAsync(Stream json, CancellationToken cancellation)
    {
        ReportJson? report;
        try
        {
            report = await JsonSerializer
                .DeserializeAsync(json, ReportJsonContext.Default.ReportJson, cancellation)
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

        var mutants = new List<Mutant>();
        var seen = new Dictionary<MutantKey, int>();
        foreach (var (path, file) in files)
        {
            var name = FromRoot(root, path);
            foreach (var mutant in file.Mutants ?? [])
            {
                mutants.Add(Read(name, mutant, seen));
            }
        }

        return mutants;
    }

    private static Mutant Read(string file, MutantJson mutant, Dictionary<MutantKey, int> seen)
    {
        if (
            mutant.MutatorName is not { } mutator
            || mutant.Location is not { Start: { } start, End: { } end }
            || mutant.Status is not { } status
        )
        {
            throw new InvalidDataException($"A mutant of {file} has no mutatorName, location or status.");
        }

        var first = new MutantKey(
            file,
            start.Line,
            start.Column,
            end.Line,
            end.Column,
            mutator,
            mutant.Replacement ?? "",
            0
        );
        var ordinal = seen.GetValueOrDefault(first);
        seen[first] = ordinal + 1;
        return new Mutant(first with { Ordinal = ordinal }, StatusOf(status, file));
    }

    internal static MutantStatus StatusOf(string status, string file) =>
        status switch
        {
            "Killed" => MutantStatus.Killed,
            "Timeout" => MutantStatus.Timeout,
            "Survived" => MutantStatus.Survived,
            "NoCoverage" => MutantStatus.NoCoverage,
            "Ignored" => MutantStatus.Ignored,
            "CompileError" => MutantStatus.CompileError,
            "RuntimeError" => MutantStatus.RuntimeError,
            "Pending" => MutantStatus.Pending,
            _ => throw new InvalidDataException($"A mutant of {file} has the status '{status}', which is not known."),
        };

    // The path of a file from the project root, with '/'.  Both are compared as text, whatever the system that reads
    // them, so that a report made on Windows reads the same anywhere.
    internal static string FromRoot(string root, string path)
    {
        var folder = root.Replace('\\', '/').TrimEnd('/') + "/";
        var file = path.Replace('\\', '/');
        return file.StartsWith(folder, StringComparison.Ordinal) ? file[folder.Length..] : file;
    }

    private sealed record ReportJson(string? ProjectRoot, Dictionary<string, FileJson>? Files);

    private sealed record FileJson(List<MutantJson>? Mutants);

    private sealed record MutantJson(string? MutatorName, string? Replacement, LocationJson? Location, string? Status);

    internal sealed record LocationJson(PositionJson? Start, PositionJson? End);

    internal sealed record PositionJson(int Line, int Column);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(ReportJson))]
    private sealed partial class ReportJsonContext : JsonSerializerContext;
}
