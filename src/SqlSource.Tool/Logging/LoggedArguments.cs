using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Logging;

/// <summary>
/// The command line as <c>run.start</c> holds it: every token, with the value of each <c>--database</c> and each
/// <c>--connection</c> replaced, and each path that holds <c>=</c> or <c>;</c>.
/// </summary>
/// <remarks>
/// <para>
/// It is built from what <see cref="UsageCheck" /> read and not from the text: a token is replaced because it was
/// bound to the option, whichever way the option was written, <c>--connection x</c>, <c>--connection=x</c> or
/// <c>--connection:x</c>.  A replaced token keeps its option and its separator.  A command line that was wrong never
/// comes here: the run ended before the log was opened.
/// </para>
/// <para>
/// A path, which is an argument or the value of <c>--project</c>, is written only where a message may print it
/// (<see cref="UsageValue.MayBePrintedAsPath" />).  A <c>--database</c> value and a <c>--connection</c> value are
/// never written: <c>run.start</c> comes before the plan, which decides whether such a name would be printed.  The
/// <c>database</c> events and <c>run.end</c> name every selected database, and <c>connectionSource</c> says which got
/// its connection from <c>--connection</c>.
/// </para>
/// </remarks>
internal static class LoggedArguments
{
    public static LogValue Of(Usage usage, IReadOnlyList<string> args)
    {
        var logged = args.ToArray();
        foreach (var value in usage.Values.Where(IsReplaced))
        {
            // The token is the value, or the option, a separator and the value.
            var token = args[value.Position - 1];
            logged[value.Position - 1] = token[..(token.Length - value.Text.Length)] + Redaction.Mark;
        }

        return LogValue.ListOfTexts(logged);
    }

    private static bool IsReplaced(UsageValue value) =>
        value.Option?.Name switch
        {
            DescribeCommand.DatabaseOption or DescribeCommand.ConnectionOption => true,
            null or DescribeCommand.ProjectOption => !value.MayBePrintedAsPath,
            _ => false,
        };
}
