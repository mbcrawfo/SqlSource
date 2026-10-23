using System;
using System.Collections.Generic;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres.Statements;

/// <summary>
/// Reads the type of a <c>-- param:</c> marker against the grammar of a type name, before it is put into a
/// <c>PREPARE</c>.
/// </summary>
/// <remarks>
/// <para>
/// A marker's type is everything to the end of its line, and the tool interpolates it into SQL it sends.  So
/// nothing leaves the tool that is not written as a type: a name, one bare identifier or one quoted one, or two
/// joined by a dot; then any of PostgreSQL's own type words, with one modifier in parentheses before, among or
/// after them; then any number of <c>[]</c>.  A comment, a <c>;</c>, a quote left open, a <c>)</c> without its
/// <c>(</c> and any other bare word are refused.
/// </para>
/// <para>
/// It does not say whether the database has the type: the server does, and checks it against every use.
/// </para>
/// </remarks>
internal static class DeclaredTypeText
{
    // The words of PostgreSQL's multi-word type names, compared ignoring case.
    private static readonly HashSet<string> TypeWords = new(
        [
            "varying",
            "precision",
            "national",
            "character",
            "with",
            "without",
            "time",
            "zone",
            "year",
            "month",
            "day",
            "hour",
            "minute",
            "second",
            "to",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// The text to put into the type list, without the spaces around it, or
    /// <see cref="DescribeFailures.NotATypeName" /> for a text that is not written as a type name.
    /// </summary>
    public static Outcome<string, string> Read(string text) =>
        IsTypeName(text)
            ? Outcome<string, string>.Of(text.Trim(' ', '\t'))
            : Outcome<string, string>.Failed(DescribeFailures.NotATypeName);

    private static bool IsTypeName(string text)
    {
        var position = 0;
        if (!TryReadIdentifier(text, ref position))
        {
            return false;
        }

        SkipSpaces(text, ref position);
        if (position < text.Length && text[position] == '.')
        {
            position++;
            if (!TryReadIdentifier(text, ref position))
            {
                return false;
            }
        }

        if (!TryReadWordsAndModifier(text, ref position) || !TryReadArrayMarks(text, ref position))
        {
            return false;
        }

        return true;
    }

    // Type words and at most one modifier, in any order, up to the first array mark or the end.
    private static bool TryReadWordsAndModifier(string text, ref int position)
    {
        var hasModifier = false;
        while (true)
        {
            SkipSpaces(text, ref position);
            if (position == text.Length || text[position] == '[')
            {
                return true;
            }

            if (text[position] == '(')
            {
                if (hasModifier || !TryReadModifier(text, ref position))
                {
                    return false;
                }

                hasModifier = true;
            }
            else
            {
                var start = position;
                while (position < text.Length && IsBare(text[position]))
                {
                    position++;
                }

                if (position == start || !TypeWords.Contains(text[start..position]))
                {
                    return false;
                }
            }
        }
    }

    // "(" with digits, commas and spaces up to ")".  A "-" stands before a digit, so that two cannot start a comment.
    private static bool TryReadModifier(string text, ref int position)
    {
        for (var index = position + 1; index < text.Length; index++)
        {
            var value = text[index];
            if (value == ')')
            {
                position = index + 1;
                return true;
            }

            var isSign = value == '-' && index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]);
            if (!char.IsAsciiDigit(value) && value is not (',' or ' ') && !isSign)
            {
                return false;
            }
        }

        return false;
    }

    // Any number of "[]", and then the end.
    private static bool TryReadArrayMarks(string text, ref int position)
    {
        while (position < text.Length)
        {
            if (text[position] != '[' || position + 1 == text.Length || text[position + 1] != ']')
            {
                return false;
            }

            position += 2;
            SkipSpaces(text, ref position);
        }

        return true;
    }

    private static bool TryReadIdentifier(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (position < text.Length && text[position] == '"')
        {
            return TryReadQuoted(text, ref position);
        }

        var start = position;
        while (position < text.Length && IsBare(text[position]))
        {
            position++;
        }

        return position > start;
    }

    // A quoted identifier, with "" for a quote.  It ends on its line.
    private static bool TryReadQuoted(string text, ref int position)
    {
        if (
            !PostgresIdentifiers.SkipQuoted(text, position, out var end)
            || text.AsSpan(position, end - position).IndexOfAny('\r', '\n', '\0') >= 0
        )
        {
            return false;
        }

        position = end;
        return true;
    }

    // Stricter than PostgreSQL's word rule (PostgresIdentifiers.IsWordPart) on purpose, and not to be replaced by it:
    // the text goes into a PREPARE, and with "$" a type of "1$$" would open a dollar quote there and "1$1" would be
    // a parameter.  A type whose name holds "$", or a character above U+007F that is no letter or digit, is written
    // quoted.
    private static bool IsBare(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }
    }
}
