using System.Globalization;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Tests;

// One error as a test compares it: the id, the file, where in it, counted from zero, and the message.
internal sealed record Reported(string Id, string? Path, LinePosition? Position, string Message)
{
    public static Reported Of(ToolDiagnostic error) =>
        new(error.Descriptor.Id, error.Path, error.Position, MessageOf(error));

    // A diagnostic of the compiler keeps its arguments to itself, so a tool error is compared by its message: the
    // descriptor's format with the error's arguments.
    public static string MessageOf(ToolDiagnostic error) =>
        string.Format(
            CultureInfo.InvariantCulture,
            error.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            [.. error.Arguments]
        );
}
