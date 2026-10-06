using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Splits SQL text into quoted regions, comments, hints and plain text.
/// </summary>
/// <remarks>
/// The rules are ANSI SQL plus the quoting forms of PostgreSQL and MySQL that cannot be mistaken for anything else.
/// Where dialects disagree the lexer picks the reading that keeps text, because a comment left in the SQL is harmless
/// and SQL removed from it is a bug.  The dialect-sensitive choices are all in this file: block comments nest, a
/// backslash escapes only inside <c>E'...'</c>, <c>--</c> needs no whitespace after it, and <c>#</c> is not a comment.
/// </remarks>
internal static class SqlLexer
{
    private static readonly char[] LineTerminators = ['\r', '\n'];

    public static SqlLexResult Lex(string text) => new Scanner(text).Run();

    private sealed class Scanner(string text)
    {
        private readonly ImmutableArray<SqlLexeme>.Builder _lexemes = ImmutableArray.CreateBuilder<SqlLexeme>();
        private SqlParseError? _error;
        private int _position;
        private int _textStart;

        public SqlLexResult Run()
        {
            while (_position < text.Length && _error is null)
            {
                Step();
            }

            if (_error is not null)
            {
                return new SqlLexResult(EquatableArray<SqlLexeme>.Empty, _error);
            }

            AddPendingText(text.Length);
            return new SqlLexResult(new EquatableArray<SqlLexeme>(_lexemes.ToImmutable()), null);
        }

        private void Step()
        {
            var start = _position;
            var current = text[start];
            if (current == '-' && CharAt(start + 1) == '-')
            {
                Add(SqlLexemeKind.LineComment, start, FindLineEnd(start));
            }
            else if (current == '/' && CharAt(start + 1) == '*')
            {
                ReadBlockComment(start);
            }
            else if (current is '\'' or '"' or '`')
            {
                ReadQuoted(start);
            }
            else if (current == '$' && TryFindDollarQuoteEnd(start, out var end))
            {
                Add(SqlLexemeKind.Quoted, start, end);
            }
            else
            {
                _position++;
            }
        }

        private static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '$';

        private char CharAt(int index) => index < text.Length ? text[index] : '\0';

        private void Add(SqlLexemeKind kind, int start, int end)
        {
            AddPendingText(start);
            _lexemes.Add(new SqlLexeme(kind, TextSpan.FromBounds(start, end)));
            _textStart = end;
            _position = end;
        }

        private void AddPendingText(int end)
        {
            if (end > _textStart)
            {
                _lexemes.Add(new SqlLexeme(SqlLexemeKind.Text, TextSpan.FromBounds(_textStart, end)));
            }
        }

        private int FindLineEnd(int start)
        {
            var index = text.IndexOfAny(LineTerminators, start);
            return index < 0 ? text.Length : index;
        }

        private void ReadBlockComment(int start)
        {
            var depth = 1;
            var index = start + 2;
            while (index < text.Length && depth > 0)
            {
                if (text[index] == '/' && CharAt(index + 1) == '*')
                {
                    depth++;
                    index += 2;
                }
                else if (text[index] == '*' && CharAt(index + 1) == '/')
                {
                    depth--;
                    index += 2;
                }
                else
                {
                    index++;
                }
            }

            if (depth > 0)
            {
                _error = SqlParseError.Create(SqlParseErrorKind.UnterminatedBlockComment, new TextSpan(start, 2));
                return;
            }

            var kind = CharAt(start + 2) is '+' or '!' ? SqlLexemeKind.Hint : SqlLexemeKind.BlockComment;
            Add(kind, start, index);
        }

        private void ReadQuoted(int start)
        {
            var quote = text[start];
            var backslashEscapes = quote == '\'' && IsEscapeStringPrefix(start);
            var index = start + 1;
            while (index < text.Length)
            {
                var current = text[index];
                if (backslashEscapes && current == '\\')
                {
                    index += 2;
                }
                else if (current != quote)
                {
                    index++;
                }
                else if (CharAt(index + 1) == quote)
                {
                    index += 2;
                }
                else
                {
                    Add(SqlLexemeKind.Quoted, start, index + 1);
                    return;
                }
            }

            _error = SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(start, 1));
        }

        // PostgreSQL's E'...' form.  The E stays in the text before the quoted region.
        private bool IsEscapeStringPrefix(int quoteIndex) =>
            quoteIndex > 0
            && (text[quoteIndex - 1] is 'E' or 'e')
            && (quoteIndex < 2 || !IsIdentifierCharacter(text[quoteIndex - 2]));

        // PostgreSQL's $tag$...$tag$ form.  An opener with no closer is plain text: $ has other meanings elsewhere.
        private bool TryFindDollarQuoteEnd(int start, out int end)
        {
            end = 0;
            if (start > 0 && IsIdentifierCharacter(text[start - 1]))
            {
                return false;
            }

            var tagEnd = start + 1;
            if (char.IsLetter(CharAt(tagEnd)) || CharAt(tagEnd) == '_')
            {
                while (char.IsLetterOrDigit(CharAt(tagEnd)) || CharAt(tagEnd) == '_')
                {
                    tagEnd++;
                }
            }

            if (CharAt(tagEnd) != '$')
            {
                return false;
            }

            var delimiter = text.Substring(start, tagEnd - start + 1);
            var close = text.IndexOf(delimiter, tagEnd + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }

            end = close + delimiter.Length;
            return true;
        }
    }
}
