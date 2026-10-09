using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

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
    /// The lines to write, each a whole message.  Empty for a command line with nothing wrong.
    /// </summary>
    public static IReadOnlyList<string> Check(Command root, IReadOnlyList<string> args)
    {
        var messages = new List<string>();
        var command = root;
        var options = root.Options.ToList();
        var arguments = 0;

        var index = 0;
        while (index < args.Count)
        {
            var token = args[index++];
            if (token.StartsWith('-'))
            {
                var name = NameOf(token);
                var option = options.Find(option => option.Name == name || option.Aliases.Contains(name));
                if (option is null)
                {
                    // The token after a misspelt option may be its value, so nothing after it is read, and nothing
                    // found before it is reported beside it.
                    return [$"sqlsource: unknown option '{name}'"];
                }

                var hasValue = token.Length > name.Length;
                if (option.Arity.MaximumNumberOfValues == 0)
                {
                    if (hasValue)
                    {
                        messages.Add($"sqlsource: option '{name}' takes no value");
                    }
                }
                else if (!hasValue && option.Arity.MinimumNumberOfValues > 0)
                {
                    // The next token is the value, whatever it starts with.
                    if (index == args.Count)
                    {
                        messages.Add($"sqlsource: option '{name}' needs a value");
                    }

                    index++;
                }
            }
            else if (arguments == 0 && command.Subcommands.FirstOrDefault(sub => sub.Name == token) is { } sub)
            {
                command = sub;
                options = [.. options.Where(static option => option.Recursive), .. sub.Options];
            }
            else if (arguments < command.Arguments.Sum(static argument => argument.Arity.MaximumNumberOfValues))
            {
                arguments++;
            }
            else
            {
                messages.Add($"sqlsource: unexpected argument at position {index}");
            }
        }

        return messages;
    }

    private static string NameOf(string token)
    {
        var end = token.AsSpan().IndexOfAny('=', ':');
        return end < 0 ? token : token[..end];
    }
}
