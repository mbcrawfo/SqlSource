using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using SqlSource.Tool.DotEnv;

namespace SqlSource.Tool;

/// <summary>
/// How <see cref="UsageCheck" /> read a command line.
/// </summary>
/// <param name="Command">The command the line names: the root, or the last command of it.</param>
/// <param name="Arguments">The tokens that are arguments of that command, in order.</param>
/// <param name="Values">The arguments and the values of options, in order, each with its position.</param>
/// <param name="Messages">What is wrong, each a whole line to write.  Empty when nothing is.</param>
internal sealed record Usage(
    Command Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<UsageValue> Values,
    IReadOnlyList<string> Messages
)
{
    /// <summary>The options that take no value and were given, in the order they were first given, once each.</summary>
    public IReadOnlyList<Option> Flags { get; init; } = [];

    /// <summary>
    /// The same command line with each path judged by <see cref="PathToken.MayBePrinted" />.  Under <c>--online</c>
    /// the paths of <c>--manifest</c>, <c>--log</c> and <c>--env-file</c> are the build's, which the owner ruled no
    /// secret.  The word <c>none</c> of <c>--env-file</c> is no path, and <c>--query</c> is judged by its file.
    /// </summary>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    public Usage WithPathsJudgedFrom(string workingDirectory)
    {
        var isOnline = Values.Any(static value => value.Option?.Name == DescribeCommand.OnlineOption);
        return this with
        {
            Values =
            [
                .. Values.Select(value =>
                    value with
                    {
                        MayBePrintedAsPath = MayBePrinted(value, workingDirectory, isOnline),
                    }
                ),
            ],
        };
    }

    private static bool MayBePrinted(UsageValue value, string workingDirectory, bool isOnline) =>
        value.Option?.Name switch
        {
            null or SharedOptions.Project or DiagnoseCommand.OutOption or DiagnoseCommand.ScrubOption =>
                PathToken.MayBePrinted(value.Text, workingDirectory),
            SharedOptions.Log or DescribeCommand.ManifestOption => isOnline
                || PathToken.MayBePrinted(value.Text, workingDirectory),
            SharedOptions.EnvFile => isOnline
                || string.Equals(value.Text.Trim(), DotEnvSearch.NoneValue, StringComparison.OrdinalIgnoreCase)
                || PathToken.MayBePrinted(value.Text, workingDirectory),
            DiagnoseCommand.QueryOption => !value.HoldsConnectionText
                && PathToken.MayBePrinted(FileOfQuery(value.Text), workingDirectory),
            _ => false,
        };

    // The file of a value of --query, before its last colon.
    private static string FileOfQuery(string text) =>
        text.LastIndexOf(':') is >= 0 and var colon ? text[..colon] : text;

    /// <summary>
    /// Whether System.CommandLine found the same command with the same arguments.  When it did not, a token that
    /// <see cref="UsageCheck" /> took for the value of an option may be an argument, and an argument is printed.
    /// </summary>
    public bool IsReadTheSameBy(ParseResult parsed) =>
        ReferenceEquals(parsed.CommandResult.Command, Command)
        && parsed
            .CommandResult.Children.OfType<ArgumentResult>()
            .SelectMany(static argument => argument.Tokens)
            .Select(static token => token.Value)
            .SequenceEqual(Arguments);
}
