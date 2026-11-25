using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SqlSource.Parsing;
using SqlSource.Parsing.Quoting;
using SqlSource.Settings.TypeOverrides;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres.Statements;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// How PostgreSQL writes a name: a quoted name (<see cref="Quote" />), the bare name it leaves unquoted, the
/// characters of a word of SQL, and a <c>$n</c>.  Each rule has its one definition here.
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
/// everything up to a comma, without its white space, which a list the server accepted never holds inside a bare name.
/// <see cref="StatementShape.PositionalParameter" /> and <c>NpgsqlCheck</c>'s <c>PartAt</c> find a <c>$n</c> without
/// reading its number.
/// </para>
/// </remarks>
internal static class PostgresIdentifiers
{
    /// <summary>The most bytes of UTF-8 that PostgreSQL keeps of a name, one less than <c>NAMEDATALEN</c>.</summary>
    public const int MaxNameBytes = 63;

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

    /// <summary>A name as a quoted identifier: in double quotes, with each quote doubled.</summary>
    /// <param name="name">The name, without quotes.</param>
    public static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

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
    /// The name a word of SQL stands for, as PostgreSQL folds and cuts it: ASCII letters in lower case, and no more
    /// than <see cref="MaxNameBytes" /> bytes.  A letter above U+007F is kept as it is, as a server of UTF-8 keeps it.
    /// </summary>
    /// <param name="word">The word, as the SQL writes it.</param>
    public static string FoldedWord(ReadOnlySpan<char> word)
    {
        var folded = new StringBuilder(word.Length);
        foreach (var character in word)
        {
            _ = folded.Append(char.IsAsciiLetterUpper(character) ? (char)(character + ('a' - 'A')) : character);
        }

        return Truncated(folded.ToString());
    }

    /// <summary>
    /// The name a quoted identifier stands for, when the lexeme at the index is one: <c>"..."</c> with <c>""</c> read
    /// as one quote, or <c>U&amp;"..."</c> with its escapes decoded, and cut to <see cref="MaxNameBytes" /> bytes.
    /// </summary>
    /// <remarks>
    /// A Unicode-escaped identifier is one whose quote the text lexeme before it ends at with <c>U&amp;</c>
    /// (<see cref="OpensUnicodeEscape" />).  Its escape is <c>\</c>, or the one character of the string that a
    /// <c>UESCAPE</c> after it gives: neither a hex digit, <c>+</c>, a quote nor white space.  The doubled quotes are
    /// read before the escapes, as the server reads them.
    /// </remarks>
    /// <param name="sql">The SQL.</param>
    /// <param name="lexemes">Its lexemes.</param>
    /// <param name="index">The index of the lexeme.</param>
    /// <param name="name">The name, or null for a Unicode-escaped identifier that cannot be decoded.</param>
    /// <param name="end">The index of the lexeme after the identifier and its <c>UESCAPE</c> clause.</param>
    /// <returns>False when the lexeme is no quoted identifier.</returns>
    public static bool TryReadQuotedName(
        string sql,
        IReadOnlyList<SqlLexeme> lexemes,
        int index,
        out string? name,
        out int end
    )
    {
        name = null;
        end = index + 1;
        var lexeme = lexemes[index];
        var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
        if (lexeme.Kind != SqlLexemeKind.Quoted || text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return false;
        }

        var unquoted = Unquote(text);
        if (!OpensUnicodeEscape(sql, lexemes, index))
        {
            name = Truncated(unquoted);
            return true;
        }

        if (EscapeOf(sql, lexemes, index + 1, ref end) is { } escape)
        {
            name = Unescaped(unquoted, escape) is { } unescaped ? Truncated(unescaped) : null;
        }

        return true;
    }

    /// <summary>
    /// Whether the quoted lexeme at the index is a Unicode-escaped identifier or string: the text lexeme just before
    /// it ends with <c>U&amp;</c>, in any case, whose <c>U</c> continues no word.
    /// </summary>
    /// <param name="sql">The SQL.</param>
    /// <param name="lexemes">Its lexemes.</param>
    /// <param name="index">The index of the quoted lexeme.</param>
    public static bool OpensUnicodeEscape(string sql, IReadOnlyList<SqlLexeme> lexemes, int index)
    {
        if (index < 1 || lexemes[index - 1] is not { Kind: SqlLexemeKind.Text, Span: var span })
        {
            return false;
        }

        var text = sql.AsSpan(span.Start, span.Length);
        return span.Start + span.Length == lexemes[index].Span.Start
            && text.EndsWith("U&", StringComparison.OrdinalIgnoreCase)
            && (text.Length == 2 || !IsWordPart(text[^3]));
    }

    /// <summary>
    /// A name as PostgreSQL keeps it: no more than <see cref="MaxNameBytes" /> bytes of UTF-8, cut at the end of a
    /// character, as the server cuts an identifier.
    /// </summary>
    /// <param name="name">The name.</param>
    public static string Truncated(string name)
    {
        if (Encoding.UTF8.GetByteCount(name) <= MaxNameBytes)
        {
            return name;
        }

        var bytes = 0;
        var at = 0;
        while (at < name.Length)
        {
            var length = char.IsSurrogatePair(name, at) ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(name.AsSpan(at, length));
            if (bytes + size > MaxNameBytes)
            {
                break;
            }

            bytes += size;
            at += length;
        }

        return name[..at];
    }

    // The escape of a Unicode-escaped identifier whose lexemes end before the index: \, or the character of the
    // UESCAPE clause there, with the end moved past it; null for a clause that gives none the server takes.
    private static char? EscapeOf(string sql, IReadOnlyList<SqlLexeme> lexemes, int index, ref int end)
    {
        var at = SkipBlanks(sql, lexemes, index);
        if (at >= lexemes.Count || lexemes[at] is not { Kind: SqlLexemeKind.Text, Span: var span })
        {
            return '\\';
        }

        var text = sql.AsSpan(span.Start, span.Length).TrimStart();
        if (
            !text.StartsWith("UESCAPE", StringComparison.OrdinalIgnoreCase)
            || (text.Length > "UESCAPE".Length && IsWordPart(text["UESCAPE".Length]))
        )
        {
            return '\\';
        }

        if (!text["UESCAPE".Length..].IsWhiteSpace())
        {
            return null;
        }

        at = SkipBlanks(sql, lexemes, at + 1);
        if (at >= lexemes.Count || lexemes[at] is not { Kind: SqlLexemeKind.Quoted, Span: var quoted })
        {
            return null;
        }

        var literal = sql.AsSpan(quoted.Start, quoted.Length);
        if (literal is not ['\'', var escape, '\''] || char.IsAsciiHexDigit(escape) || escape is '+' or '\'' or '"')
        {
            return null;
        }

        end = at + 1;
        return char.IsWhiteSpace(escape) ? null : escape;
    }

    // The index of the first lexeme from the index that is no comment and no text of white space alone.
    private static int SkipBlanks(string sql, IReadOnlyList<SqlLexeme> lexemes, int index)
    {
        var at = index;
        while (
            at < lexemes.Count
            && (
                DescriberSql.IsCommentOrHint(lexemes[at].Kind)
                || (
                    lexemes[at] is { Kind: SqlLexemeKind.Text, Span: var span }
                    && sql.AsSpan(span.Start, span.Length).IsWhiteSpace()
                )
            )
        )
        {
            at++;
        }

        return at;
    }

    // The text of a Unicode-escaped name with its escapes decoded, or null when one is not valid.
    private static string? Unescaped(string text, char escape)
    {
        var name = new StringBuilder(text.Length);
        var at = 0;
        while (at < text.Length)
        {
            if (text[at] != escape)
            {
                _ = name.Append(text[at]);
                at++;
            }
            else if (UnicodeEscape.TryRead(text, at, escape, out var first, out var second, out at))
            {
                _ = name.Append(first);
                if (second != '\0')
                {
                    _ = name.Append(second);
                }
            }
            else
            {
                return null;
            }
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

    /// <summary>
    /// The index after the run of word parts, as <see cref="IsWordPart" /> reads them, that starts at the index.  The
    /// rule a reader of words uses.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="from">The index the run starts at.</param>
    /// <returns>The index after the run, which is <paramref name="from" /> when no word part starts there.</returns>
    public static int WordEnd(ReadOnlySpan<char> text, int from)
    {
        var end = from;
        while (end < text.Length && IsWordPart(text[end]))
        {
            end++;
        }

        return end;
    }

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
