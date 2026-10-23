using System.Collections.Generic;
using System.Globalization;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Testing;

// What a test reads of a describer's failure.
internal static class Failures
{
    // Every text of a failure that a run prints or logs.
    public static IReadOnlyList<string> TextsOf(DescribeFailure failure) =>
        [
            failure.Descriptor.Id,
            failure.Descriptor.Title.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            .. failure.Arguments,
            .. failure.ServerLines,
            failure.Help ?? string.Empty,
        ];

    // The message of the failure: the descriptor's format with the arguments.
    public static string MessageOf(DescribeFailure failure) =>
        string.Format(
            CultureInfo.InvariantCulture,
            failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            [.. failure.Arguments]
        );
}
