using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace SqlSource.Tool;

/// <summary>
/// How <see cref="UsageCheck" /> read a command line.
/// </summary>
/// <param name="Command">The command the line names: the root, or the last command of it.</param>
/// <param name="Arguments">The tokens that are arguments of that command, in order.</param>
/// <param name="Messages">What is wrong, each a whole line to write.  Empty when nothing is.</param>
internal sealed record Usage(Command Command, IReadOnlyList<string> Arguments, IReadOnlyList<string> Messages)
{
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
