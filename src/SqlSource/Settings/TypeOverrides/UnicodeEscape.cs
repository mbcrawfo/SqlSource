using System;

namespace SqlSource.Settings.TypeOverrides;

/// <summary>
/// Reads an escape of PostgreSQL's Unicode-escape form, as a quoted part of a key may be written:
/// <c>U&amp;"a\000Ab"</c>.
/// </summary>
internal static class UnicodeEscape
{
    /// <summary>
    /// Reads the escape at text[at], a backslash that is followed by a second one, by 4 hex digits, or by + and 6.  A
    /// character outside the BMP is given as its surrogates, in first and second, and a high surrogate must be
    /// followed at once by an escape of a low one.  The second is '\0' for a single character, which no escape can
    /// name.  next is the index after what was read.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<char> text, int at, out char first, out char second, out int next)
    {
        first = '\0';
        second = '\0';
        if (!TryReadCodePoint(text, at, out var codePoint, out next))
        {
            return false;
        }

        if (codePoint is >= 0xDC00 and <= 0xDFFF)
        {
            return false;
        }

        if (codePoint is >= 0xD800 and <= 0xDBFF)
        {
            // The high surrogate waits for its low one.
            if (!TryReadCodePoint(text, next, out var low, out next) || low is < 0xDC00 or > 0xDFFF)
            {
                return false;
            }

            first = (char)codePoint;
            second = (char)low;
            return true;
        }

        if (codePoint > 0xFFFF)
        {
            first = (char)(0xD800 + ((codePoint - 0x10000) >> 10));
            second = (char)(0xDC00 + ((codePoint - 0x10000) & 0x3FF));
            return true;
        }

        first = (char)codePoint;
        return true;
    }

    // Reads one escape to its code point, from U+0001 to U+10FFFF, a surrogate included.
    private static bool TryReadCodePoint(ReadOnlySpan<char> text, int at, out int codePoint, out int next)
    {
        codePoint = 0;
        next = at;
        if (at >= text.Length || text[at] != '\\' || at + 1 >= text.Length)
        {
            return false;
        }

        if (text[at + 1] == '\\')
        {
            codePoint = '\\';
            next = at + 2;
            return true;
        }

        var isLong = text[at + 1] == '+';
        var start = at + (isLong ? 2 : 1);
        var digits = isLong ? 6 : 4;
        if (start + digits > text.Length)
        {
            return false;
        }

        for (var index = start; index < start + digits; index++)
        {
            var value = HexValue(text[index]);
            if (value < 0)
            {
                return false;
            }

            codePoint = (codePoint << 4) | value;
        }

        next = start + digits;
        return codePoint is >= 1 and <= 0x10FFFF;
    }

    private static int HexValue(char character) =>
        character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'a' and <= 'f' => character - 'a' + 10,
            >= 'A' and <= 'F' => character - 'A' + 10,
            _ => -1,
        };
}
