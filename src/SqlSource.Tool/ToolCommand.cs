using System;
using System.Collections.Generic;
using System.CommandLine;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// A command of the tool as <see cref="Cli" /> runs it: what each step of a run asks of the command.  Each member is
/// required, so a new one is named by every command.
/// </summary>
internal sealed record ToolCommand
{
    /// <summary>The command's name, which <see cref="SharedOptions.Runs" /> tests.</summary>
    public required string Name { get; init; }

    /// <summary>Makes the command, with its arguments and options, for <c>Cli.BuildCommands</c>.</summary>
    public required Func<Command> Create { get; init; }

    /// <summary>
    /// What is wrong with a command line by the command's own rules, each a whole line to write.  Empty for another
    /// command.
    /// </summary>
    public required Func<Usage, IReadOnlyList<string>> CheckUsage { get; init; }

    /// <summary>
    /// Whether <see cref="UsageCheck" /> and System.CommandLine gave the command's options the same values, in the
    /// same order.  True for another command.
    /// </summary>
    public required Func<Usage, ParseResult, bool> ReadsValuesAs { get; init; }

    /// <summary>Whether a run of the command reads <c>SQLSOURCE_LOG</c> when it is given no <c>--log</c>.</summary>
    public required Func<ParseResult, bool> ReadsLogVariable { get; init; }

    /// <summary>
    /// Whether the run's reporter writes every error on one line, as the build that runs the tool reads it.
    /// </summary>
    public required Func<ParseResult, bool> WritesOneLine { get; init; }

    /// <summary>
    /// Whether a failed run of the command ends with <c>Cli.RunAgainLine</c> when it has no log.
    /// </summary>
    public required Func<ParseResult, bool> MayPoint { get; init; }

    /// <summary>
    /// Gives the command that the command line runs its action, once the run has its reporter: with the command line,
    /// as each reader read it, the host, the reporter, and what is given the counts of each selected database, as
    /// <c>run.end</c> holds them.
    /// </summary>
    public required Action<ParseResult, Usage, ToolHost, Reporter, Action<LogValue>> Bind { get; init; }
}
