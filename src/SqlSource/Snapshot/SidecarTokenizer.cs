using System;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Reads the tokens of a sidecar's JSON and checks the grammar of a value.  It allocates nothing: a token is a span,
/// and a string is created only by <see cref="GetString" />.
/// </summary>
/// <param name="text">The text.</param>
/// <param name="position">The offset to start at.</param>
internal struct SidecarTokenizer(string text, int position = 0)
{
    /// <summary>
    /// How deep a text may nest.  The readers above are recursive, so the limit is what keeps a file of ten thousand
    /// <c>[</c> from overflowing the stack: an exception in the generator costs every type its generated code.
    /// </summary>
    public const int MaxDepth = 64;

    /// <summary>The offset after the last token read.</summary>
    public int Position { get; private set; } = position;

    /// <summary>The value of a string token.  A string without an escape costs one <c>Substring</c>.</summary>
    public static string GetString(string text, SidecarToken token)
    {
        var start = token.Span.Start + 1;
        var length = token.Span.Length - 2;
        return token.IsPlain ? text.Substring(start, length) : Unescape(text, start, start + length);
    }

    /// <summary>
    /// Whether a string token's value is <paramref name="value" />.  A plain string allocates nothing.
    /// </summary>
    public static bool StringEquals(string text, SidecarToken token, string value) =>
        token.IsPlain
            ? token.Span.Length - 2 == value.Length
                && string.CompareOrdinal(text, token.Span.Start + 1, value, 0, value.Length) == 0
            : string.Equals(GetString(text, token), value, StringComparison.Ordinal);

    /// <summary>Reads a number token that is a signed 32-bit integer.  False for any other token.</summary>
    public static bool TryGetInt32(string text, SidecarToken token, out int value)
    {
        value = 0;
        if (token.Kind != SidecarTokenKind.Number || !token.IsPlain)
        {
            return false;
        }

        var index = token.Span.Start;
        var negative = text[index] == '-';
        if (negative)
        {
            index++;
        }

        // More digits than a 32-bit integer has.  The total below then fits a long.
        if (token.Span.End - index > 10)
        {
            return false;
        }

        long total = 0;
        for (; index < token.Span.End; index++)
        {
            total = (total * 10) + (text[index] - '0');
        }

        total = negative ? -total : total;
        if (total is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        value = (int)total;
        return true;
    }

    /// <summary>Reads the next token, after any white space.</summary>
    public SidecarToken Next()
    {
        while (Position < text.Length && (text[Position] is ' ' or '\t' or '\n' or '\r'))
        {
            Position++;
        }

        if (Position >= text.Length)
        {
            return new SidecarToken(SidecarTokenKind.End, new TextSpan(text.Length, 0));
        }

        return text[Position] switch
        {
            '{' => Punctuation(SidecarTokenKind.ObjectStart),
            '}' => Punctuation(SidecarTokenKind.ObjectEnd),
            '[' => Punctuation(SidecarTokenKind.ArrayStart),
            ']' => Punctuation(SidecarTokenKind.ArrayEnd),
            ':' => Punctuation(SidecarTokenKind.Colon),
            ',' => Punctuation(SidecarTokenKind.Comma),
            '"' => ReadString(),
            't' => Literal("true", SidecarTokenKind.True),
            'f' => Literal("false", SidecarTokenKind.False),
            'n' => Literal("null", SidecarTokenKind.Null),
            '-' or (>= '0' and <= '9') => ReadNumber(),
            _ => InvalidAt(Position),
        };
    }

    /// <summary>
    /// Reads to the end of the value that starts with <paramref name="first" />, which <see cref="Next" /> returned,
    /// and checks its grammar on the way.  <paramref name="depth" /> is the number of containers open around the
    /// value.  On failure <paramref name="error" /> is the token that breaks the grammar, or the bracket that would
    /// open a level deeper than <see cref="MaxDepth" />.
    /// </summary>
    public bool TrySkipValue(SidecarToken first, int depth, out TextSpan error)
    {
        error = default;
        if (
            first.Kind
            is SidecarTokenKind.String
                or SidecarTokenKind.Number
                or SidecarTokenKind.True
                or SidecarTokenKind.False
                or SidecarTokenKind.Null
        )
        {
            return true;
        }

        if (depth < MaxDepth && first.Kind == SidecarTokenKind.ObjectStart)
        {
            return TrySkipObject(depth + 1, out error);
        }

        if (depth < MaxDepth && first.Kind == SidecarTokenKind.ArrayStart)
        {
            return TrySkipArray(depth + 1, out error);
        }

        return Fail(first, out error);
    }

    private static bool Fail(SidecarToken token, out TextSpan error)
    {
        error = token.Span;
        return false;
    }

    private static bool IsHex(char character) =>
        character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private static int HexValue(char character) => character <= '9' ? character - '0' : (character | 0x20) - 'a' + 10;

    // An escape starts at index, with its backslash.  On success end is the offset after the escape; otherwise it
    // is the offset of the character that cannot be read, which may be the end of the text.
    private static bool TryReadEscape(string text, int index, out int end)
    {
        end = index + 1;
        if (end >= text.Length)
        {
            return false;
        }

        var escape = text[end];
        if (escape is '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't')
        {
            end++;
            return true;
        }

        if (escape != 'u')
        {
            return false;
        }

        for (end++; end < index + 6; end++)
        {
            if (end >= text.Length || !IsHex(text[end]))
            {
                return false;
            }
        }

        return true;
    }

    // The tokenizer has checked every escape between start and end.
    private static string Unescape(string text, int start, int end)
    {
        var value = new StringBuilder(end - start);
        var index = start;
        while (index < end)
        {
            var slash = text.IndexOf('\\', index, end - index);
            if (slash < 0)
            {
                break;
            }

            _ = value.Append(text, index, slash - index);
            var escape = text[slash + 1];
            if (escape == 'u')
            {
                var code =
                    (HexValue(text[slash + 2]) << 12)
                    | (HexValue(text[slash + 3]) << 8)
                    | (HexValue(text[slash + 4]) << 4)
                    | HexValue(text[slash + 5]);
                _ = value.Append((char)code);
                index = slash + 6;
                continue;
            }

            _ = value.Append(
                escape switch
                {
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => escape,
                }
            );
            index = slash + 2;
        }

        return value.Append(text, index, end - index).ToString();
    }

    private bool TrySkipObject(int depth, out TextSpan error)
    {
        error = default;
        var token = Next();
        if (token.Kind == SidecarTokenKind.ObjectEnd)
        {
            return true;
        }

        while (true)
        {
            if (token.Kind != SidecarTokenKind.String)
            {
                return Fail(token, out error);
            }

            token = Next();
            if (token.Kind != SidecarTokenKind.Colon)
            {
                return Fail(token, out error);
            }

            if (!TrySkipValue(Next(), depth, out error))
            {
                return false;
            }

            token = Next();
            if (token.Kind == SidecarTokenKind.ObjectEnd)
            {
                return true;
            }

            if (token.Kind != SidecarTokenKind.Comma)
            {
                return Fail(token, out error);
            }

            token = Next();
        }
    }

    private bool TrySkipArray(int depth, out TextSpan error)
    {
        error = default;
        var token = Next();
        if (token.Kind == SidecarTokenKind.ArrayEnd)
        {
            return true;
        }

        while (true)
        {
            if (!TrySkipValue(token, depth, out error))
            {
                return false;
            }

            token = Next();
            if (token.Kind == SidecarTokenKind.ArrayEnd)
            {
                return true;
            }

            if (token.Kind != SidecarTokenKind.Comma)
            {
                return Fail(token, out error);
            }

            token = Next();
        }
    }

    private SidecarToken Punctuation(SidecarTokenKind kind)
    {
        var token = new SidecarToken(kind, new TextSpan(Position, 1));
        Position++;
        return token;
    }

    private SidecarToken Literal(string word, SidecarTokenKind kind)
    {
        var start = Position;
        if (string.CompareOrdinal(text, start, word, 0, word.Length) != 0)
        {
            return InvalidAt(start);
        }

        Position = start + word.Length;
        return new SidecarToken(kind, new TextSpan(start, word.Length));
    }

    // Ends the read: nothing after text that is not JSON can be trusted.
    private SidecarToken InvalidAt(int index)
    {
        Position = text.Length;
        return new SidecarToken(SidecarTokenKind.Invalid, new TextSpan(index, index < text.Length ? 1 : 0));
    }

    private SidecarToken ReadString()
    {
        var start = Position;
        var plain = true;
        var index = start + 1;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '"')
            {
                Position = index + 1;
                return new SidecarToken(SidecarTokenKind.String, TextSpan.FromBounds(start, Position), plain);
            }

            if (character < ' ')
            {
                return InvalidAt(index);
            }

            if (character != '\\')
            {
                index++;
                continue;
            }

            plain = false;
            if (!TryReadEscape(text, index, out var end))
            {
                return InvalidAt(end);
            }

            index = end;
        }

        return InvalidAt(text.Length);
    }

    private SidecarToken ReadNumber()
    {
        var start = Position;
        var first = text[start] == '-' ? start + 1 : start;
        var index = SkipDigits(first);
        if (index == first)
        {
            return InvalidAt(first);
        }

        // A zero stands alone before a fraction: 01 is not a number.
        if (text[first] == '0' && index > first + 1)
        {
            return InvalidAt(first + 1);
        }

        var plain = true;
        if (IsAt(index, '.'))
        {
            plain = false;
            var end = SkipDigits(index + 1);
            if (end == index + 1)
            {
                return InvalidAt(end);
            }

            index = end;
        }

        if (IsAt(index, 'e') || IsAt(index, 'E'))
        {
            plain = false;
            var digits = IsAt(index + 1, '+') || IsAt(index + 1, '-') ? index + 2 : index + 1;
            var end = SkipDigits(digits);
            if (end == digits)
            {
                return InvalidAt(end);
            }

            index = end;
        }

        Position = index;
        return new SidecarToken(SidecarTokenKind.Number, TextSpan.FromBounds(start, index), plain);
    }

    private readonly bool IsAt(int index, char character) => index < text.Length && text[index] == character;

    private readonly int SkipDigits(int index)
    {
        while (index < text.Length && (text[index] is >= '0' and <= '9'))
        {
            index++;
        }

        return index;
    }
}
