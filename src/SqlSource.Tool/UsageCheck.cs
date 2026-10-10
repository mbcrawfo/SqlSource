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
/// System.CommandLine's own messages repeat the token they reject, and a token may be a secret: a misspelt
/// <c>--conection</c> is followed by a connection string.  So the tool writes these lines itself.  A line names an
/// option the tool has, or the name of an unknown option cut at its first <c>=</c> or <c>:</c>, or a position, and
/// never any other text of the command line.
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
        var command = root;
        var options = root.Options.ToList();

        var index = 0;
        while (index < args.Count)
        {
            var token = args[index++];
            if (token.StartsWith('-'))
            {
                var name = NameOf(token);
                var option = Find(options, name);
                if (option is null)
                {
                    // The token after a misspelt option may be its value, so nothing after it is read, and nothing
                    // found before it is reported beside it.
                    return new Usage(command, arguments, [$"sqlsource: unknown option '{OneLine.Of(name)}'"]);
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
            }
            else
            {
                messages.Add($"sqlsource: unexpected argument at position {index}");
            }
        }

        return new Usage(command, arguments, messages);
    }

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
