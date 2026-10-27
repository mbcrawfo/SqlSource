using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// The grammar of a key's parts, <c>[part "."] part ["[]"]</c>, which <see cref="TypeKeys" /> reads by each engine's
/// rules: a part is plain, quoted, or quoted in PostgreSQL's Unicode-escape form, whose escapes
/// <see cref="UnicodeEscape" /> reads.  It splits a written key into its parts, gives a part's text, and writes a text
/// as a part.
/// </summary>
internal static class TypeKeyText
{
    // The start of a part in the escaped form, with the U in either case.
    private const string EscapedOpening = "U&\"";

    /// <summary>
    /// Splits a key as it is written, which is what <see cref="TypeKeys.TryNormalizePostgres" /> gives and
    /// <see cref="WritePart" /> builds, into its parts, quotes included: <c>"a.b".c[]</c> gives <c>"a.b"</c>, <c>c</c>
    /// and true, and <c>U&amp;"a.b".c</c> gives <c>U&amp;"a.b"</c>, whose escapes are checked here.
    /// <paramref name="schema" /> is empty for an unqualified key.  Returns false for text that is not
    /// <c>[part "."] part ["[]"]</c>, the grammar of <see cref="TypeKeys.TryNormalizePostgres" />.
    /// </summary>
    public static bool TrySplit(
        ReadOnlySpan<char> key,
        out ReadOnlySpan<char> schema,
        out ReadOnlySpan<char> name,
        out bool isArray
    )
    {
        schema = default;
        name = default;
        isArray = false;

        // A key is read from the left, so that the brackets of a quoted name are not the key's own.
        if (!TryReadPart(key, 0, out var end))
        {
            return false;
        }

        name = key.Slice(0, end);
        if (end < key.Length && key[end] == '.')
        {
            var start = end + 1;
            if (!TryReadPart(key, start, out end))
            {
                return false;
            }

            schema = name;
            name = key.Slice(start, end - start);
        }

        var rest = key.Slice(end);
        isArray = rest.SequenceEqual("[]".AsSpan());
        return isArray || rest.IsEmpty;
    }

    /// <summary>
    /// Whether <paramref name="part" />, as <see cref="TrySplit" /> gives it, is in quotes: <c>"x"</c>, or
    /// <c>U&amp;"x"</c> in the escaped form, with <c>u</c> allowed.
    /// </summary>
    public static bool IsQuoted(ReadOnlySpan<char> part) => (!part.IsEmpty && part[0] == '"') || IsEscaped(part);

    /// <summary>
    /// The text of <paramref name="part" />, as <see cref="TrySplit" /> gives it, without the quotes of a quoted one
    /// and with each <c>""</c> in it a single <c>"</c>, and each escape of an escaped one the character it names.
    /// Allocates only for a part that holds <c>""</c>, or <c>\</c> in an escaped one.  A <c>\</c> that is not a valid
    /// escape, which <see cref="TrySplit" /> does not give, is kept as a character.
    /// </summary>
    public static ReadOnlySpan<char> TextOf(ReadOnlySpan<char> part)
    {
        if (!IsQuoted(part))
        {
            return part;
        }

        var isEscaped = IsEscaped(part);
        var opening = isEscaped ? EscapedOpening.Length : 1;
        if (part.Length <= opening)
        {
            return default;
        }

        var inside = part.Slice(opening, part.Length - opening - 1);
        if (inside.IndexOf('"') < 0 && (!isEscaped || inside.IndexOf('\\') < 0))
        {
            return inside;
        }

        // The span is over the one array that is made, so the text costs a single allocation.  No step writes more
        // characters than it reads, so the text is never longer than the inside.
        var text = new char[inside.Length];
        var length = 0;
        var index = 0;
        while (index < inside.Length)
        {
            var character = inside[index];
            if (
                isEscaped
                && character == '\\'
                && UnicodeEscape.TryRead(inside, index, out var first, out var second, out var next)
            )
            {
                text[length++] = first;
                if (second != '\0')
                {
                    text[length++] = second;
                }

                index = next;
                continue;
            }

            text[length++] = character;

            // A quote is doubled in a part that is well formed, and the second of the two is skipped.
            index += character == '"' ? 2 : 1;
        }

        return new ReadOnlySpan<char>(text, 0, length);
    }

    /// <summary>
    /// Writes <paramref name="text" /> as a part of a key: as it is when it is not empty and holds only letters,
    /// digits, <c>_</c> and <c>$</c>, none of them upper case, since a plain part is read in lower case; otherwise in
    /// double quotes with each <c>"</c> doubled, or in the escaped form when the quotes would not keep it.  A key is
    /// trimmed and a marker is one line, so a text with a control character other than NUL or a white space other than
    /// a lone space is written <c>U&amp;"a\000Ab"</c>, with <c>\XXXX</c> for each such character, <c>\\</c> for a
    /// <c>\</c> and <c>""</c> for a <c>"</c>.  A NUL stays as it is: a PostgreSQL name cannot hold it, and
    /// <c>\0000</c> is no escape.
    /// </summary>
    public static string WritePart(string text)
    {
        var isPlain = text.Length > 0 && text.All(character => IsPlainCharacter(character) && !char.IsUpper(character));
        return isPlain ? text : Quote(text);
    }

    /// <summary>
    /// A part in double quotes, with each quote doubled, or in the escaped form when the quotes would not keep it.
    /// </summary>
    internal static string Quote(string text)
    {
        if (!NeedsEscape(text))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        var written = new StringBuilder(EscapedOpening, text.Length + EscapedOpening.Length + 8);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (MustEscape(text, index))
            {
                _ = written.Append('\\').Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            }
            else if (character is '\\' or '"')
            {
                _ = written.Append(character).Append(character);
            }
            else
            {
                _ = written.Append(character);
            }
        }

        return written.Append('"').ToString();
    }

    private static bool NeedsEscape(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (MustEscape(text, index))
            {
                return true;
            }
        }

        return false;
    }

    // A character that a quoted part would not keep: the trim reads any white space but U+0020 as a space and a run
    // of them as one, and a control character may end the line.  A NUL is none of them: no PostgreSQL name holds one,
    // and \0000 cannot write it.
    private static bool MustEscape(string text, int index)
    {
        var character = text[index];
        return (character != '\0' && char.IsControl(character))
            || (char.IsWhiteSpace(character) && character != ' ')
            || (character == ' ' && index > 0 && text[index - 1] == ' ');
    }

    /// <summary>
    /// Reads the part of a key that starts at <paramref name="start" />, plain, quoted or escaped, and gives the index
    /// after it.  A quoted part holds at least one character, and ends at the first quote that is not doubled; an
    /// escaped one holds only valid escapes.  False for an empty part, or a quoted one that does not close.
    /// </summary>
    public static bool TryReadPart(ReadOnlySpan<char> key, int start, out int end)
    {
        end = start;
        if (start >= key.Length)
        {
            return false;
        }

        var isEscaped = IsEscaped(key.Slice(start));
        if (key[start] != '"' && !isEscaped)
        {
            while (end < key.Length && IsPlainCharacter(key[end]))
            {
                end++;
            }

            return end > start;
        }

        var opening = isEscaped ? start + EscapedOpening.Length - 1 : start;
        end = opening + 1;
        while (end < key.Length)
        {
            if (isEscaped && key[end] == '\\')
            {
                if (!UnicodeEscape.TryRead(key, end, out _, out _, out end))
                {
                    return false;
                }
            }
            else if (key[end] != '"')
            {
                end++;
            }
            else if (end + 1 < key.Length && key[end + 1] == '"')
            {
                end += 2;
            }
            else
            {
                end++;
                return end - opening > 2;
            }
        }

        return false;
    }

    /// <summary>Whether the part starts as the escaped form does, <c>U&amp;"</c> in either case of the U.</summary>
    internal static bool IsEscaped(ReadOnlySpan<char> part) =>
        part.Length >= EscapedOpening.Length && part[0] is 'U' or 'u' && part[1] == '&' && part[2] == '"';

    /// <summary>Whether a character may stand in a plain part: a letter, a digit, <c>_</c> or <c>$</c>.</summary>
    public static bool IsPlainCharacter(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';
}
