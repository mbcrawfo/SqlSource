using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// The static mutants of a report.  Exclusions holds one pattern for each file that has any, in the form Stryker's
// --mutate takes, "!<path from the project root>{start..end}{start..end}", and Tested counts the static mutants that
// the run did not leave out.
internal sealed record StaticMutants(IReadOnlyList<string> Exclusions, int Tested);

// Finds the mutants that Stryker marks static, those of a static initialiser or constructor.  Its runner for the
// Microsoft Testing Platform tests a static mutant in a process it then goes on using, so the mutant's state stays
// for the mutants tested after it and tests that notice it are counted as killing them.  A run therefore keeps the
// static mutants out by their spans, which are counted in characters from the start of the file's source.
internal static partial class StaticMutantReader
{
    // Throws InvalidDataException for text that is no report, a status it does not know or a place outside the source.
    public static async ValueTask<StaticMutants> ReadAsync(Stream json, CancellationToken cancellation)
    {
        ReportJson? report;
        try
        {
            report = await JsonSerializer
                .DeserializeAsync(json, StaticJsonContext.Default.ReportJson, cancellation)
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

        var tested = 0;
        var exclusions = new List<string>();
        foreach (var (path, file) in files)
        {
            var name = ReportReader.FromRoot(root, path);
            var spans = new List<(int Start, int End)>();
            int[]? lineStarts = null;
            foreach (var mutant in file.Mutants ?? [])
            {
                if (!mutant.Static)
                {
                    continue;
                }

                if (mutant.Location is not { Start: { } start, End: { } end } || mutant.Status is not { } status)
                {
                    throw new InvalidDataException($"A mutant of {name} has no location or status.");
                }

                if (ReportReader.StatusOf(status, name) is not (MutantStatus.Ignored or MutantStatus.CompileError))
                {
                    tested++;
                }

                lineStarts ??= LineStarts(file.Source ?? "");
                var length = (file.Source ?? "").Length;
                spans.Add((Offset(lineStarts, length, start, name), Offset(lineStarts, length, end, name)));
            }

            if (spans.Count > 0)
            {
                exclusions.Add(Exclusion(name, spans));
            }
        }

        exclusions.Sort(StringComparer.Ordinal);
        return new StaticMutants(exclusions, tested);
    }

    // The offset of each line's first character.  A line ends at its line feed, so a carriage return before it is a
    // character of the line, as it is in the file.
    private static int[] LineStarts(string source)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return [.. starts];
    }

    private static int Offset(int[] lineStarts, int length, ReportReader.PositionJson position, string file)
    {
        var (line, column) = (position.Line, position.Column);
        var offset = line >= 1 && line <= lineStarts.Length && column >= 1 ? lineStarts[line - 1] + column - 1 : -1;
        return offset >= 0 && offset <= length
            ? offset
            : throw new InvalidDataException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A mutant of {file} is at line {line}, column {column}, outside its source."
                )
            );
    }

    // One pattern for a file, its spans in order, with those that overlap or touch as one.
    private static string Exclusion(string file, List<(int Start, int End)> spans)
    {
        spans.Sort();
        var pattern = new StringBuilder("!").Append(file);
        var (from, to) = spans[0];
        foreach (var (start, end) in spans)
        {
            if (start <= to)
            {
                to = Math.Max(to, end);
                continue;
            }

            AppendSpan(pattern, from, to);
            (from, to) = (start, end);
        }

        AppendSpan(pattern, from, to);
        return pattern.ToString();
    }

    private static void AppendSpan(StringBuilder pattern, int from, int to) =>
        pattern.Append(CultureInfo.InvariantCulture, $"{{{from}..{to}}}");

    private sealed record ReportJson(string? ProjectRoot, Dictionary<string, FileJson>? Files);

    private sealed record FileJson(string? Source, List<MutantJson>? Mutants);

    private sealed record MutantJson(ReportReader.LocationJson? Location, string? Status, bool Static);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(ReportJson))]
    private sealed partial class StaticJsonContext : JsonSerializerContext;
}
