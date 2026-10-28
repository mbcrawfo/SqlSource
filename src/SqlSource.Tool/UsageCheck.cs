using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool;

/// <summary>
/// Finds what is wrong with a command line, before System.CommandLine reads it.
/// </summary>
/// <remarks>
/// <para>
/// System.CommandLine's own messages repeat the token they reject, and a token may be a secret: a misspelt
/// <c>--conection</c> is followed by a connection string.  So the tool writes these lines itself.  A line names an
/// option the tool has, or the name of an unknown option that is close to one the tool has, cut at its first
/// <c>=</c> or <c>:</c>, or a position, and never any other text of the command line.  The lines have no id and do
/// not go through the reporter.  <c>UsageCheckTests</c> holds the rule for an option that takes a value.
/// </para>
/// <para>
/// It and System.CommandLine must read a command line the same way: <c>Cli</c> runs nothing unless System.CommandLine
/// found the same command and arguments (<c>Usage.IsReadTheSameBy</c>), and gave <c>--project</c>,
/// <c>--database</c>, <c>--connection</c> and <c>--log</c> the same values in the same order
/// (<c>DescribeCommand.ReadsValuesAs</c>).  A token that one takes for the value of an option and the other for the
/// path is printed as a path: what is left of that risk is
/// <c>docs/tech-debt/TD-0023-path-without-an-equals-sign-is-printed-as-given.md</c>.
/// </para>
/// <para>
/// <c>Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine</c> holds the two together over every command
/// line of a few tokens, and a new kind of option adds its tokens to that test's vocabulary.  An option of the tool
/// itself is added to that of <c>Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine</c>, which
/// runs the tool's own commands and also compares the values that each reader gives an option.  A disagreement
/// either finds is fixed here, by reading as System.CommandLine does or by rejecting more.
/// </para>
/// </remarks>
internal static class UsageCheck
{
    /// <summary>
    /// Reads a command line: the command it names, that command's arguments, and what is wrong with it.
    /// </summary>
    public static Usage Check(Command root, IReadOnlyList<string> args)
    {
        var messages = new List<string>();
        var arguments = new List<string>();
        var values = new List<UsageValue>();
        // An option that takes one value takes it once: System.CommandLine rejects a second with the value's text.
        var givenOnce = new HashSet<Option>();
        var command = root;
        var options = root.Options.ToList();

        void AddValue(Option option, string name, string text, int position)
        {
            if (option.Arity.MaximumNumberOfValues == 1 && !givenOnce.Add(option))
            {
                messages.Add($"sqlsource: option '{name}' is given more than once");
                return;
            }

            values.Add(new UsageValue(option, text, position));
        }

        var index = 0;
        while (index < args.Count)
        {
            // After this, index is the position of the token counted from one.
            var token = args[index++];
            if (token.StartsWith('-'))
            {
                var name = NameOf(token);
                var option = Find(options, name);
                if (option is null)
                {
                    // The token after a misspelt option may be its value, so nothing after it is read, and nothing
                    // found before it is reported beside it.  The token itself may be a secret typed as an option,
                    // as -pS3cret is, so it is named only when it is a misspelling of an option of the tool.
                    var line = IsNearAnOption(root, name)
                        ? $"sqlsource: unknown option '{OneLine.Of(name)}'"
                        : $"sqlsource: unknown option at position {index}";
                    return new Usage(command, arguments, [], [line]);
                }

                var hasSeparator = token.Length > name.Length;
                if (option.Arity.MaximumNumberOfValues == 0)
                {
                    if (hasSeparator)
                    {
                        messages.Add($"sqlsource: option '{name}' takes no value");
                    }
                }
                else if (hasSeparator)
                {
                    // "--name=" is what --name="$UNSET" gives.  System.CommandLine reads it as an option without a
                    // value and takes the next token for one, so every token after it would be read one place off.
                    if (token.Length == name.Length + 1)
                    {
                        messages.Add($"sqlsource: option '{name}' needs a value");
                    }
                    else
                    {
                        AddValue(option, name, token[(name.Length + 1)..], index);
                    }
                }
                else if (option.Arity.MinimumNumberOfValues > 0)
                {
                    // The next token is the value, whatever it starts with, unless it names an option of this
                    // command or is "--": System.CommandLine takes neither for a value.
                    if (index == args.Count || IsOption(args[index], options))
                    {
                        messages.Add($"sqlsource: option '{name}' needs a value");
                    }
                    else
                    {
                        AddValue(option, name, args[index], index + 1);
                        index++;
                    }
                }
            }
            else if (arguments.Count == 0 && command.Subcommands.FirstOrDefault(sub => sub.Name == token) is { } sub)
            {
                command = sub;
                options = [.. options.Where(static option => option.Recursive), .. sub.Options];
            }
            else if (arguments.Count < command.Arguments.Sum(static argument => argument.Arity.MaximumNumberOfValues))
            {
                arguments.Add(token);
                values.Add(new UsageValue(null, token, index));
            }
            else
            {
                messages.Add($"sqlsource: unexpected argument at position {index}");
            }
        }

        return new Usage(command, arguments, values, messages);
    }

    // The names and aliases of every option of the tool, whichever command the walk is in, so that --forse is named
    // before the command, where --force is unknown, as it is after it.
    private static bool IsNearAnOption(Command command, string name) =>
        command.Options.Any(option =>
            NearName.IsNear(name, option.Name, ignoreCase: false)
            || option.Aliases.Any(alias => NearName.IsNear(name, alias, ignoreCase: false))
        ) || command.Subcommands.Any(sub => IsNearAnOption(sub, name));

    private static Option? Find(List<Option> options, string name) =>
        options.Find(option => option.Name == name || option.Aliases.Contains(name));

    private static bool IsOption(string token, List<Option> options) =>
        token == "--" || (token.StartsWith('-') && Find(options, NameOf(token)) is not null);

    private static string NameOf(string token)
    {
        var end = token.AsSpan().IndexOfAny('=', ':');
        return end < 0 ? token : token[..end];
    }
}
