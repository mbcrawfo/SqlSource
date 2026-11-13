using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace SqlSource.MutationReport;

// Reads the Cobertura file that a test project writes with --coverage, and finds the lines of the source that only
// the database projects reach.
internal static class CoverageReader
{
    // The lines with a hit, by file from the repository root with '/', of the files under src/.  Throws
    // InvalidDataException for text that is no Cobertura file.
    public static IReadOnlyDictionary<string, IReadOnlySet<int>> Read(Stream cobertura, string repositoryRoot)
    {
        var hit = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        try
        {
            using var reader = XmlReader.Create(cobertura);
            if (reader.MoveToContent() != XmlNodeType.Element || reader.Name != "coverage")
            {
                throw new InvalidDataException("It is no Cobertura file: its root is not <coverage>.");
            }

            // A class holds its lines twice, under each method and under itself, and a file may be several classes.
            HashSet<int>? lines = null;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.Name == "class")
                {
                    var file = ReportReader.FromRoot(repositoryRoot, reader.GetAttribute("filename") ?? "");
                    lines = null;
                    if (file.StartsWith("src/", StringComparison.Ordinal) && !hit.TryGetValue(file, out lines))
                    {
                        lines = [];
                        hit.Add(file, lines);
                    }
                }
                else if (reader.Name == "line" && lines is not null)
                {
                    if (
                        !int.TryParse(
                            reader.GetAttribute("number"),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var number
                        )
                        || !long.TryParse(
                            reader.GetAttribute("hits"),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var hits
                        )
                    )
                    {
                        throw new InvalidDataException("It is no Cobertura file: a line has no number or hits.");
                    }

                    if (hits > 0)
                    {
                        _ = lines.Add(number);
                    }
                }
            }
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"It is no Cobertura file: {exception.Message}", exception);
        }

        return hit.Where(file => file.Value.Count > 0)
            .ToDictionary(file => file.Key, file => (IReadOnlySet<int>)file.Value, StringComparer.Ordinal);
    }

    // The lines that a file of database hits and no file of others does, as ranges, by file in ordinal order.
    public static IReadOnlyList<(string File, IReadOnlyList<(int From, int To)> Lines)> Only(
        IReadOnlyList<IReadOnlyDictionary<string, IReadOnlySet<int>>> database,
        IReadOnlyList<IReadOnlyDictionary<string, IReadOnlySet<int>>> others
    )
    {
        var only = new SortedDictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        foreach (var (file, lines) in database.SelectMany(project => project))
        {
            foreach (var line in lines)
            {
                if (others.Any(project => project.TryGetValue(file, out var reached) && reached.Contains(line)))
                {
                    continue;
                }

                if (!only.TryGetValue(file, out var kept))
                {
                    kept = [];
                    only.Add(file, kept);
                }

                _ = kept.Add(line);
            }
        }

        return [.. only.Select(file => (file.Key, LineRanges.Of(file.Value)))];
    }
}

// Lines as the ranges of those that follow each other.
internal static class LineRanges
{
    public static IReadOnlyList<(int From, int To)> Of(IEnumerable<int> lines)
    {
        var ranges = new List<(int From, int To)>();
        foreach (var line in lines.Distinct().Order())
        {
            if (ranges.Count > 0 && ranges[^1].To == line - 1)
            {
                ranges[^1] = (ranges[^1].From, line);
            }
            else
            {
                ranges.Add((line, line));
            }
        }

        return ranges;
    }
}
