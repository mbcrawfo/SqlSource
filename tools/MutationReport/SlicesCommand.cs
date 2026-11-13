using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SqlSource.MutationReport;

// The slices command: cuts the .cs files of some test folders into the slices of a test review, and writes each
// slice's files, one to a line, to <name>.txt of the output folder, and a line "<name> <lines> <files>" for each to
// its index.txt.  Each folder is cut by itself, so no slice holds files of two.
internal static partial class SlicesCommand
{
    // A partial class that a file declares itself, and not one in the text of a test's input, which is indented.
    [GeneratedRegex(@"^(?:public|internal)(?: (?:sealed|static|abstract))* partial class (\w+)")]
    private static partial Regex PartialClass();

    // 0: the slices are written.  2: a folder does not exist or is inside another, or the output folder holds files.
    public static async Task<int> RunAsync(
        string root,
        string outputFolder,
        int limit,
        string? exclusionFile,
        IReadOnlyList<string> folders,
        TextWriter output,
        TextWriter error
    )
    {
        // Each folder as its path from the root, however it was written: "./tests/P", "tests/P/" and "tests/Q/../P"
        // are tests/P, which is what a slice's files and its name are cut from.
        var whole = Path.GetFullPath(root);
        var names = new List<string>();
        foreach (var folder in folders)
        {
            var name = Path.GetRelativePath(whole, Path.GetFullPath(Path.Combine(whole, folder))).Replace('\\', '/');
            if (
                name == "."
                || name == ".."
                || name.StartsWith("../", StringComparison.Ordinal)
                || Path.IsPathRooted(name)
            )
            {
                await error
                    .WriteLineAsync($"The folder {folder} is not a folder below the repository's root.")
                    .ConfigureAwait(false);
                return 2;
            }

            names.Add(name);
        }

        foreach (var name in names)
        {
            if (!Directory.Exists(Path.Combine(whole, name)))
            {
                await error.WriteLineAsync($"The folder {name} does not exist.").ConfigureAwait(false);
                return 2;
            }

            if (names.Exists(other => name.StartsWith(other + "/", StringComparison.Ordinal)))
            {
                await error
                    .WriteLineAsync($"The folder {name} is inside another of the folders.")
                    .ConfigureAwait(false);
                return 2;
            }
        }

        if (Directory.Exists(outputFolder) && Directory.EnumerateFileSystemEntries(outputFolder).Any())
        {
            await error.WriteLineAsync($"The folder {outputFolder} is not empty.").ConfigureAwait(false);
            return 2;
        }

        string[] excluded;
        try
        {
            excluded = exclusionFile is null ? [] : await ListFile.ReadAsync(exclusionFile).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await error.WriteLineAsync($"Cannot read {exclusionFile}: {exception.Message}").ConfigureAwait(false);
            return 2;
        }

        var slices = new List<Slice>();
        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            slices.AddRange(TestSlices.Cut(name, FilesOf(whole, name, excluded), limit));
        }

        _ = Directory.CreateDirectory(outputFolder);
        foreach (var slice in slices)
        {
            await File.WriteAllLinesAsync(Path.Combine(outputFolder, slice.Name + ".txt"), slice.Files)
                .ConfigureAwait(false);
        }

        await File.WriteAllLinesAsync(
                Path.Combine(outputFolder, "index.txt"),
                slices.Select(slice =>
                    string.Create(CultureInfo.InvariantCulture, $"{slice.Name} {slice.Lines} {slice.Files.Count}")
                )
            )
            .ConfigureAwait(false);
        var (files, lines) = (slices.Sum(slice => slice.Files.Count), slices.Sum(slice => slice.Lines));
        await output
            .WriteLineAsync(
                string.Create(CultureInfo.InvariantCulture, $"{slices.Count} slices of {files} files and {lines} lines")
            )
            .ConfigureAwait(false);
        return 0;
    }

    // The .cs files under a folder, but those of a build's output and those under an excluded path.
    private static List<SliceFile> FilesOf(string root, string folder, string[] excluded)
    {
        var files = new List<SliceFile>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (
                path.Contains("/bin/", StringComparison.Ordinal)
                || path.Contains("/obj/", StringComparison.Ordinal)
                || excluded.Any(prefix => path.StartsWith(prefix + "/", StringComparison.Ordinal))
            )
            {
                continue;
            }

            var lines = 0;
            string? partialClass = null;
            foreach (var line in File.ReadLines(file))
            {
                lines++;
                if (partialClass is null && PartialClass().Match(line) is { Success: true } match)
                {
                    partialClass = match.Groups[1].Value;
                }
            }

            files.Add(new SliceFile(path, lines, partialClass));
        }

        return files;
    }
}
