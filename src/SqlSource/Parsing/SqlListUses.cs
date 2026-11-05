using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// What each use of a parameter in the SQL of one query is: a value, a list or a null test.
/// </summary>
/// <remarks>
/// A use is a list when the word before it is <c>IN</c>, and a null test when the words after it are <c>IS NULL</c> or
/// <c>IS NOT NULL</c>.  Words are runs of the dialect's name characters, so <c>JOIN</c> and T-SQL's <c>x#IN</c> are
/// not <c>IN</c>, and a quoted region is never a word.  A comment or a hint between two words is passed over as white
/// space.  <c>position(a IN b)</c> puts a single value after <c>IN</c>, so an <c>IN</c> directly inside the
/// parentheses of a call to <c>position</c> makes no list.  The uses are read from the lexemes of the file, so
/// nothing is lexed again, and a query without the word <c>IN</c> costs a search.
/// </remarks>
internal static class SqlListUses
{
    private const string In = "IN";

    private const string Position = "position";

    private const string Is = "IS";

    private const string Not = "NOT";

    private const string Null = "NULL";

    // What the cursor over a query's lexemes meets next.
    private enum Item
    {
        // The end of the query.
        End,

        // A run of name characters.
        Word,

        // "(".
        Open,

        // ")".
        Close,

        // A parameter lexeme.
        Parameter,

        // Anything else that is not white space: a quoted region, or a character that is not a name character.
        Other,
    }

    /// <summary>
    /// The use of each parameter lexeme from <paramref name="start" /> up to <paramref name="end" />, in order, or an
    /// empty array when no use is a list.  An empty array means that every use is read as a value: a null test matters
    /// only of a list.  A query whose text does not hold the word <c>IN</c>, in any case, is not walked.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="lexemes">The lexemes of the file.</param>
    /// <param name="start">The first lexeme of the query.</param>
    /// <param name="end">The lexeme after the last one of the query.</param>
    /// <param name="rules">The rules the file was read by.</param>
    public static ImmutableArray<SqlParameterUse> Find(
        string text,
        EquatableArray<SqlLexeme> lexemes,
        int start,
        int end,
        SqlDialectRules rules
    )
    {
        if (start >= end)
        {
            return ImmutableArray<SqlParameterUse>.Empty;
        }

        // Most queries have no list, and a search says so.  It also finds an IN in a string or a comment, which only
        // costs the walk.
        if (!HoldsTheWordIn(text, lexemes[start].Span.Start, lexemes[end - 1].Span.End, rules))
        {
            return ImmutableArray<SqlParameterUse>.Empty;
        }

        var count = 0;
        for (var index = start; index < end; index++)
        {
            if (lexemes[index].Kind == SqlLexemeKind.Parameter)
            {
                count++;
            }
        }

        var uses = ImmutableArray.CreateBuilder<SqlParameterUse>(count);

        // Whether each open parenthesis is that of a call to position, innermost last.  Created at the first "(".
        List<bool>? calls = null;
        var hasList = false;
        var afterIn = false;
        var afterPosition = false;
        var cursor = new Cursor(text, lexemes, start, end, rules);
        for (var item = cursor.Next(); item != Item.End; item = cursor.Next())
        {
            if (item == Item.Word)
            {
                afterIn = cursor.IsWord(In);
                afterPosition = cursor.IsWord(Position);
                continue;
            }

            if (item == Item.Open)
            {
                (calls ??= []).Add(afterPosition);
            }
            else if (item == Item.Close && calls is { Count: > 0 })
            {
                calls.RemoveAt(calls.Count - 1);
            }
            else if (item == Item.Parameter)
            {
                var use = Classify(afterIn && !(calls is { Count: > 0 } && calls[calls.Count - 1]), cursor);
                hasList |= use == SqlParameterUse.List;
                uses.Add(use);
            }

            afterIn = false;
            afterPosition = false;
        }

        return hasList ? uses.MoveToImmutable() : ImmutableArray<SqlParameterUse>.Empty;
    }

    // Whether the text from one offset up to another holds IN as a whole word, so that JOIN, INSERT and INTO, which
    // most queries hold, cost no walk.
    private static bool HoldsTheWordIn(string text, int start, int end, SqlDialectRules rules)
    {
        var from = start;
        while (true)
        {
            var index = text.IndexOf(In, from, end - from, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            from = index + In.Length;
            if (
                (index == start || !rules.IsNameCharacter(text[index - 1]))
                && (from == end || !rules.IsNameCharacter(text[from]))
            )
            {
                return true;
            }
        }
    }

    // A use after IN is a list, unless the IN is that of a call to position.  Any other use is a null test or a value.
    private static SqlParameterUse Classify(bool isList, Cursor cursor)
    {
        if (isList)
        {
            return SqlParameterUse.List;
        }

        return IsNullTest(cursor) ? SqlParameterUse.NullTest : SqlParameterUse.Value;
    }

    // Whether the words that the cursor meets next are IS NULL or IS NOT NULL.  The cursor is a copy, so the walk
    // goes on from the parameter.
    private static bool IsNullTest(Cursor cursor) =>
        cursor.Next() == Item.Word
        && cursor.IsWord(Is)
        && cursor.Next() == Item.Word
        && (cursor.IsWord(Null) || (cursor.IsWord(Not) && cursor.Next() == Item.Word && cursor.IsWord(Null)));

    // Reads a query's lexemes one item at a time.  A struct, so that a copy can look ahead without moving the walk.
    private struct Cursor(string text, EquatableArray<SqlLexeme> lexemes, int start, int end, SqlDialectRules rules)
    {
        private int _lexeme = start;
        private int _offset = lexemes[start].Span.Start;
        private TextSpan _word;

        // Whether the word the cursor last met is the given one, ignoring case.
        public readonly bool IsWord(string word) =>
            text.AsSpan(_word.Start, _word.Length).Equals(word.AsSpan(), StringComparison.OrdinalIgnoreCase);

        public Item Next()
        {
            while (_lexeme < end)
            {
                var lexeme = lexemes[_lexeme];
                if (lexeme.Kind != SqlLexemeKind.Text)
                {
                    MoveToNextLexeme();
                    if (lexeme.Kind == SqlLexemeKind.Parameter)
                    {
                        return Item.Parameter;
                    }

                    if (lexeme.Kind == SqlLexemeKind.Quoted)
                    {
                        return Item.Other;
                    }

                    // A comment or a hint is passed over as white space.
                    continue;
                }

                var textEnd = lexeme.Span.End;
                while (_offset < textEnd && char.IsWhiteSpace(text[_offset]))
                {
                    _offset++;
                }

                if (_offset == textEnd)
                {
                    MoveToNextLexeme();
                    continue;
                }

                var value = text[_offset];
                if (!rules.IsNameCharacter(value))
                {
                    _offset++;
                    return value switch
                    {
                        '(' => Item.Open,
                        ')' => Item.Close,
                        _ => Item.Other,
                    };
                }

                // A word never runs on into the next lexeme: one that follows a parameter, a quoted region or a
                // comment starts a word of its own.
                var wordStart = _offset;
                while (_offset < textEnd && rules.IsNameCharacter(text[_offset]))
                {
                    _offset++;
                }

                _word = TextSpan.FromBounds(wordStart, _offset);
                return Item.Word;
            }

            return Item.End;
        }

        private void MoveToNextLexeme()
        {
            _lexeme++;
            if (_lexeme < end)
            {
                _offset = lexemes[_lexeme].Span.Start;
            }
        }
    }
}
