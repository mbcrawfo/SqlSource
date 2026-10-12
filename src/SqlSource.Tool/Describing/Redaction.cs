using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Takes a connection's value out of a text that a describer gave, before the text is printed.
/// </summary>
/// <remarks>
/// A describer's rule is to put the value in nothing it returns.  The run does not rely on it: a driver's message
/// for a connection string that is wrong can quote the string.  The whole value is replaced and nothing less: the
/// tool does not read a connection string, so it knows no part of one.
/// </remarks>
internal static class Redaction
{
    /// <summary>What stands where the value was.</summary>
    public const string Mark = "***";

    public static string Of(string text, string secret) =>
        secret.Length == 0 ? text : text.Replace(secret, Mark, StringComparison.Ordinal);

    /// <summary>
    /// The failure with the value taken out of its descriptor and of what the server said.  Its arguments and its
    /// help are cleaned with the error that is made of it.
    /// </summary>
    /// <remarks>
    /// A describer cuts what a server said into lines, so a value that holds a line break is in several of them and
    /// in none whole: the lines are searched joined.  A descriptor is a describer's text when the describer built
    /// it from what a server said.
    /// </remarks>
    public static DescribeFailure Of(DescribeFailure failure, string secret) =>
        secret.Length == 0
            ? failure
            : failure with
            {
                Descriptor = OfDescriptor(failure.Descriptor, secret),
                ServerLines = OfLines(failure.ServerLines, secret),
            };

    /// <summary>The error with the value taken out of its arguments and of the text of each line under it.</summary>
    public static ToolDiagnostic Of(ToolDiagnostic diagnostic, string secret) =>
        diagnostic with
        {
            Arguments = new EquatableArray<string>([.. diagnostic.Arguments.Select(argument => Of(argument, secret))]),
            Lines = new EquatableArray<ContinuationLine>([
                .. diagnostic.Lines.Select(line => line with { Text = Of(line.Text, secret) }),
            ]),
        };

    // The same descriptor when no text of it holds the value, which is every descriptor but a broken describer's.
    private static DiagnosticDescriptor OfDescriptor(DiagnosticDescriptor descriptor, string secret)
    {
        var culture = CultureInfo.InvariantCulture;
        var title = descriptor.Title.ToString(culture);
        var format = descriptor.MessageFormat.ToString(culture);
        var description = descriptor.Description.ToString(culture);
        string[] texts = [descriptor.Id, title, format, descriptor.Category, description, descriptor.HelpLinkUri];
        return texts.Any(text => text.Contains(secret, StringComparison.Ordinal))
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

    // The cut dropped the kind of each line break, so the value is searched as it is and with its breaks as the
    // one the lines are joined with.  A value that was in two lines leaves one.
    private static EquatableArray<string> OfLines(EquatableArray<string> lines, string secret)
    {
        const string Break = "\n";
        var joined = string.Join(Break, lines);
        var cleaned = Of(Of(joined, secret), secret.ReplaceLineEndings(Break));
        return string.Equals(cleaned, joined, StringComparison.Ordinal)
            ? lines
            : new EquatableArray<string>([.. cleaned.Split(Break)]);
    }
}
