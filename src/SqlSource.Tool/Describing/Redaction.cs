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
/// stands as a token (<see cref="HoldsToken" />).
/// </remarks>
internal static class Redaction
{
    /// <summary>What stands where the value was.</summary>
    public const string Mark = "***";

    /// <summary>
    /// The text with the whole value wherever it stands, and then each part where it stands as a token, written as
    /// <see cref="Mark" />.
    /// </summary>
    public static string Of(string text, ConnectionSecret secret) => Of(text, secret.Value, secret.Parts);

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
    /// The error with the secret taken out of its arguments, of the text of each line under it, and of its message.
    /// </summary>
    /// <remarks>
    /// Each text is cleaned as it is, then put on one line as <see cref="Reporter" /> prints it, and cleaned again:
    /// a line break written as a space can make a part that holds a space.  A format and an argument, or two
    /// arguments, can make the value together, so the message is formatted from the cleaned arguments, as
    /// <see cref="Reporter" /> formats it, and searched too.  When it holds the secret, the error gets a copy of its
    /// descriptor whose format is <c>{0}</c>, and the cleaned message is its one argument.
    /// </remarks>
    public static ToolDiagnostic Of(ToolDiagnostic diagnostic, ConnectionSecret secret)
    {
        string[] arguments = [.. diagnostic.Arguments.Select(argument => AsPrinted(argument, secret))];
        var cleaned = diagnostic with
        {
            Arguments = new EquatableArray<string>([.. arguments]),
            Lines = new EquatableArray<ContinuationLine>([
                .. diagnostic.Lines.Select(line => line with { Text = AsPrinted(line.Text, secret) }),
            ]),
        };

        object?[] values = [.. arguments];
        var message = string.Format(
            CultureInfo.InvariantCulture,
            diagnostic.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            values
        );
        return secret.IsIn(message)
            ? cleaned with
            {
                Descriptor = WithWholeMessage(diagnostic.Descriptor),
                Arguments = new EquatableArray<string>([Of(message, secret)]),
            }
            : cleaned;
    }

    /// <summary>
    /// Whether the text holds the part as a token: where its first character, when that is a letter, a digit or
    /// <c>_</c>, follows none of those, and its last character, when that is one, is followed by none.
    /// </summary>
    public static bool HoldsToken(string text, string part) => NextToken(text, part, 0) >= 0;

    // The same descriptor when no text of it holds the secret, which is every descriptor but a broken describer's.
    private static DiagnosticDescriptor OfDescriptor(DiagnosticDescriptor descriptor, ConnectionSecret secret)
    {
        var culture = CultureInfo.InvariantCulture;
        var title = descriptor.Title.ToString(culture);
        var format = descriptor.MessageFormat.ToString(culture);
        var description = descriptor.Description.ToString(culture);
        string[] texts = [descriptor.Id, title, format, descriptor.Category, description, descriptor.HelpLinkUri];
        return texts.Any(secret.IsIn)
            ? new DiagnosticDescriptor(
                Of(descriptor.Id, secret),
                Of(title, secret),
                Of(format, secret),
                Of(descriptor.Category, secret),
                descriptor.DefaultSeverity,
                descriptor.IsEnabledByDefault,
                Of(description, secret),
                Of(descriptor.HelpLinkUri, secret),
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

    // The text as Reporter prints it, with the secret taken out before and after it is put on one line.
    private static string AsPrinted(string text, ConnectionSecret secret) => Of(OneLine.Of(Of(text, secret)), secret);

    private static string Of(string text, string value, IEnumerable<string> parts)
    {
        var cleaned = value.Length == 0 ? text : text.Replace(value, Mark, StringComparison.Ordinal);
        foreach (var part in parts)
        {
            cleaned = OfToken(cleaned, part);
        }

        return cleaned;
    }

    // The cut dropped the kind of each line break, so the value and each part are searched as they are and with
    // their breaks as the one the lines are joined with.  A value that was in two lines leaves one.
    private static EquatableArray<string> OfLines(EquatableArray<string> lines, ConnectionSecret secret)
    {
        const string Break = "\n";
        var joined = string.Join(Break, lines);
        var cleaned = Of(
            Of(joined, secret),
            secret.Value.ReplaceLineEndings(Break),
            secret.Parts.Select(static part => part.ReplaceLineEndings(Break))
        );
        return string.Equals(cleaned, joined, StringComparison.Ordinal)
            ? lines
            : new EquatableArray<string>([.. cleaned.Split(Break)]);
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
