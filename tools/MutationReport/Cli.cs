using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// The command line of the program, which tools/mutation-test.sh runs: compare <before.json> <after.json>.
internal static class Cli
{
    private const string Usage = "Usage: compare <before.json> <after.json>";

    // 0: every mutant detected before is detected after.  1: one is lost or not comparable.
    // 2: the arguments are wrong, or a report cannot be read.
    public static async Task<int> RunAsync(string[] arguments, TextWriter output, TextWriter error)
    {
        if (arguments is not ["compare", var beforePath, var afterPath])
        {
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }

        var before = await ReadAsync(beforePath, error).ConfigureAwait(false);
        if (before is null)
        {
            return 2;
        }

        var after = await ReadAsync(afterPath, error).ConfigureAwait(false);
        if (after is null)
        {
            return 2;
        }

        var comparison = ReportComparison.Compare(before, after);
        foreach (var change in comparison.Lost)
        {
            await output.WriteLineAsync(Line("LOST", change)).ConfigureAwait(false);
        }

        foreach (var change in comparison.NotComparable)
        {
            await output.WriteLineAsync(Line("NOT COMPARABLE", change)).ConfigureAwait(false);
        }

        var (detected, lost, apart) = (comparison.Detected, comparison.Lost.Count, comparison.NotComparable.Count);
        var counts = string.Create(
            CultureInfo.InvariantCulture,
            $"{detected} detected before, {detected - lost - apart} still detected, {lost} lost, "
        );
        await output
            .WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{counts}{apart} not comparable"))
            .ConfigureAwait(false);
        return comparison.Holds ? 0 : 1;
    }

    private static string Line(string kind, Change change)
    {
        var key = change.Key;
        var now = change.After?.ToString() ?? "absent";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{kind} {key.File}:{key.StartLine}:{key.StartColumn} {key.Mutator} -> {key.Replacement} "
                + $"(was {change.Before}, now {now})"
        );
    }

    // The mutants of a report, or null after saying on the error stream why the file cannot be read.
    private static async Task<IReadOnlyList<Mutant>?> ReadAsync(string path, TextWriter error)
    {
        try
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                return await ReportReader.ReadAsync(stream, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await error.WriteLineAsync($"Cannot read {path}: {exception.Message}").ConfigureAwait(false);
            return null;
        }
    }
}
