using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing.Quoting;

namespace SqlSource.Parsing;

/// <summary>
/// Splits SQL text into quoted regions, comments, hints and plain text.
/// </summary>
/// <remarks>
/// The lexer reads by the <see cref="SqlDialectRules" /> it holds and names no dialect and no quoting form.  It knows
/// nothing about markers.  Where the rules leave a choice it keeps text, because a comment left in the SQL is
/// harmless and SQL removed from it is a bug.
/// </remarks>
internal sealed class SqlLexer(string text, SqlDialectRules rules)
{
    private static readonly char[] LineTerminators = ['\r', '\n'];

    private readonly ImmutableArray<SqlLexeme>.Builder _lexemes = ImmutableArray.CreateBuilder<SqlLexeme>();
    private SqlParseError? _error;
    private int _textStart;

    /// <summary>
    /// The rules for the text that has not been read yet.  They may be changed between two lexemes, which is how a
    /// file's <c>dialect=</c> directive takes effect from the line after it.
    /// </summary>
    public SqlDialectRules Rules { get; set; } = rules;

    /// <summary>The offset after the last lexeme that was read.</summary>
    public int Position { get; private set; }

    /// <summary>Lexes a text that has one dialect.</summary>
    public static SqlLexResult Lex(string text, SqlDialectRules rules) => new SqlLexer(text, rules).ReadToEnd();

    /// <summary>
    /// Reads the next comment, if only whitespace comes before it.  Returns false, and reads nothing, when the next
    /// thing in the text is anything else, a hint included, and at the end of the text.
    /// </summary>
    public bool TryReadLeadingComment(out SqlLexeme comment)
    {
        comment = default;
        var index = Position;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (_error is not null || index >= text.Length || !IsComment(index))
        {
            return false;
        }

        Position = index;
        Step();
        if (_error is not null)
        {
            return false;
        }

        comment = _lexemes[_lexemes.Count - 1];
        return true;
    }

    /// <summary>Reads the rest of the text and returns every lexeme, or the error that stopped the lexer.</summary>
    public SqlLexResult ReadToEnd()
    {
        while (Position < text.Length && _error is null)
        {
            var starter = Rules.FindStarter(text, Position);
            Position = starter < 0 ? text.Length : starter;
            if (starter >= 0)
            {
                Step();
            }
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
        var start = Position;
        var current = text[start];
        if (current == '-' && IsDashComment(start))
        {
            Add(IsLineHint(start) ? SqlLexemeKind.Hint : SqlLexemeKind.LineComment, start, FindLineEnd(start));
        }
        else if (current == '#' && Rules.HashComments)
        {
            Add(SqlLexemeKind.LineComment, start, FindLineEnd(start));
        }
        else if (current == '/' && CharAt(start + 1) == '*')
        {
            ReadBlockComment(start);
        }
        else if (Rules.ReaderFor(current) is { } reader)
        {
            ReadQuoted(reader, start);
        }
        else
        {
            Position++;
        }
    }

    // A comment that is stripped: not a hint, which is kept and can hold SQL.
    private bool IsComment(int index) =>
        text[index] switch
        {
            '-' => IsDashComment(index) && !IsLineHint(index),
            '#' => Rules.HashComments,
            '/' => CharAt(index + 1) == '*' && !IsBlockHint(index),
            _ => false,
        };

    private bool IsDashComment(int index) =>
        CharAt(index + 1) == '-' && (!Rules.DashNeedsWhitespace || IsDashBoundary(CharAt(index + 2)));

    // What MySQL wants after the two dashes.  The end of the text is '\0' here, which is a control character.
    private static bool IsDashBoundary(char value) => char.IsWhiteSpace(value) || char.IsControl(value);

    private bool IsLineHint(int index) => Rules.LineHints && CharAt(index + 2) == '+';

    private bool IsBlockHint(int index) =>
        CharAt(index + 2) is '+' or '!' || (Rules.MariaDbHints && CharAt(index + 2) == 'M' && CharAt(index + 3) == '!');

    private char CharAt(int index) => index < text.Length ? text[index] : '\0';

    private void Add(SqlLexemeKind kind, int start, int end)
    {
        AddPendingText(start);
        _lexemes.Add(new SqlLexeme(kind, TextSpan.FromBounds(start, end)));
        _textStart = end;
        Position = end;
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
            if (Rules.NestedComments && text[index] == '/' && CharAt(index + 1) == '*')
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

        Add(IsBlockHint(start) ? SqlLexemeKind.Hint : SqlLexemeKind.BlockComment, start, index);
    }

    private void ReadQuoted(QuoteReader reader, int start)
    {
        var end = reader.FindEnd(text, start);
        if (end == QuoteReader.NotAQuote)
        {
            Position++;
        }
        else if (end == QuoteReader.Unterminated)
        {
            _error = SqlParseError.Create(SqlParseErrorKind.UnterminatedQuote, new TextSpan(start, 1));
        }
        else
        {
            Add(SqlLexemeKind.Quoted, start, end);
        }
    }
}
