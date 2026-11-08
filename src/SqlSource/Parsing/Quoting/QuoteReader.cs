using System.Globalization;

namespace SqlSource.Parsing.Quoting;

/// <summary>
/// Finds where one kind of quoted region ends.  A quoted region is a string or a quoted identifier: the lexer copies
/// it as written and looks for no comment inside it.
/// </summary>
/// <remarks>
/// A reader holds no state between calls and allocates nothing, so one instance serves every file.  It is called at
/// the character that opens the region, and may look at the characters before it for a prefix such as <c>E</c>.
/// </remarks>
internal abstract class QuoteReader
{
    /// <summary>The region is opened and never closed.</summary>
    public const int Unterminated = -1;

    /// <summary>The character opens no region here, and is plain text.</summary>
    public const int NotAQuote = -2;

    /// <summary>
    /// Returns the offset after the closing delimiter of the region that opens at <paramref name="start" />, or
    /// <see cref="Unterminated" />, or <see cref="NotAQuote" />.
    /// </summary>
    public abstract int FindEnd(string text, int start);

    /// <summary>
    /// The reader of a part that continues the region that opens at <paramref name="start" />, or null when a
    /// later part would be read as the region itself.  A reader reads one part: whether another follows, and after
    /// what, is for the lexer and the rules of the dialect.
    /// </summary>
    public virtual QuoteReader? FindContinuation(string text, int start) => null;

    /// <summary>The zero-width space, U+200B, which SQL Server reads as a space, as Unicode 3.2 classed it.</summary>
    internal const char ZeroWidthSpace = '\u200B';

    /// <summary>True for a character that can be part of an unquoted identifier.</summary>
    protected static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '$';

    /// <summary>
    /// True for a character that can be part of a name as PostgreSQL and MySQL read it: an identifier character, or
    /// any character above U+007F, which they read as a letter.
    /// </summary>
    internal static bool IsNameCharacterOfPostgreSql(char value) => IsIdentifierCharacter(value) || value > '\u007F';

    /// <summary>
    /// True for a character that can be part of a name as SQL Server reads one after its first character: a letter or
    /// a digit of any script, a combining mark, a letter number, a format character but the zero-width space, a
    /// connector such as <c>‿</c>,
    /// <c>_</c>, <c>@</c>, <c>#</c> or <c>$</c>.  SQL Server 2017 and 2025 take <c>Größe</c>, <c>名前</c>, <c>x٣</c>,
    /// the full-width <c>Ａbc</c>, <c>x</c> with a combining acute accent and <c>xⅠ</c> in a name, and refuse a currency
    /// sign, a superscript digit, a circled digit and a middle dot (<c>€</c>, <c>²</c>, <c>①</c>, <c>·</c>) with error
    /// 102.
    /// </summary>
    /// <remarks>
    /// The server reads a name by the tables of Unicode 3.2, and this rule by the current ones of .NET, so a character
    /// added or recategorised since can differ: <c>ꞔ</c> is taken here and refused by the server, and <c>・</c> after
    /// the first character is refused here and taken by the server.  The zero-width space, U+200B, is the one format
    /// character left out: Unicode 3.2 made it a space, and the server reads it as one, beside a variable's name and
    /// a type's, so text pasted with one still reads as written.
    /// </remarks>
    internal static bool IsNameCharacterOfSqlServer(char value) =>
        char.IsLetterOrDigit(value)
        || value is '_' or '@' or '#' or '$'
        || (
            value > '\u007F'
            && value != ZeroWidthSpace
            && char.GetUnicodeCategory(value)
                is UnicodeCategory.NonSpacingMark
                    or UnicodeCategory.SpacingCombiningMark
                    or UnicodeCategory.LetterNumber
                    or UnicodeCategory.Format
                    or UnicodeCategory.ConnectorPunctuation
        );

    /// <summary>
    /// The end of a region that closes with the character it opens with, where that character doubled stands for
    /// itself.
    /// </summary>
    protected static int FindDoubledEnd(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;
        while (true)
        {
            var close = text.IndexOf(quote, index);
            if (close < 0)
            {
                return Unterminated;
            }

            if (close + 1 < text.Length && text[close + 1] == quote)
            {
                index = close + 2;
            }
            else
            {
                return close + 1;
            }
        }
    }

    /// <summary>
    /// As <see cref="FindDoubledEnd" />, and a backslash takes the character after it with it, whatever that is.
    /// </summary>
    protected static int FindBackslashEnd(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;
        while (index < text.Length)
        {
            var current = text[index];
            if (current == '\\')
            {
                index += 2;
            }
            else if (current != quote)
            {
                index++;
            }
            else if (index + 1 < text.Length && text[index + 1] == quote)
            {
                index += 2;
            }
            else
            {
                return index + 1;
            }
        }

        return Unterminated;
    }
}
