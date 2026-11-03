using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.DotEnv;

/// <summary>
/// Reads the tool's connection variables from the text of a <c>.env</c> file: the subset of the format that
/// matters for one line, <c>KEY=value</c>, with an optional <c>export</c>, quotes and comments.
/// </summary>
/// <remarks>
/// A line that is not one of the tool's, whatever it holds, is passed over without a word, since the file is
/// shared with other programs.  A line of the tool's that cannot be read is an error that names its key and why,
/// and never any part of its value, which is a secret.
/// </remarks>
internal static class DotEnvParser
{
    private const string Export = "export";

    /// <summary>
    /// Whether <paramref name="key" /> is a variable of the tool: <c>SQLSOURCE_CONNECTION</c> or that and <c>_</c>
    /// and more, written as a name of ASCII letters, digits and <c>_</c> that does not start with a digit.
    /// </summary>
    public static bool IsToolKey(string key) =>
        IsName(key)
        && (
            key.Equals(ConnectionVariables.Unnamed, StringComparison.Ordinal)
            || key.StartsWith(ConnectionVariables.NamedPrefix, StringComparison.Ordinal)
        );

    /// <summary>Reads the text of a file.</summary>
    public static DotEnvContents Parse(string text)
    {
        var values = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var errors = ImmutableArray.CreateBuilder<DotEnvLineError>();

        if (text.StartsWith('﻿'))
        {
            text = text[1..];
        }

        var lines = text.Split('\n');
        for (var number = 0; number < lines.Length; number++)
        {
            ParseLine(lines[number].TrimEnd('\r'), number, values, errors);
        }

        return new DotEnvContents(values.ToImmutable(), errors.ToImmutable());
    }

    private static void ParseLine(
        string line,
        int number,
        ImmutableDictionary<string, string>.Builder values,
        ImmutableArray<DotEnvLineError>.Builder errors
    )
    {
        var column = SkipWhiteSpace(line, 0);
        if (column == line.Length || line[column] == '#')
        {
            return;
        }

        if (
            line.AsSpan(column).StartsWith(Export, StringComparison.Ordinal)
            && column + Export.Length < line.Length
            && char.IsWhiteSpace(line[column + Export.Length])
        )
        {
            column = SkipWhiteSpace(line, column + Export.Length);
        }

        var separator = line.IndexOf('=', column);
        if (separator < 0)
        {
            var bare = line[column..].Trim();
            if (IsToolKey(bare))
            {
                errors.Add(new DotEnvLineError(number, column, bare, "it has no '='"));
            }

            return;
        }

        var key = line[column..separator].TrimEnd();
        if (!IsToolKey(key))
        {
            // A key that starts like the tool's but is not a name is a mistake worth naming, one that is a name
            // and not the tool's, such as SQLSOURCE_CONNECTIONX, belongs to another program.
            if (key.StartsWith(ConnectionVariables.Unnamed, StringComparison.Ordinal) && !IsName(key))
            {
                errors.Add(new DotEnvLineError(number, column, LeadingName(key), "the key is not a valid name"));
            }

            return;
        }

        var reason = TryReadValue(line, separator + 1, out var value);
        if (reason is null)
        {
            values[key] = value;
        }
        else
        {
            errors.Add(new DotEnvLineError(number, column, key, reason));
        }
    }

    /// <summary>Reads the value that starts after the <c>=</c>; the reason it cannot be read, or <c>null</c>.</summary>
    private static string? TryReadValue(string line, int start, out string value)
    {
        value = string.Empty;
        var begin = SkipWhiteSpace(line, start);
        if (begin == line.Length)
        {
            return null;
        }

        int end;
        switch (line[begin])
        {
            case '\'':
                end = line.IndexOf('\'', begin + 1);
                if (end < 0)
                {
                    return "a quote is not closed";
                }

                value = line[(begin + 1)..end];
                break;
            case '"':
                var reason = TryReadDoubleQuoted(line, begin + 1, out value, out end);
                if (reason is not null)
                {
                    return reason;
                }

                break;
            default:
                value = ReadUnquoted(line, start);
                return null;
        }

        return IsEndOfLine(line, end + 1) ? null : "text follows the closing quote";
    }

    /// <summary>
    /// Reads from just after an opening <c>"</c> to the next one that is not escaped; <paramref name="end" /> is the
    /// index of the closing quote.
    /// </summary>
    private static string? TryReadDoubleQuoted(string line, int start, out string value, out int end)
    {
        var text = new StringBuilder();
        value = string.Empty;
        end = -1;
        var index = start;
        while (index < line.Length)
        {
            var character = line[index];
            if (character == '"')
            {
                value = text.ToString();
                end = index;
                return null;
            }

            if (character != '\\')
            {
                _ = text.Append(character);
                index++;
                continue;
            }

            if (index + 1 == line.Length)
            {
                break;
            }

            var escaped = line[index + 1] switch
            {
                '\\' => '\\',
                '"' => '"',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => (char?)null,
            };
            if (escaped is null)
            {
                return "a backslash starts no escape that .env takes";
            }

            _ = text.Append(escaped.Value);
            index += 2;
        }

        return "a quote is not closed";
    }

    /// <summary>
    /// The value that starts at <paramref name="start" /> with no quote: it ends at a <c>#</c> that follows white
    /// space, which starts a comment, and is trimmed.
    /// </summary>
    private static string ReadUnquoted(string line, int start)
    {
        var end = line.Length;
        for (var index = start; index < line.Length; index++)
        {
            if (line[index] == '#' && index > start && char.IsWhiteSpace(line[index - 1]))
            {
                end = index;
                break;
            }
        }

        return line[start..end].Trim();
    }

    /// <summary>
    /// Whether only white space, or white space and a comment, is left from <paramref name="index" />.  A comment
    /// follows white space, as in an unquoted value.
    /// </summary>
    private static bool IsEndOfLine(string line, int index)
    {
        var next = SkipWhiteSpace(line, index);
        return next == line.Length || (next > index && line[next] == '#');
    }

    private static int SkipWhiteSpace(string line, int index)
    {
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>A name of ASCII letters, digits and <c>_</c> that does not start with a digit.</summary>
    private static bool IsName(string text)
    {
        if (text.Length == 0 || char.IsAsciiDigit(text[0]))
        {
            return false;
        }

        return text.All(IsNameCharacter);
    }

    private static string LeadingName(string key)
    {
        var length = 0;
        while (length < key.Length && IsNameCharacter(key[length]))
        {
            length++;
        }

        return key[..length];
    }

    private static bool IsNameCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';
}
