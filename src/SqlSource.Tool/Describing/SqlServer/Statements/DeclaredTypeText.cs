using System;
using SqlSource.Parsing.Quoting;
using TypeNameFound = SqlSource.Tool.Describing.Describers.DescribeFailures.TypeNameFound;

namespace SqlSource.Tool.Describing.SqlServer.Statements;

/// <summary>
/// Reads the type of a <c>-- param:</c> marker against T-SQL's grammar of a type name, before it is put into the
/// declarations the tool sends.
/// </summary>
/// <remarks>
/// <para>
/// A type is a name of one or two parts joined by a dot with no space around it, each part a plain identifier (a
/// letter of any script, <c>_</c>, <c>@</c> or <c>#</c>, then those, digits of any script and <c>$</c>, as the server
/// reads one), a bracketed one with <c>]]</c> for a bracket, or a double-quoted one with <c>""</c> for a quote;
/// then, after any spaces, <c>(max)</c> in any case or one or two numbers in parentheses; and nothing else but
/// spaces at either end.  A comment, a <c>;</c>, a second <c>(</c>, a quote or a bracket left open and a line break
/// are refused, each with what was found.  A plain identifier that holds a combining mark, a letter number or a
/// format character after its first character is refused, though the server takes it, and is written in brackets
/// (TD-0076).
/// </para>
/// <para>
/// It is the first refusal and not the guarantee: a declaration reaches the server only as the value of a parameter
/// of a procedure, which reads it as a list of declarations and refuses one that is not.  It does not say whether the
/// database has the type: the server does.
/// </para>
/// </remarks>
internal static class DeclaredTypeText
{
    public const string NotClosed = "a quote or a bracket is not closed";

    public const string Shape = "a type name is a name, then (max) or one or two numbers in parentheses";

    private const string Max = "max";

    /// <summary>
    /// The type without the spaces around it, or why it is refused: <see cref="TypeNameFound.Reason" /> of what was
    /// found at the first place the text leaves the grammar.
    /// </summary>
    public static Outcome<string, string> Read(string text)
    {
        var position = 0;
        var found = ReadName(text, ref position) ?? ReadModifier(text, ref position) ?? ReadEnd(text, position);
        return found is null
            ? Outcome<string, string>.Of(text.Trim(' ', '\t'))
            : Outcome<string, string>.Failed(TypeNameFound.Reason(found));
    }

    // One part, or two joined by a dot.  Null when it was read, else what was found.  A dot with no part on one side
    // of it, or a third part, is a count of parts.
    private static string? ReadName(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (IsAt(text, position, '.'))
        {
            return TypeNameFound.Parts;
        }

        var found = ReadPart(text, ref position);
        if (found is not null || !IsAt(text, position, '.'))
        {
            return found;
        }

        position++;
        if (position == text.Length || (text[position] is ' ' or '\t' or '.'))
        {
            return TypeNameFound.Parts;
        }

        return ReadPart(text, ref position) ?? (IsAt(text, position, '.') ? TypeNameFound.Parts : null);
    }

    // A plain identifier, a bracketed one or a double-quoted one.
    private static string? ReadPart(string text, ref int position)
    {
        if (IsAt(text, position, '['))
        {
            return ReadDelimited(text, ref position, ']');
        }

        if (IsAt(text, position, '"'))
        {
            return ReadDelimited(text, ref position, '"');
        }

        if (position == text.Length || !IsFirst(text[position]))
        {
            return Found(text, position);
        }

        while (position < text.Length && IsNext(text[position]))
        {
            position++;
        }

        return null;
    }

    // From the opening delimiter to its closing one, which is written twice inside.  It ends on its line, and holds
    // at least one character.
    private static string? ReadDelimited(string text, ref int position, char closing)
    {
        var index = position + 1;
        while (index < text.Length)
        {
            var value = text[index];
            if (TypeNameFound.IsLineBreak(value))
            {
                return TypeNameFound.OneLine;
            }

            if (value != closing)
            {
                index++;
            }
            else if (IsAt(text, index + 1, closing))
            {
                index += 2;
            }
            else if (index == position + 1)
            {
                return Shape;
            }
            else
            {
                position = index + 1;
                return null;
            }
        }

        return NotClosed;
    }

    // Nothing, or "(max)" or "(n)" or "(n, m)" after any spaces, with spaces allowed inside.
    private static string? ReadModifier(string text, ref int position)
    {
        var start = position;
        SkipSpaces(text, ref position);
        if (!IsAt(text, position, '('))
        {
            position = start;
            return null;
        }

        position++;
        SkipSpaces(text, ref position);
        if (text.AsSpan(position).StartsWith(Max, StringComparison.OrdinalIgnoreCase))
        {
            position += Max.Length;
        }
        else if (!ReadDigits(text, ref position))
        {
            return Found(text, position);
        }
        else
        {
            SkipSpaces(text, ref position);
            if (IsAt(text, position, ','))
            {
                position++;
                SkipSpaces(text, ref position);
                if (!ReadDigits(text, ref position))
                {
                    return Found(text, position);
                }
            }
        }

        SkipSpaces(text, ref position);
        if (!IsAt(text, position, ')'))
        {
            return Found(text, position);
        }

        position++;
        return null;
    }

    // Only spaces to the end.
    private static string? ReadEnd(string text, int position)
    {
        SkipSpaces(text, ref position);
        if (position == text.Length)
        {
            return null;
        }

        return text[position] == '.' ? TypeNameFound.Parts : Found(text, position);
    }

    // What stands where the grammar was left, anything but a ";", a comment or a line break being the shape of a
    // type name.
    private static string Found(string text, int position) => TypeNameFound.At(text, position, Shape);

    private static bool ReadDigits(string text, ref int position)
    {
        var start = position;
        while (position < text.Length && char.IsAsciiDigit(text[position]))
        {
            position++;
        }

        return position > start;
    }

    // A plain identifier of T-SQL as the server reads one: a letter of any script, "_", "#" or "@" first, then those,
    // a digit of any script or "$".  A name with any other character is written in brackets.
    private static bool IsFirst(char value) => char.IsLetter(value) || (value is '_' or '#' or '@');

    private static bool IsNext(char value) => QuoteReader.IsNameCharacterOfSqlServer(value);

    private static bool IsAt(string text, int position, char value) =>
        position < text.Length && text[position] == value;

    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && (text[position] is ' ' or '\t'))
        {
            position++;
        }
    }
}
