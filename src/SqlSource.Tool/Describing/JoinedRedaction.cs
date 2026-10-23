using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Takes a connection's secret out of the texts of one event, also where it stands over several of them.
/// </summary>
/// <remarks>
/// <para>
/// A describer can write a value half in one string of an event and half in the next, where a search of each alone
/// finds neither.  Every range is found in the original texts, and only then is <see cref="Redaction.Mark" /> written
/// over each, once: cleaning a text first could hide the half of a value that its neighbour holds the other half of.
/// A range writes the mark over the slice of each text it overlaps, so that the texts keep their order and their
/// number; the slices of one text that overlap or touch make one mark.  A separator is in no text.
/// </para>
/// <para>
/// The ranges are those of each text alone, as <see cref="Redaction.Of(string, ConnectionSecret)" /> finds them, and
/// those of the texts joined with nothing between them and with a line break between them: all of them, and each
/// subset the caller names.  The line break is searched because a value that holds one is cut where it stands, and the
/// texts are its lines, as the server lines of a failure are.  A form or part with no line break cannot stand over a
/// separator, and a token ends at the end of a text either way, so only those that hold a line break (as they are or
/// written with <c>\n</c>) are searched joined with one.
/// </para>
/// </remarks>
internal static class JoinedRedaction
{
    private const string Break = "\n";

    /// <summary>
    /// The texts with the secret written as <see cref="Redaction.Mark" /> wherever it stands in one of them, in all of
    /// them joined, and in the texts of each subset joined.
    /// </summary>
    /// <param name="texts">The strings of an event, in the order they were written.</param>
    /// <param name="secret">The connection's value and its secret parts.</param>
    /// <param name="subsets">
    /// Lists of indexes into <paramref name="texts" />, each searched joined in the order it names, for the texts that
    /// a describer wrote the value over with others between them.
    /// </param>
    public static string[] Of(IReadOnlyList<string> texts, ConnectionSecret secret, params IReadOnlyList<int>[] subsets)
    {
        var cuts = new List<(int Start, int End)>?[texts.Count];
        var lineWholes = WithBreaks(secret.Wholes);
        var lineParts = WithBreaks(secret.Parts);
        int[] all = [.. Enumerable.Range(0, texts.Count)];
        foreach (var index in all)
        {
            Cut(texts, [index], string.Empty, secret.Wholes, secret.Parts, cuts);
        }

        foreach (var group in subsets.Prepend(all).Where(static group => group.Count > 0))
        {
            Cut(texts, group, string.Empty, secret.Wholes, secret.Parts, cuts);
            Cut(texts, group, Break, lineWholes, lineParts, cuts);
        }

        return [.. texts.Select((text, index) => cuts[index] is { } slices ? Marked(text, slices) : text)];
    }

    // The forms or parts as they are and written with the break, those that hold one.
    private static List<string> WithBreaks(IEnumerable<string> forms) =>
        [
            .. forms
                .SelectMany(static form => new[] { form, form.ReplaceLineEndings(Break) })
                .Where(static form => form.Contains(Break, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal),
        ];

    // Adds to the cuts of each text of the group the slices of it that the wholes and the parts cover in the group's
    // texts joined.
    private static void Cut(
        IReadOnlyList<string> texts,
        IReadOnlyList<int> group,
        string separator,
        IReadOnlyList<string> wholes,
        IReadOnlyList<string> parts,
        List<(int Start, int End)>?[] cuts
    )
    {
        if (wholes.Count == 0 && parts.Count == 0)
        {
            return;
        }

        var joined = string.Join(separator, group.Select(index => texts[index]));
        var starts = new int[group.Count];
        for (var i = 1; i < group.Count; i++)
        {
            starts[i] = starts[i - 1] + texts[group[i - 1]].Length + separator.Length;
        }

        foreach (var whole in wholes)
        {
            for (
                var found = joined.IndexOf(whole, StringComparison.Ordinal);
                found >= 0;
                found = joined.IndexOf(whole, found + 1, StringComparison.Ordinal)
            )
            {
                Cover(texts, group, starts, found, found + whole.Length, cuts);
            }
        }

        foreach (var part in parts)
        {
            for (
                var found = Redaction.NextToken(joined, part, 0);
                found >= 0;
                found = Redaction.NextToken(joined, part, found + 1)
            )
            {
                Cover(texts, group, starts, found, found + part.Length, cuts);
            }
        }
    }

    // Adds the slice of each text that the range of the joined text overlaps.
    private static void Cover(
        IReadOnlyList<string> texts,
        IReadOnlyList<int> group,
        int[] starts,
        int start,
        int end,
        List<(int Start, int End)>?[] cuts
    )
    {
        for (var i = 0; i < group.Count && starts[i] < end; i++)
        {
            var length = texts[group[i]].Length;
            var from = Math.Max(start, starts[i]);
            var to = Math.Min(end, starts[i] + length);
            if (from < to)
            {
                (cuts[group[i]] ??= []).Add((from - starts[i], to - starts[i]));
            }
        }
    }

    // The text with each run of slices that overlap or touch written as one mark.
    private static string Marked(string text, List<(int Start, int End)> slices)
    {
        slices.Sort();
        List<(int Start, int End)> runs = [];
        foreach (var slice in slices)
        {
            if (runs.Count > 0 && slice.Start <= runs[^1].End)
            {
                runs[^1] = (runs[^1].Start, Math.Max(runs[^1].End, slice.End));
            }
            else
            {
                runs.Add(slice);
            }
        }

        var marked = new StringBuilder(text.Length);
        var from = 0;
        foreach (var (start, end) in runs)
        {
            _ = marked.Append(text, from, start - from).Append(Redaction.Mark);
            from = end;
        }

        return marked.Append(text, from, text.Length - from).ToString();
    }
}
