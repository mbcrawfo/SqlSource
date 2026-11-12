using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// The command line of the program, which tools/mutation-test.sh runs.
internal static class Cli
{
    // Every command exits with 2 when its arguments are wrong or a report cannot be read.
    public static async Task<int> RunAsync(string[] arguments, TextWriter output, TextWriter error)
    {
        switch (arguments)
        {
            case ["compare", var before, var after]:
                return await CompareAsync(before, after, output, error).ConfigureAwait(false);
            case ["static", var report]:
                return await StaticAsync(report, output, error).ConfigureAwait(false);
            default:
                await error.WriteLineAsync("Usage: compare <before.json> <after.json>").ConfigureAwait(false);
                await error.WriteLineAsync("       static <report.json>").ConfigureAwait(false);
                return 2;
        }
    }

    // Prints the exclusion of each file's static mutants.  0: the run left every static mutant out.  1: it tested
    // some, so its kills cannot be trusted.
    private static async Task<int> StaticAsync(string path, TextWriter output, TextWriter error)
    {
        StaticMutants mutants;
        try
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                mutants = await StaticMutantReader.ReadAsync(stream, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            await error.WriteLineAsync($"Cannot read {path}: {exception.Message}").ConfigureAwait(false);
            return 2;
        }

        foreach (var exclusion in mutants.Exclusions)
        {
            await output.WriteLineAsync(exclusion).ConfigureAwait(false);
        }

        if (mutants.Tested == 0)
        {
            return 0;
        }

        var count = string.Create(CultureInfo.InvariantCulture, $"{mutants.Tested} static mutants were tested");
        await error.WriteLineAsync($"{count}, so the report's kills cannot be trusted.").ConfigureAwait(false);
        return 1;
    }

    // 0: every mutant detected before is detected after.  1: one is lost or not comparable.
    private static async Task<int> CompareAsync(
        string beforePath,
        string afterPath,
        TextWriter output,
        TextWriter error
    )
    {
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
        catch (Exception exception) when (IsUnreadable(exception))
        {
            await error.WriteLineAsync($"Cannot read {path}: {exception.Message}").ConfigureAwait(false);
            return null;
        }
    }

    private static bool IsUnreadable(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException;
}
