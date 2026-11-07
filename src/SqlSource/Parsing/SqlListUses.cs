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
/// <c>IS NOT NULL</c>.  A list is negated when a <c>NOT</c> is open where it stands: one opens at the word <c>NOT</c>,
/// but for the <c>NOT</c> of <c>IS NOT</c>, and stays open at its depth of parentheses, and in every parenthesis
/// opened inside it, until an <c>AND</c>, an <c>OR</c> or a comma at that depth, or the parenthesis that closes the
/// depth.  So <c>id NOT IN @ids</c>, <c>NOT id IN @ids</c> and <c>NOT (a AND id IN @ids)</c> negate the list, and
/// <c>NOT a AND id IN @ids</c> does not.  A list whose truth the query uses as a value, such as
/// <c>(id IN @ids) IS FALSE</c> or <c>f(id IN @ids)</c>, is negated too, as <see cref="Conditions" /> reads it.  Words
/// are runs of the dialect's name characters, so <c>JOIN</c> and T-SQL's <c>x#IN</c> are not <c>IN</c>, and a quoted
/// region is never a word.  A comment or a hint between two words is passed over as white space.
/// <c>position(a IN b)</c> puts a single value after <c>IN</c>, so an <c>IN</c> directly inside the parentheses of a
/// call to <c>position</c> makes no list.  The uses are read from the lexemes of the file, so nothing is lexed again,
/// and a query without the word <c>IN</c> costs a search.
/// </remarks>
internal static partial class SqlListUses
{
    private const string In = "IN";

    private const string Position = "position";

    private const string Is = "IS";

    private const string Not = "NOT";

    private const string Null = "NULL";

    private const string And = "AND";

    private const string Or = "OR";

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

        // "[".
        OpenBracket,

        // "]".
        CloseBracket,

        // ",".
        Comma,

        // A parameter lexeme.
        Parameter,

        // A run of operator characters, such as "=" or "->>".
        Operator,

        // A quoted region.
        Quoted,

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
    ) => Find(text, lexemes, start, end, rules, readPlaces: false, out _);

    /// <summary>
    /// The use of each parameter lexeme from <paramref name="start" /> up to <paramref name="end" />, as the overload
    /// without <paramref name="places" /> gives it, and where each use stands, as <see cref="Places" /> reads it.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="lexemes">The lexemes of the file.</param>
    /// <param name="start">The first lexeme of the query.</param>
    /// <param name="end">The lexeme after the last one of the query.</param>
    /// <param name="rules">The rules the file was read by.</param>
    /// <param name="places">
    /// The place of each use, in the order of the uses, and empty exactly when they are.  A use that is not a list is
    /// <see cref="SqlListPlace.None" />.
    /// </param>
    public static ImmutableArray<SqlParameterUse> Find(
        string text,
        EquatableArray<SqlLexeme> lexemes,
        int start,
        int end,
        SqlDialectRules rules,
        out ImmutableArray<SqlListPlace> places
    ) => Find(text, lexemes, start, end, rules, readPlaces: true, out places);

    // The walk of both overloads.  The places are read only when asked for.
    private static ImmutableArray<SqlParameterUse> Find(
        string text,
        EquatableArray<SqlLexeme> lexemes,
        int start,
        int end,
        SqlDialectRules rules,
        bool readPlaces,
        out ImmutableArray<SqlListPlace> places
    )
    {
        places = ImmutableArray<SqlListPlace>.Empty;
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

        // Each open parenthesis, innermost last.  Created at the first "(".
        List<Parenthesis>? open = null;
        var hasList = false;
        var afterIn = false;
        var afterIs = false;
        var afterPosition = false;

        // Whether a NOT is open at the depth of the cursor, and whether one is open at a depth that holds it.
        var notHere = false;
        var notAbove = false;
        var conditions = new Conditions(uses, count);
        var reader = readPlaces ? Places.Start(count) : default;
        var cursor = new Cursor(text, lexemes, start, end, rules);
        for (var item = cursor.Next(); item != Item.End; item = cursor.Next())
        {
            reader.Read(item, cursor);
            if (item == Item.Word)
            {
                conditions.Word(cursor);
                if (cursor.IsWord(Not) && !afterIs)
                {
                    notHere = true;
                }
                else if (cursor.IsWord(And) || cursor.IsWord(Or))
                {
                    notHere = false;
                }

                afterIn = cursor.IsWord(In);
                afterIs = cursor.IsWord(Is);
                afterPosition = cursor.IsWord(Position);
                continue;
            }

            if (item == Item.Open)
            {
                conditions.Open();
                (open ??= []).Add(new Parenthesis(afterPosition, notHere, notAbove));
                notAbove |= notHere;
                notHere = false;
            }
            else if (item == Item.Close)
            {
                conditions.Close();
            }

            if (item == Item.Close && open is { Count: > 0 })
            {
                var closed = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                notHere = closed.NotOutside;
                notAbove = closed.NotAboveOutside;
            }
            else if (item == Item.Comma)
            {
                conditions.Comma();
                notHere = false;
            }
            else if (item == Item.Parameter)
            {
                var isList = afterIn && !(open is { Count: > 0 } && open[open.Count - 1].IsPosition);
                var use = Classify(isList, notHere || notAbove, cursor);
                hasList |= use is SqlParameterUse.List or SqlParameterUse.NegatedList;
                reader.Parameter(uses.Count, use);
                conditions.Parameter(uses.Count, use);
                uses.Add(use);
            }
            else if (item == Item.Operator)
            {
                conditions.Operator(cursor);
            }
            else if (item == Item.OpenBracket)
            {
                conditions.OpenBracket();
            }
            else if (item == Item.CloseBracket)
            {
                conditions.CloseBracket();
            }
            else if (item == Item.Other)
            {
                conditions.Other(cursor);
            }
            else if (item == Item.Quoted)
            {
                conditions.Quoted();
            }

            afterIn = false;
            afterIs = false;
            afterPosition = false;
        }

        conditions.End();
        conditions.Return();
        if (hasList && readPlaces)
        {
            places = reader.Result(count);
        }

        reader.Return();
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

    // A use after IN is a list, unless the IN is that of a call to position, and a negated one where a NOT is open.
    // Any other use is a null test or a value.
    private static SqlParameterUse Classify(bool isList, bool isNegated, Cursor cursor)
    {
        if (isList)
        {
            return isNegated ? SqlParameterUse.NegatedList : SqlParameterUse.List;
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

        // The word, or the run of operator characters or the character, the cursor last met.
        public readonly ReadOnlySpan<char> Word => text.AsSpan(_word.Start, _word.Length);

        // Whether the word the cursor last met is one of the given ones, ignoring case.
        public readonly bool IsAnyWord(string[] words)
        {
            for (var index = 0; index < words.Length; index++)
            {
                if (IsWord(words[index]))
                {
                    return true;
                }
            }

            return false;
        }

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
                        _word = default;
                        return Item.Quoted;
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
                    if (IsOperatorCharacter(value))
                    {
                        ReadOperator(textEnd);
                        return Item.Operator;
                    }

                    _word = new TextSpan(_offset, 1);
                    _offset++;
                    return value switch
                    {
                        '(' => Item.Open,
                        ')' => Item.Close,
                        ',' => Item.Comma,
                        '[' => Item.OpenBracket,
                        ']' => Item.CloseBracket,
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

        // An operator is a run of operator characters, which, as PostgreSQL reads it, ends in no "+" or "-" unless it
        // holds one of ~ ! @ # % ^ & | ` ?, so that "=-1" is "=" and "-".
        private void ReadOperator(int textEnd)
        {
            var operatorStart = _offset;
            var unusual = false;
            while (_offset < textEnd && IsOperatorCharacter(text[_offset]))
            {
                unusual |= text[_offset] is not ('+' or '-' or '*' or '/' or '<' or '>' or '=');
                _offset++;
            }

            while (!unusual && _offset - operatorStart > 1 && text[_offset - 1] is '+' or '-')
            {
                _offset--;
            }

            _word = TextSpan.FromBounds(operatorStart, _offset);
        }

        private static bool IsOperatorCharacter(char value) =>
            value
                is '+'
                    or '-'
                    or '*'
                    or '/'
                    or '<'
                    or '>'
                    or '='
                    or '~'
                    or '!'
                    or '@'
                    or '#'
                    or '%'
                    or '^'
                    or '&'
                    or '|'
                    or '`'
                    or '?';

        private void MoveToNextLexeme()
        {
            _lexeme++;
            if (_lexeme < end)
            {
                _offset = lexemes[_lexeme].Span.Start;
            }
        }
    }

    // An open parenthesis: whether it is that of a call to position, and what was open outside it, to be had again
    // when it closes.
    private readonly record struct Parenthesis(bool IsPosition, bool NotOutside, bool NotAboveOutside);
}
