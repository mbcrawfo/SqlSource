using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// The rules every check of a describer over a case shares, whatever the engine: that a description has the columns of
// the truth, and the text a failure is shown as.
internal static class RealisticDescriptions
{
    // The columns of a case that is not refused, which must be described, with as many columns as its truth and
    // each of the truth's name at its position.  A failure names the case on its first line.
    public static IReadOnlyList<SidecarColumn> ColumnsOf(
        RealisticCase found,
        Outcome<QueryDescription, DescribeFailure> described
    )
    {
        if (!described.Succeeded)
        {
            Assert.Fail($"{found} was refused:\n{RenderFailure(described.Failure)}");
        }

        IReadOnlyList<SidecarColumn> columns = described.Value.Columns ?? [];
        if (columns.Count != found.Columns.Count)
        {
            Assert.Fail($"{found} is described with {columns.Count} columns, and its truth has {found.Columns.Count}");
        }

        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Name != found.Columns[index].Name)
            {
                Assert.Fail(
                    $"{found} column {index} is described as {columns[index].Name}, and its truth names it "
                        + found.Columns[index].Name
                );
            }
        }

        return columns;
    }

    // The id, each argument, the help and each line of the server of a failure, a line for each, for a message.
    public static string RenderFailure(DescribeFailure failure)
    {
        var text = new StringBuilder();
        Line(text, $"failure: {failure.Descriptor.Id}");
        foreach (var argument in failure.Arguments)
        {
            Line(text, $"argument: {argument}");
        }

        if (failure.Help is { } help)
        {
            Line(text, $"help: {help}");
        }

        foreach (var line in failure.ServerLines)
        {
            Line(text, $"server: {line}");
        }

        return text.ToString();
    }

    private static void Line(StringBuilder text, FormattableString line) =>
        text.Append(line.ToString(CultureInfo.InvariantCulture)).Append('\n');
}
