using System;
using System.Linq;
using SqlSource.Diagnostics;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// An exception as <c>SQLSRC200</c>.
/// </summary>
internal static class UnexpectedFailure
{
    /// <summary>
    /// The error of an exception: its type and its message, and its stack trace as lines labelled <c>trace</c>
    /// when <c>SQLSOURCE_DEBUG</c> is set and not empty.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="getEnvironmentVariable">The host's.</param>
    /// <param name="redact">
    /// Applied to the message, and to the whole text of the stack trace before it is cut into lines, so that a
    /// text which spans several lines is replaced whole.  Null leaves the texts as they are.
    /// </param>
    public static ToolDiagnostic Of(
        Exception exception,
        Func<string, string?> getEnvironmentVariable,
        Func<string, string>? redact = null
    )
    {
        redact ??= static text => text;
        var failure = ToolDiagnostic.Create(
            ToolDiagnostics.UnexpectedFailure,
            exception.GetType().FullName ?? exception.GetType().Name,
            redact(exception.Message)
        );
        if (string.IsNullOrEmpty(getEnvironmentVariable(Cli.DebugVariable)))
        {
            return failure;
        }

        var trace = redact(exception.ToString())
            .Split('\n')
            .Select(static line => new ContinuationLine("trace", line.TrimEnd('\r')));
        return failure.WithLines([.. trace]);
    }
}
