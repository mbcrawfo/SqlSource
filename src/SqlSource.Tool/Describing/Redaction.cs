using System;
using System.Linq;
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

    /// <summary>The error with the value taken out of its arguments and of the text of each line under it.</summary>
    public static ToolDiagnostic Of(ToolDiagnostic diagnostic, string secret) =>
        diagnostic with
        {
            Arguments = new EquatableArray<string>([.. diagnostic.Arguments.Select(argument => Of(argument, secret))]),
            Lines = new EquatableArray<ContinuationLine>([
                .. diagnostic.Lines.Select(line => line with { Text = Of(line.Text, secret) }),
            ]),
        };
}
