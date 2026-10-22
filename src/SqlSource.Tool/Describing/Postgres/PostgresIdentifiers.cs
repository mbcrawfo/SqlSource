using System;
using System.Globalization;
using System.Text;
using SqlSource.Parsing.Quoting;
using SqlSource.Tool.Describing.Postgres.Statements;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// How PostgreSQL writes a name: a quoted name, the bare name it leaves unquoted, the characters of a word of SQL, and
/// a <c>$n</c>.  Each rule has its one definition here.
/// </summary>
/// <remarks>
/// <para>
/// The bare-name rule and the word rule are two rules.  A bare name is what <c>quote_identifier</c> leaves unquoted
/// and a plan prints bare, so an entry in upper case, such as <c>CURRENT_DATE</c>, is no name of a column.  A word is
/// what PostgreSQL's lexer reads as one, in the user's SQL.
/// </para>
/// <para>
/// Some readers keep a rule of their own on purpose.  <see cref="DeclaredTypeText" />'s is stricter than the word rule,
/// since it guards what is put into a <c>PREPARE</c>.  <see cref="PostgresSearchPath" /> reads a bare element as
/// anything up to a comma or white space, as the server does.  <see cref="OuterJoinGate" /> compares a quoted name
/// without unescaping it.  <see cref="StatementShape.PositionalParameter" /> and <c>NpgsqlCheck</c>'s <c>PartAt</c>
/// find a <c>$n</c> without reading its number.
/// </para>
/// </remarks>
internal static class PostgresIdentifiers
{
    /// <summary>
    /// Finds the end of the quoted text that opens at the index, with the quote character there, <c>"</c> or
    /// <c>'</c>, doubled read as one.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="at">The index of the opening quote.</param>
    /// <param name="end">The index after the closing quote, or the length of the text when none closes.</param>
    /// <returns>False when the quote does not close.</returns>
    public static bool SkipQuoted(string text, int at, out int end)
    {
        var quote = text[at];
        end = at + 1;
        while (end < text.Length)
        {
            if (text[end] != quote)
            {
                end++;
            }
            else if (end + 1 < text.Length && text[end + 1] == quote)
            {
                end += 2;
            }
            else
            {
                end++;
                return true;
            }
        }

        return false;
    }

    /// <summary>The name a quoted name stands for: the text between its quotes, with <c>""</c> made <c>"</c>.</summary>
    /// <param name="quoted">The quoted name, with its quotes.</param>
    public static string Unquote(ReadOnlySpan<char> quoted)
    {
        var inner = quoted[1..^1];
        var name = new StringBuilder(inner.Length);
        var index = 0;
        while (index < inner.Length)
        {
            _ = name.Append(inner[index]);
            index += inner[index] == '"' ? 2 : 1;
        }

        return name.ToString();
    }

    /// <summary>
    /// Whether a name is one that PostgreSQL leaves unquoted: lower-case ASCII letters, digits and <c>_</c>, and not
    /// starting with a digit.  The keyword check of <c>quote_identifier</c> is not made.
    /// </summary>
    public static bool IsBareName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty || char.IsAsciiDigit(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a character starts a word of SQL as PostgreSQL reads one: an ASCII letter, <c>_</c>, or any character
    /// above U+007F, which PostgreSQL reads as a letter.
    /// </summary>
    public static bool IsWordStart(char character) =>
        char.IsAsciiLetter(character) || character == '_' || character > '\u007F';

    /// <summary>
    /// Whether a character continues a word of SQL as PostgreSQL reads one: a character that starts one, a digit or
    /// <c>$</c>.  The lexer's rule.
    /// </summary>
    public static bool IsWordPart(char character) => QuoteReader.IsNameCharacterOfPostgreSql(character);

    /// <summary>The n of a <c>$n</c> that starts at the index, or null when there is none or n does not fit.</summary>
    public static int? ParameterNumberAt(string text, int at)
    {
        if (at >= text.Length || text[at] != '$')
        {
            return null;
        }

        var end = at + 1;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return int.TryParse(
            text.AsSpan(at + 1, end - at - 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var number
        )
            ? number
            : null;
    }
}
