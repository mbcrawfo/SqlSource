using System;
using System.Collections.Generic;
using TypeNameFound = SqlSource.Tool.Describing.Describers.DescribeFailures.TypeNameFound;

namespace SqlSource.Tool.Describing.Postgres.Statements;

/// <summary>
/// Reads the type of a <c>-- param:</c> marker against the grammar of a type name, before it is put into a
/// <c>PREPARE</c>.
/// </summary>
/// <remarks>
/// <para>
/// A marker's type is everything to the end of its line, and the tool interpolates it into SQL it sends.  So
/// nothing leaves the tool that is not written as a type: a name, one bare identifier or one quoted one, or two
/// joined by a dot; then any of PostgreSQL's own type words, with one modifier in parentheses, of numbers and
/// bare words, before, among or after them; then any number of <c>[]</c>.  A comment, a <c>;</c>, a quote left
/// open, a <c>)</c> without its <c>(</c> and any other bare word are refused, each with what was found.
/// </para>
/// <para>
/// It does not say whether the database has the type: the server does, and checks it against every use.
/// </para>
/// </remarks>
internal static class DeclaredTypeText
{
    public const string NotClosed = "a quote or a parenthesis is not closed";

    public const string Modifier = "a modifier in parentheses holds numbers and plain words";

    public const string Shape = "a type name is a name, then type words and one modifier in parentheses, then []";

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
    /// The text to put into the type list, without the spaces around it, or why it is refused:
    /// <see cref="TypeNameFound.Reason" /> of what was found at the first place the text leaves the grammar.
    /// </summary>
    public static Outcome<string, string> Read(string text)
    {
        var position = 0;
        var found =
            ReadName(text, ref position)
            ?? ReadWordsAndModifier(text, ref position)
            ?? ReadArrayMarks(text, ref position);
        return found is null
            ? Outcome<string, string>.Of(text.Trim(' ', '\t'))
            : Outcome<string, string>.Failed(TypeNameFound.Reason(found));
    }

    // One identifier, or two joined by a dot.  Null when it was read, else what was found.  A dot with no identifier
    // on one side of it is a count of parts.
    private static string? ReadName(string text, ref int position)
    {
        var found = ReadIdentifier(text, ref position);
        if (found is not null)
        {
            return found;
        }

        SkipSpaces(text, ref position);
        if (!IsAt(text, position, '.'))
        {
            return null;
        }

        position++;
        SkipSpaces(text, ref position);
        return position == text.Length ? TypeNameFound.Parts : ReadIdentifier(text, ref position);
    }

    // Type words and at most one modifier, in any order, up to the first array mark or the end.  A dot where a type
    // word is expected is a third part.
    private static string? ReadWordsAndModifier(string text, ref int position)
    {
        var hasModifier = false;
        while (true)
        {
            SkipSpaces(text, ref position);
            if (position == text.Length || text[position] == '[')
            {
                return null;
            }

            if (text[position] == '(' && !hasModifier)
            {
                var found = ReadModifier(text, ref position);
                if (found is not null)
                {
                    return found;
                }

                hasModifier = true;
                continue;
            }

            if (text[position] == '.')
            {
                return TypeNameFound.Parts;
            }

            var start = position;
            while (position < text.Length && IsBare(text[position]))
            {
                position++;
            }

            if (position == start || !TypeWords.Contains(text[start..position]))
            {
                return TypeNameFound.At(text, start, Shape);
            }
        }
    }

    // "(" with signed integers and bare ASCII words, separated by commas and spaces, up to ")".  A "-" stands before
    // a digit, so that two cannot start a comment; a word is ASCII, as PostGIS's are.
    private static string? ReadModifier(string text, ref int position)
    {
        var index = position + 1;
        while (index < text.Length)
        {
            var value = text[index];
            if (value == ')')
            {
                position = index + 1;
                return null;
            }

            if (value is ',' or ' ')
            {
                index++;
            }
            else if (
                char.IsAsciiDigit(value)
                || (value == '-' && index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]))
            )
            {
                index++;
                while (index < text.Length && char.IsAsciiDigit(text[index]))
                {
                    index++;
                }

                // A number ends before a letter or a "_", so that "4326Point" and "4326_point" are neither.
                if (index < text.Length && IsAsciiWordPart(text[index]) && !char.IsAsciiDigit(text[index]))
                {
                    return Modifier;
                }
            }
            else if (char.IsAsciiLetter(value) || value == '_')
            {
                while (index < text.Length && IsAsciiWordPart(text[index]))
                {
                    index++;
                }
            }
            else
            {
                return TypeNameFound.At(text, index, Modifier);
            }
        }

        return NotClosed;
    }

    private static bool IsAsciiWordPart(char value) => char.IsAsciiLetterOrDigit(value) || value == '_';

    // Any number of "[]", and then the end.
    private static string? ReadArrayMarks(string text, ref int position)
    {
        while (position < text.Length)
        {
            if (text[position] != '[')
            {
                return TypeNameFound.At(text, position, Shape);
            }

            if (!IsAt(text, position + 1, ']'))
            {
                return TypeNameFound.At(text, position + 1, Shape);
            }

            position += 2;
            SkipSpaces(text, ref position);
        }

        return null;
    }

    // A bare identifier or a quoted one, after any spaces.  A dot where one is expected is a count of parts.
    private static string? ReadIdentifier(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (IsAt(text, position, '"'))
        {
            return ReadQuoted(text, ref position);
        }

        if (IsAt(text, position, '.'))
        {
            return TypeNameFound.Parts;
        }

        var start = position;
        while (position < text.Length && IsBare(text[position]))
        {
            position++;
        }

        return position > start ? null : TypeNameFound.At(text, position, Shape);
    }

    // A quoted identifier, with "" for a quote.  It ends on its line and holds no NUL, and what stands first of a line
    // break, a NUL and the end of the text is what was found.
    private static string? ReadQuoted(string text, ref int position)
    {
        var closed = PostgresIdentifiers.SkipQuoted(text, position, out var end);
        var inside = text.AsSpan(position, end - position).IndexOfAny('\r', '\n', '\0');
        if (inside >= 0)
        {
            return text[position + inside] == '\0' ? Shape : TypeNameFound.OneLine;
        }

        if (!closed)
        {
            return NotClosed;
        }

        position = end;
        return null;
    }

    // Stricter than PostgreSQL's word rule (PostgresIdentifiers.IsWordPart) on purpose, and not to be replaced by it:
    // the text goes into a PREPARE, and with "$" a type of "1$$" would open a dollar quote there and "1$1" would be
    // a parameter.  A type whose name holds "$", or a character above U+007F that is no letter or digit, is written
    // quoted.
    private static bool IsBare(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static bool IsAt(string text, int position, char value) =>
        position < text.Length && text[position] == value;

    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }
    }
}
