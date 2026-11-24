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
/// A text that was marked is then cleaned alone by <see cref="Redaction.Of(string, ConnectionSecret)" />, so that the
/// result is never less than what that gives: a part that stood against a whole is a token only once the whole is a
/// mark, and a range judged on the original text would leave it.
/// </para>
/// <para>
/// The ranges are those of each text alone, as <see cref="Redaction.Of(string, ConnectionSecret)" /> finds them, and
/// those of the texts joined with nothing between them and with a line break between them: all of them, and each
/// subset the caller names.  The line break is searched because a value that holds one is cut where it stands, and the
/// texts are its lines, as the server lines of a failure are.  A form or part with no line break cannot stand over a
/// separator, and a token ends at the end of a text either way, so only those that hold a line break (as they are or
/// written with <c>\n</c>) are searched joined with one.
/// </para>
/// <para>
/// <see cref="OfEveryPair" /> also searches every two texts joined, in both orders, for a whole form or a part cut in
/// two, as a member's name and a later member's value, or two values written in the reverse order.  It does not join
/// them: a whole or a part over two texts ends the first with its head and starts the second with the rest, so each cut
/// of it is looked for at the ends of each text, and the search grows with the number of texts and not with its square.
/// A part's ends are judged as a token's against the text beside them.  A keyed part is not searched so: its key would
/// have to be found over two texts as well.
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
    public static string[] Of(
        IReadOnlyList<string> texts,
        ConnectionSecret secret,
        params IReadOnlyList<int>[] subsets
    ) => Cleaned(texts, secret, everyPair: false, subsets);

    /// <summary>
    /// The texts cleaned as <see cref="Of" /> cleans them, and also where a whole form or a part stands over two of
    /// them joined in either order.
    /// </summary>
    /// <param name="texts">The strings of an event, in the order they were written.</param>
    /// <param name="secret">The connection's value and its secret parts.</param>
    /// <param name="subsets">The lists of indexes that <see cref="Of" /> takes.</param>
    public static string[] OfEveryPair(
        IReadOnlyList<string> texts,
        ConnectionSecret secret,
        params IReadOnlyList<int>[] subsets
    ) => Cleaned(texts, secret, everyPair: true, subsets);

    private static string[] Cleaned(
        IReadOnlyList<string> texts,
        ConnectionSecret secret,
        bool everyPair,
        IReadOnlyList<int>[] subsets
    )
    {
        var cuts = new List<(int Start, int End)>?[texts.Count];
        var plain = new Forms(secret.Wholes, secret.Parts, secret.KeyedParts);
        var lines = new Forms(WithBreaks(secret.Wholes), WithBreaks(secret.Parts), WithBreaks(secret.KeyedParts));
        int[] all = [.. Enumerable.Range(0, texts.Count)];
        foreach (var index in all)
        {
            Cut(texts, [index], string.Empty, plain, cuts);
        }

        foreach (var group in subsets.Prepend(all).Where(static group => group.Count > 0))
        {
            Cut(texts, group, string.Empty, plain, cuts);
            Cut(texts, group, Break, lines, cuts);
        }

        if (everyPair)
        {
            CutPairs(texts, string.Empty, plain, cuts);
            CutPairs(texts, Break, lines, cuts);
        }

        return
        [
            .. texts.Select(
                (text, index) => cuts[index] is { } slices ? Redaction.Of(Marked(text, slices), secret) : text
            ),
        ];
    }

    // The forms or parts as they are and written with the break, those that hold one.
    private static List<string> WithBreaks(IEnumerable<string> forms) =>
        [
            .. forms
                .SelectMany(static form => new[] { form, form.ReplaceLineEndings(Break) })
                .Where(static form => form.Contains(Break, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal),
        ];

    // Adds to the cuts of each text of the group the slices of it that the wholes, the parts and the keyed parts cover
    // in the group's texts joined.
    private static void Cut(
        IReadOnlyList<string> texts,
        IReadOnlyList<int> group,
        string separator,
        Forms forms,
        List<(int Start, int End)>?[] cuts
    )
    {
        if (forms.IsEmpty)
        {
            return;
        }

        var joined = string.Join(separator, group.Select(index => texts[index]));
        var starts = new int[group.Count];
        for (var i = 1; i < group.Count; i++)
        {
            starts[i] = starts[i - 1] + texts[group[i - 1]].Length + separator.Length;
        }

        foreach (var whole in forms.Wholes)
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

        foreach (var (found, length) in Found(joined, forms.Parts, Redaction.NextToken))
        {
            Cover(texts, group, starts, found, found + length, cuts);
        }

        foreach (var (found, length) in Found(joined, forms.Keyed, Redaction.NextKeyed))
        {
            Cover(texts, group, starts, found, found + length, cuts);
        }
    }

    // Where each part stands as the search finds it, with its length.
    private static IEnumerable<(int Found, int Length)> Found(
        string text,
        IReadOnlyList<string> parts,
        Func<string, string, int, int> next
    )
    {
        foreach (var part in parts)
        {
            for (var found = next(text, part, 0); found >= 0; found = next(text, part, found + 1))
            {
                yield return (found, part.Length);
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

    // Adds the slices that a whole or a part covers over two different texts joined with the separator, in either
    // order.  With no separator a form is cut between two of its characters, so each text holds some of it; with the
    // break, the form is cut at a break of its own, which the separator stands for, and a half may be empty.
    private static void CutPairs(
        IReadOnlyList<string> texts,
        string separator,
        Forms forms,
        List<(int Start, int End)>?[] cuts
    )
    {
        foreach (
            var (form, isPart) in forms
                .Wholes.Select(static whole => (whole, false))
                .Concat(forms.Parts.Select(static part => (part, true)))
        )
        {
            foreach (var cut in CutsOf(form, separator))
            {
                var head = form[..cut];
                var tail = form[(cut + separator.Length)..];
                var ends = Matching(texts, text => EndsWith(text, head, form, isPart));
                var begins = Matching(texts, text => BeginsWith(text, tail, form, isPart));
                foreach (var index in ends.Where(index => begins.Any(other => other != index)))
                {
                    AddSlice(cuts, index, texts[index].Length - head.Length, texts[index].Length);
                }

                foreach (var index in begins.Where(index => ends.Any(other => other != index)))
                {
                    AddSlice(cuts, index, 0, tail.Length);
                }
            }
        }
    }

    // Where the form may be cut: between two characters, or at each break that the separator stands for.
    private static IEnumerable<int> CutsOf(string form, string separator) =>
        separator.Length == 0
            ? Enumerable.Range(1, Math.Max(form.Length - 1, 0))
            : Enumerable
                .Range(0, form.Length)
                .Where(index => string.CompareOrdinal(form, index, separator, 0, separator.Length) == 0);

    // The indexes of the texts that hold the side.
    private static List<int> Matching(IReadOnlyList<string> texts, Func<string, bool> holds)
    {
        List<int> found = [];
        for (var index = 0; index < texts.Count; index++)
        {
            if (holds(texts[index]))
            {
                found.Add(index);
            }
        }

        return found;
    }

    // Whether the text ends with the head of the form, and, for a part, begins it where a token may begin.
    private static bool EndsWith(string text, string head, string form, bool isPart)
    {
        if (!text.EndsWith(head, StringComparison.Ordinal))
        {
            return false;
        }

        var before = text.Length - head.Length - 1;
        return !isPart || !Redaction.IsWordCharacter(form[0]) || before < 0 || !Redaction.IsWordCharacter(text[before]);
    }

    // Whether the text begins with the tail of the form, and, for a part, ends it where a token may end.
    private static bool BeginsWith(string text, string tail, string form, bool isPart)
    {
        if (!text.StartsWith(tail, StringComparison.Ordinal))
        {
            return false;
        }

        var after = tail.Length;
        return !isPart
            || !Redaction.IsWordCharacter(form[^1])
            || after == text.Length
            || !Redaction.IsWordCharacter(text[after]);
    }

    private static void AddSlice(List<(int Start, int End)>?[] cuts, int index, int start, int end)
    {
        if (start < end)
        {
            (cuts[index] ??= []).Add((start, end));
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

    // The wholes, the parts and the keyed parts that one search looks for.
    private sealed record Forms(IReadOnlyList<string> Wholes, IReadOnlyList<string> Parts, IReadOnlyList<string> Keyed)
    {
        public bool IsEmpty => Wholes.Count == 0 && Parts.Count == 0 && Keyed.Count == 0;
    }
}
