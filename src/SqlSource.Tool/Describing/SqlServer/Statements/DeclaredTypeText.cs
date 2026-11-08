using System;
using System.Globalization;
using SqlSource.Parsing.Quoting;
using TypeNameFound = SqlSource.Tool.Describing.Describers.DescribeFailures.TypeNameFound;

namespace SqlSource.Tool.Describing.SqlServer.Statements;

/// <summary>
/// Reads the type of a <c>-- param:</c> marker against T-SQL's grammar of a type name, before it is put into the
/// declarations the tool sends.
/// </summary>
/// <remarks>
/// <para>
/// A type is a name of one or two parts joined by a dot, with any spaces, tabs and zero-width spaces (which the server
/// reads as spaces) around it, each part a plain identifier (a letter or a letter number of any script, <c>_</c>,
/// <c>@</c> or <c>#</c>, then those, digits of any script, combining marks, format characters, connectors and <c>$</c>,
/// as the server reads one), a bracketed one with <c>]]</c> for a bracket, or a double-quoted one with <c>""</c> for a
/// quote; then, after any spaces, one or two numbers in parentheses, or a name in parentheses, an XML schema
/// collection's after <c>CONTENT</c> or <c>DOCUMENT</c> in any case or not, of which <c>(max)</c> is one; and nothing
/// else but spaces at either end.  A comment, a <c>;</c>, a second <c>(</c>, a quote or a bracket left open and a line
/// break are refused, each with what was found.
/// </para>
/// <para>
/// The server reads a plain identifier by the tables of Unicode 3.2, and this reader by the current ones of .NET, so a
/// character added or recategorised since can differ: <c>ꞔ</c> is taken here and refused by the server, and <c>・</c>
/// after the first character is refused here and taken by the server, which a name in brackets avoids.
/// </para>
/// <para>
/// It is the first refusal and not the guarantee: a declaration reaches the server only as the value of a parameter
/// of a procedure, which reads it as a list of declarations and refuses one that is not.  It does not say whether the
/// database has the type: the server does.  Nor does it say whether a type takes a schema collection: the server
/// refuses <c>int(dbo.S)</c> as it refuses any declaration it does not read.  A schema collection of three parts is
/// refused here though, since the server ends the session for one.
/// </para>
/// </remarks>
internal static class DeclaredTypeText
{
    public const string NotClosed = "a quote or a bracket is not closed";

    public const string Shape =
        "a type name is a name, then (max), one or two numbers or an XML schema collection in parentheses";

    /// <summary>
    /// The type without the spaces around it, with whether its parentheses hold a name, or why it is refused:
    /// <see cref="TypeNameFound.Reason" /> of what was found at the first place the text leaves the grammar.
    /// </summary>
    public static Outcome<Declaration, string> Read(string text)
    {
        var position = 0;
        var holdsName = false;
        var found =
            ReadName(text, ref position) ?? ReadModifier(text, ref position, ref holdsName) ?? ReadEnd(text, position);
        return found is null
            ? Outcome<Declaration, string>.Of(
                new Declaration(text.Trim(' ', '\t', QuoteReader.ZeroWidthSpace), holdsName)
            )
            : Outcome<Declaration, string>.Failed(TypeNameFound.Reason(found));
    }

    // One part, or two joined by a dot with any spaces around it.  Null when it was read, else what was found.  A dot
    // with no part on one side of it, or a third part, is a count of parts.
    private static string? ReadName(string text, ref int position)
    {
        SkipSpaces(text, ref position);
        if (IsAt(text, position, '.'))
        {
            return TypeNameFound.Parts;
        }

        var found = ReadPart(text, ref position);
        if (found is not null || !SkipToDot(text, ref position))
        {
            return found;
        }

        position++;
        SkipSpaces(text, ref position);
        if (position == text.Length || text[position] == '.')
        {
            return TypeNameFound.Parts;
        }

        return ReadPart(text, ref position) ?? (SkipToDot(text, ref position) ? TypeNameFound.Parts : null);
    }

    // Moves to a dot after any spaces, or stays where it was when no dot follows them.
    private static bool SkipToDot(string text, ref int position)
    {
        var dot = position;
        SkipSpaces(text, ref dot);
        if (!IsAt(text, dot, '.'))
        {
            return false;
        }

        position = dot;
        return true;
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

    // Nothing, or "(n)", "(n, m)" or "(collection)" after any spaces, with spaces allowed inside.  "(max)" is read as
    // a collection named max, and sent as written.  A name read in the parentheses sets holdsName.
    private static string? ReadModifier(string text, ref int position, ref bool holdsName)
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
        if (ReadDigits(text, ref position))
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
        else if (position < text.Length && (text[position] is '[' or '"' or '.' || IsFirst(text[position])))
        {
            var found = ReadCollection(text, ref position);
            if (found is not null)
            {
                return found;
            }

            holdsName = true;
        }
        else
        {
            return Found(text, position);
        }

        SkipSpaces(text, ref position);
        if (!IsAt(text, position, ')'))
        {
            return Found(text, position);
        }

        position++;
        return null;
    }

    // An XML schema collection: a name, after CONTENT or DOCUMENT or not.  A plain first part that is one of the two
    // words is the word when another name follows it, and the collection's name when ")" or a dot does.
    private static string? ReadCollection(string text, ref int position)
    {
        var start = position;
        if (IsAt(text, position, '.'))
        {
            return TypeNameFound.Parts;
        }

        var found = ReadPart(text, ref position);
        if (found is not null)
        {
            return found;
        }

        // The part holds its brackets or quotes, so a delimited word is never one of the two.
        var part = text.AsSpan(start, position - start);
        var isWord =
            part.Equals("CONTENT", StringComparison.OrdinalIgnoreCase)
            || part.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);
        SkipSpaces(text, ref position);
        if (!isWord || position == text.Length || text[position] is ')' or '.')
        {
            position = start;
        }

        return ReadName(text, ref position);
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

    // A plain identifier of T-SQL as the server reads one: a letter or a letter number of any script, "_", "#" or "@"
    // first, then what QuoteReader.IsNameCharacterOfSqlServer takes.  A name with any other character is written in
    // brackets.
    private static bool IsFirst(char value) =>
        char.IsLetter(value)
        || (value is '_' or '#' or '@')
        || char.GetUnicodeCategory(value) == UnicodeCategory.LetterNumber;

    private static bool IsNext(char value) => QuoteReader.IsNameCharacterOfSqlServer(value);

    private static bool IsAt(string text, int position, char value) =>
        position < text.Length && text[position] == value;

    private static void SkipSpaces(string text, ref int position)
    {
        while (position < text.Length && (text[position] is ' ' or '\t' or QuoteReader.ZeroWidthSpace))
        {
            position++;
        }
    }

    /// <summary>A type that <see cref="Read" /> took.</summary>
    /// <param name="Text">The type without the spaces around it.</param>
    /// <param name="HoldsName">
    /// Whether its parentheses hold a name: an XML schema collection's, or <c>max</c>, which names a collection only
    /// in a type the server reads as <c>xml</c>.
    /// </param>
    internal readonly record struct Declaration(string Text, bool HoldsName);
}
