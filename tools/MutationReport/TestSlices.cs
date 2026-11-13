using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.MutationReport;

// A file of a test folder: its path from the repository root, with '/', its lines, and the partial class it declares
// at the start of a line, if it declares one.
internal sealed record SliceFile(string Path, int Lines, string? PartialClass);

// A part of the tests that one reviewer of the test review reads whole.  Files are in path order.
internal sealed record Slice(string Name, int Lines, IReadOnlyList<string> Files);

// Cuts a test folder into slices of at most a limit of lines, so that every file is in exactly one, no file is
// split and the files of one partial class stay together.  Slices follow folders:
//   - a folder whose files, at any depth, come to at most the limit is one block;
//   - a larger folder is its own files and then its sub-folders in order, each cut by this rule, and own files over
//     the limit are packed in path order into parts of at most the limit, a file or a partial class that is over
//     the limit alone being a part;
//   - the neighbouring blocks and parts of one folder are joined, in order, while they stay at or under the limit.
// A slice is named for the deepest folder that holds all its files, without the leading "tests/" and with '.' for
// '/', and numbered among the slices of that name, from 1: "SqlSource.Tests.Parsing-2".
internal static class TestSlices
{
    public static IReadOnlyList<Slice> Cut(string folder, IReadOnlyList<SliceFile> files, int limit)
    {
        var numbers = new Dictionary<string, int>();
        var slices = new List<Slice>();
        foreach (var block in CutFolder(folder.TrimEnd('/'), [.. files], limit))
        {
            var paths = block.Select(file => file.Path).Order(StringComparer.Ordinal).ToArray();
            var name = NameOf(paths);
            var number = numbers.GetValueOrDefault(name) + 1;
            numbers[name] = number;
            slices.Add(new Slice($"{name}-{number}", block.Sum(file => file.Lines), paths));
        }

        return slices;
    }

    private static List<List<SliceFile>> CutFolder(string folder, List<SliceFile> files, int limit)
    {
        if (files.Count == 0)
        {
            return [];
        }

        if (files.Sum(file => file.Lines) <= limit)
        {
            return [files];
        }

        // The blocks that may still be joined with the next: the parts of the folder's own files and each
        // sub-folder that fits.  A sub-folder that is cut ends them, and its slices are its own.
        var cut = new List<List<SliceFile>>();
        var joinable = new List<List<SliceFile>>();
        var own = files.Where(file => ChildOf(folder, file) is null).ToList();
        joinable.AddRange(Pack(own, limit));
        var children = files
            .Where(file => ChildOf(folder, file) is not null)
            .GroupBy(file => ChildOf(folder, file))
            .OrderBy(child => child.Key, StringComparer.Ordinal);
        foreach (var child in children)
        {
            var below = child.ToList();
            if (below.Sum(file => file.Lines) <= limit)
            {
                joinable.Add(below);
                continue;
            }

            cut.AddRange(Join(joinable, limit));
            joinable.Clear();
            cut.AddRange(CutFolder($"{folder}/{child.Key}", below, limit));
        }

        cut.AddRange(Join(joinable, limit));
        return cut;
    }

    // The name of the sub-folder of a folder that a file is in, or null for a file of the folder itself.
    private static string? ChildOf(string folder, SliceFile file)
    {
        var rest = file.Path[(folder.Length + 1)..];
        var end = rest.IndexOf('/', StringComparison.Ordinal);
        return end < 0 ? null : rest[..end];
    }

    // The files of one folder in parts of at most the limit, in path order, the files of one partial class as one
    // at the place of the first.
    private static List<List<SliceFile>> Pack(List<SliceFile> files, int limit)
    {
        var units = files
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .GroupBy(file => file.PartialClass ?? file.Path, StringComparer.Ordinal)
            .Select(unit => unit.ToList());
        return Join(units, limit);
    }

    private static List<List<SliceFile>> Join(IEnumerable<List<SliceFile>> blocks, int limit)
    {
        var joined = new List<List<SliceFile>>();
        var lines = 0;
        foreach (var block in blocks)
        {
            var more = block.Sum(file => file.Lines);
            if (joined.Count == 0 || lines + more > limit)
            {
                joined.Add([.. block]);
                lines = more;
            }
            else
            {
                joined[^1].AddRange(block);
                lines += more;
            }
        }

        return joined;
    }

    private static string NameOf(string[] paths)
    {
        var folder = paths[0][..paths[0].LastIndexOf('/')];
        while (!paths.All(path => path.StartsWith(folder + "/", StringComparison.Ordinal)))
        {
            folder = folder[..folder.LastIndexOf('/')];
        }

        const string Tests = "tests/";
        var name = folder.StartsWith(Tests, StringComparison.Ordinal) ? folder[Tests.Length..] : folder;
        return name.Replace('/', '.');
    }
}
