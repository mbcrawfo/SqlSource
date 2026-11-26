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
/// stands as a token (<see cref="HoldsToken" />), or, for a part too short or too common to be a token
/// (<see cref="ConnectionSecret.KeyedParts" />), where it stands as the value of a password key
/// (<see cref="HoldsKeyed" />).  A whole form is the value, the value without the white space at its ends, and each
/// rewrite of it that the describer gave (<see cref="ConnectionSecret.Wholes" />): a driver that trims the value or
/// writes it again quotes the other text in a message.
/// </remarks>
internal static class Redaction
{
    /// <summary>What stands where the value was.</summary>
    public const string Mark = "***";

    // The keys whose value is a password, as the drivers write them in a connection string, ignoring case.  "password"
    // ends any key, as "SSL Password" and PGPASSWORD do; a short one is a key of its own, which no letter or digit
    // comes before.
    private static readonly (string Key, bool Alone)[] PasswordKeys =
    [
        ("password", false),
        ("pwd", true),
        ("psw", true),
    ];

    /// <summary>
    /// The text with each whole form of the value wherever it stands, then each part where it stands as a token, and
    /// then each keyed part where it stands as the value of a password key, written as <see cref="Mark" />.
    /// </summary>
    public static string Of(string text, ConnectionSecret secret) =>
        Of(text, secret.Wholes, secret.Parts, secret.KeyedParts);

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
    /// Whether the text holds the part as a token: where its first character, when that is a letter, a digit or
    /// <c>_</c>, follows none of those, and its last character, when that is one, is followed by none.
    /// </summary>
    public static bool HoldsToken(string text, string part) => NextToken(text, part, 0) >= 0;

    /// <summary>
    /// Whether the text holds the part as the value of a password key: as a token (<see cref="HoldsToken" />) after a
    /// key, a quote, white space, <c>=</c> or <c>:</c>, white space and a quote, each but the sign optional.  The key
    /// ends in <c>password</c>, or is <c>pwd</c> or <c>psw</c> after no letter or digit, ignoring case:
    /// <c>Password=pg;</c>, <c>PWD = 'pg'</c>, <c>"password":"pg"</c>.
    /// </summary>
    public static bool HoldsKeyed(string text, string part) => NextKeyed(text, part, 0) >= 0;

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

    private static string Of(
        string text,
        IEnumerable<string> wholes,
        IEnumerable<string> parts,
        IEnumerable<string> keyed
    )
    {
        var cleaned = text;
        foreach (var whole in wholes)
        {
            cleaned = cleaned.Replace(whole, Mark, StringComparison.Ordinal);
        }

        foreach (var part in parts)
        {
            cleaned = OfFound(cleaned, part, NextToken);
        }

        foreach (var part in keyed)
        {
            cleaned = OfFound(cleaned, part, NextKeyed);
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
            secret.Parts.Select(static part => part.ReplaceLineEndings(Break)),
            secret.KeyedParts.Select(static part => part.ReplaceLineEndings(Break))
        );
        return string.Equals(cleaned, joined, StringComparison.Ordinal)
            ? lines
            : new EquatableArray<string>([.. cleaned.Split(Break)]);
    }

    // The text with each place the search finds the part written as the mark.
    private static string OfFound(string text, string part, Func<string, string, int, int> next)
    {
        var found = next(text, part, 0);
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
            found = next(text, part, from);
        }

        return cleaned.Append(text, from, text.Length - from).ToString();
    }

    // Where the part next stands as the value of a password key, at or after the start, or -1.  JoinedRedaction uses
    // it too.
    internal static int NextKeyed(string text, string part, int start)
    {
        for (var found = NextToken(text, part, start); found >= 0; found = NextToken(text, part, found + 1))
        {
            if (FollowsPasswordKey(text, found))
            {
                return found;
            }
        }

        return -1;
    }

    // Whether a password key and its sign stand before the index, with the white space and the quotes HoldsKeyed
    // allows.
    private static bool FollowsPasswordKey(string text, int value)
    {
        var at = WhiteSpaceBefore(text, QuoteBefore(text, value));
        if (at == 0 || text[at - 1] is not ('=' or ':'))
        {
            return false;
        }

        at = QuoteBefore(text, WhiteSpaceBefore(text, at - 1));
        foreach (var (key, alone) in PasswordKeys)
        {
            var keyStart = at - key.Length;
            if (
                keyStart >= 0
                && string.Compare(text, keyStart, key, 0, key.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (!alone || keyStart == 0 || !char.IsLetterOrDigit(text[keyStart - 1]))
            )
            {
                return true;
            }
        }

        return false;
    }

    // Where a quote that ends at the index starts, or the index when none does.
    private static int QuoteBefore(string text, int end) => end > 0 && text[end - 1] is '\'' or '"' ? end - 1 : end;

    // Where the white space that ends at the index starts.
    private static int WhiteSpaceBefore(string text, int end)
    {
        var at = end;
        while (at > 0 && char.IsWhiteSpace(text[at - 1]))
        {
            at--;
        }

        return at;
    }

    // Where the part next stands as a token, at or after the start, or -1.  The one place of the rule of a token, which
    // JoinedRedaction uses too.
    internal static int NextToken(string text, string part, int start)
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

    // A character that a token may not have beside it.  JoinedRedaction judges the ends of a part cut in two by it.
    internal static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';
}
