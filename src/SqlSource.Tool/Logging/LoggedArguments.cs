using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Logging;

/// <summary>
/// The command line as <c>run.start</c> holds it: every token, with the value of each <c>--connection</c>
/// replaced.
/// </summary>
/// <remarks>
/// It is built from what <see cref="UsageCheck" /> read and not from the text: a token is replaced because it was
/// bound to the option, whichever way the option was written, <c>--connection x</c>, <c>--connection=x</c> or
/// <c>--connection:x</c>.  A command line that was wrong never comes here: the run ended before the log was opened.
/// </remarks>
internal static class LoggedArguments
{
    public static LogValue Of(Usage usage, IReadOnlyList<string> args)
    {
        var logged = args.ToArray();
        foreach (
            var value in usage.Values.Where(static value => value.Option?.Name == DescribeCommand.ConnectionOption)
        )
        {
            // The token is the value, or the option, a separator and the value.
            var token = args[value.Position - 1];
            logged[value.Position - 1] = token[..(token.Length - value.Text.Length)] + WithoutItsValue(value.Text);
        }

        return LogValue.ListOfTexts(logged);
    }

    // The name stays, so that a reader sees which database was given a connection.
    private static string WithoutItsValue(string text) =>
        ConnectionArgument.TryParse(text, out var connection) ? connection.Name + "=" + Redaction.Mark : Redaction.Mark;
}
