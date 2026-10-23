using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Takes a connection's secret out of a text that a describer gave, before the text is printed.
/// </summary>
/// <remarks>
/// A describer's rule is to put the value in nothing it returns.  The run does not rely on it: a driver's message
/// for a connection string that is wrong can quote the string, and one for a password that is wrong can quote the
/// password.  The whole value is replaced wherever it stands, and each part the describer said is secret where it
/// stands as a token (<see cref="HoldsToken" />).  A whole form is the value, the value without the white space at its
/// ends, and each rewrite of it that the describer gave (<see cref="ConnectionSecret.Wholes" />): a driver that
/// trims the value or writes it again quotes the other text in a message.
/// </remarks>
internal static class Redaction
{
    /// <summary>What stands where the value was.</summary>
    public const string Mark = "***";

    /// <summary>
    /// The text with each whole form of the value wherever it stands, and then each part where it stands as a token,
    /// written as <see cref="Mark" />.
    /// </summary>
    public static string Of(string text, ConnectionSecret secret) => Of(text, secret.Wholes, secret.Parts);

    /// <summary>
    /// The failure with the secret taken out of its descriptor and of what the server said.  Its arguments and its
    /// help are cleaned with the error that is made of it.
    /// </summary>
    /// <remarks>
    /// A describer cuts what a server said into lines, so a value that holds a line break is in several of them and
    /// in none whole: the lines are searched joined.  A descriptor is a describer's text when the describer built
    /// it from what a server said.
    /// </remarks>
    public static DescribeFailure Of(DescribeFailure failure, ConnectionSecret secret) =>
        failure with
        {
            Descriptor = OfDescriptor(failure.Descriptor, secret),
            ServerLines = OfLines(failure.ServerLines, secret),
        };

    /// <summary>
    /// The error with the secret taken out of its arguments, of the label and the text of each line under it, and of
    /// its message.
    /// </summary>
    /// <remarks>
    /// Each text is cleaned as it is, then put on one line as <see cref="Reporter" /> prints it, and cleaned again:
    /// a line break written as a space can make a part that holds a space.  A format and an argument, or two
    /// arguments, can make the value together, so the message is formatted from the cleaned arguments, as
    /// <see cref="Reporter" /> formats it, and searched too, as it is and on one line, since a format may hold a line
    /// break.  When it holds the secret, the error gets a copy of its
    /// descriptor whose format is <c>{0}</c>, and the cleaned message is its one argument.
    /// </remarks>
    public static ToolDiagnostic Of(ToolDiagnostic diagnostic, ConnectionSecret secret)
    {
        string[] arguments = [.. diagnostic.Arguments.Select(argument => AsPrinted(argument, secret))];
        var cleaned = diagnostic with
        {
            Arguments = new EquatableArray<string>([.. arguments]),
            Lines = new EquatableArray<ContinuationLine>([
                .. diagnostic.Lines.Select(line =>
                    line with
                    {
                        Label = AsPrinted(line.Label, secret),
                        Text = AsPrinted(line.Text, secret),
                    }
                ),
            ]),
        };

        object?[] values = [.. arguments];
        var message = string.Format(
            CultureInfo.InvariantCulture,
            diagnostic.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            values
        );
        return IsInAsPrinted(message, secret)
            ? cleaned with
            {
                Descriptor = WithWholeMessage(diagnostic.Descriptor),
                Arguments = new EquatableArray<string>([AsPrinted(message, secret)]),
            }
            : cleaned;
    }

    /// <summary>
    /// The texts each cleaned alone, and then cleaned again for what they hold together: each whole form anywhere in
    /// the texts joined, and each part where it stands as a token in them joined, and again with a line break between
    /// them for the forms and parts that hold one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A describer can write a value half in one string of an event and half in the next, where a search of each
    /// alone finds neither.  A range that is found writes <see cref="Mark" /> over the slice of each text it
    /// overlaps, so that the texts keep their order and their number; slices of one text that touch make one mark.
    /// A separator is in no text.
    /// </para>
    /// <para>
    /// The line break is searched because a value that holds one is cut where it stands: the texts are its lines, as
    /// the server lines of a failure are (<see cref="OfLines" />).  A part with no line break cannot stand over a
    /// separator, and a token ends at the end of a text either way, so only the forms and parts that hold a line break
    /// are searched joined with one.
    /// </para>
    /// </remarks>
    public static string[] OfJoined(IReadOnlyList<string> texts, ConnectionSecret secret)
    {
        const string Break = "\n";
        string[] cleaned = [.. texts.Select(text => Of(text, secret))];
        var cuts = new List<(int Start, int End)>?[cleaned.Length];
        Cut(cleaned, string.Empty, secret.Wholes, secret.Parts, cuts);
        Cut(cleaned, Break, WithBreaks(secret.Wholes, Break), WithBreaks(secret.Parts, Break), cuts);
        for (var i = 0; i < cleaned.Length; i++)
        {
            if (cuts[i] is { } slices)
            {
                cleaned[i] = Marked(cleaned[i], slices);
            }
        }

        return cleaned;
    }

    /// <summary>
    /// Whether the text holds the part as a token: where its first character, when that is a letter, a digit or
    /// <c>_</c>, follows none of those, and its last character, when that is one, is followed by none.
    /// </summary>
    public static bool HoldsToken(string text, string part) => NextToken(text, part, 0) >= 0;

    // The same descriptor when no text of it holds the secret, as it is or on one line, which is every descriptor but
    // a broken describer's.  One that does has each text cleaned as AsPrinted cleans it.
    private static DiagnosticDescriptor OfDescriptor(DiagnosticDescriptor descriptor, ConnectionSecret secret)
    {
        var culture = CultureInfo.InvariantCulture;
        var title = descriptor.Title.ToString(culture);
        var format = descriptor.MessageFormat.ToString(culture);
        var description = descriptor.Description.ToString(culture);
        string[] texts = [descriptor.Id, title, format, descriptor.Category, description, descriptor.HelpLinkUri];
        return texts.Any(text => IsInAsPrinted(text, secret))
            ? new DiagnosticDescriptor(
                AsPrinted(descriptor.Id, secret),
                AsPrinted(title, secret),
                AsPrinted(format, secret),
                AsPrinted(descriptor.Category, secret),
                descriptor.DefaultSeverity,
                descriptor.IsEnabledByDefault,
                AsPrinted(description, secret),
                AsPrinted(descriptor.HelpLinkUri, secret),
                [.. descriptor.CustomTags]
            )
            : descriptor;
    }

    // The descriptor whose message is its one argument.
    private static DiagnosticDescriptor WithWholeMessage(DiagnosticDescriptor descriptor)
    {
        var culture = CultureInfo.InvariantCulture;
        return new DiagnosticDescriptor(
            descriptor.Id,
            descriptor.Title.ToString(culture),
            "{0}",
            descriptor.Category,
            descriptor.DefaultSeverity,
            descriptor.IsEnabledByDefault,
            descriptor.Description.ToString(culture),
            descriptor.HelpLinkUri,
            [.. descriptor.CustomTags]
        );
    }

    /// <summary>
    /// The text as <see cref="Reporter" /> prints it, on one line, with the secret taken out before and after it is
    /// put there: a line break written as a space can make a part that holds a space.
    /// </summary>
    public static string AsPrinted(string text, ConnectionSecret secret) => Of(OneLine.Of(Of(text, secret)), secret);

    // Whether the text holds the secret as it is or on one line, where a line break written as a space can make a
    // part that holds a space.  The search of AsPrinted.
    private static bool IsInAsPrinted(string text, ConnectionSecret secret) =>
        secret.IsIn(text) || secret.IsIn(OneLine.Of(text));

    private static string Of(string text, IEnumerable<string> wholes, IEnumerable<string> parts)
    {
        var cleaned = text;
        foreach (var whole in wholes)
        {
            cleaned = cleaned.Replace(whole, Mark, StringComparison.Ordinal);
        }

        foreach (var part in parts)
        {
            cleaned = OfToken(cleaned, part);
        }

        return cleaned;
    }

    // The cut dropped the kind of each line break, so each whole form and each part are searched as they are and with
    // their breaks as the one the lines are joined with.  A value that was in two lines leaves one.
    private static EquatableArray<string> OfLines(EquatableArray<string> lines, ConnectionSecret secret)
    {
        const string Break = "\n";
        var joined = string.Join(Break, lines);
        var cleaned = Of(
            Of(joined, secret),
            secret
                .Wholes.Select(static whole => whole.ReplaceLineEndings(Break))
                .OrderByDescending(static whole => whole.Length),
            secret.Parts.Select(static part => part.ReplaceLineEndings(Break))
        );
        return string.Equals(cleaned, joined, StringComparison.Ordinal)
            ? lines
            : new EquatableArray<string>([.. cleaned.Split(Break)]);
    }

    // The forms or parts as they are searched joined with the break, those that hold one.
    private static List<string> WithBreaks(IEnumerable<string> forms, string lineBreak) =>
        [
            .. forms
                .Select(form => form.ReplaceLineEndings(lineBreak))
                .Where(form => form.Contains(lineBreak, StringComparison.Ordinal)),
        ];

    // Adds to the cuts of each text the slices of it that the wholes and the parts cover in the texts joined.
    private static void Cut(
        string[] texts,
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

        var joined = string.Join(separator, texts);
        var starts = new int[texts.Length];
        for (var i = 1; i < texts.Length; i++)
        {
            starts[i] = starts[i - 1] + texts[i - 1].Length + separator.Length;
        }

        foreach (var whole in wholes)
        {
            for (
                var found = joined.IndexOf(whole, StringComparison.Ordinal);
                found >= 0;
                found = joined.IndexOf(whole, found + 1, StringComparison.Ordinal)
            )
            {
                Cover(texts, starts, found, found + whole.Length, cuts);
            }
        }

        foreach (var part in parts)
        {
            for (var found = NextToken(joined, part, 0); found >= 0; found = NextToken(joined, part, found + 1))
            {
                Cover(texts, starts, found, found + part.Length, cuts);
            }
        }
    }

    // Adds the slice of each text that the range of the joined text overlaps.
    private static void Cover(string[] texts, int[] starts, int start, int end, List<(int Start, int End)>?[] cuts)
    {
        for (var i = 0; i < texts.Length && starts[i] < end; i++)
        {
            var from = Math.Max(start, starts[i]);
            var to = Math.Min(end, starts[i] + texts[i].Length);
            if (from < to)
            {
                (cuts[i] ??= []).Add((from - starts[i], to - starts[i]));
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
            _ = marked.Append(text, from, start - from).Append(Mark);
            from = end;
        }

        return marked.Append(text, from, text.Length - from).ToString();
    }

    private static string OfToken(string text, string part)
    {
        var found = NextToken(text, part, 0);
        if (found < 0)
        {
            return text;
        }

        var cleaned = new StringBuilder(text.Length);
        var from = 0;
        while (found >= 0)
        {
            _ = cleaned.Append(text, from, found - from).Append(Mark);
            from = found + part.Length;
            found = NextToken(text, part, from);
        }

        return cleaned.Append(text, from, text.Length - from).ToString();
    }

    // Where the part next stands as a token, at or after the start, or -1.  The one place of the rule of a token.
    private static int NextToken(string text, string part, int start)
    {
        if (part.Length == 0)
        {
            return -1;
        }

        var checksBefore = IsWordCharacter(part[0]);
        var checksAfter = IsWordCharacter(part[^1]);
        for (
            var found = text.IndexOf(part, start, StringComparison.Ordinal);
            found >= 0;
            found = text.IndexOf(part, found + 1, StringComparison.Ordinal)
        )
        {
            var end = found + part.Length;
            var joinsBefore = checksBefore && found > 0 && IsWordCharacter(text[found - 1]);
            var joinsAfter = checksAfter && end < text.Length && IsWordCharacter(text[end]);
            if (!joinsBefore && !joinsAfter)
            {
                return found;
            }
        }

        return -1;
    }

    private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';
}
