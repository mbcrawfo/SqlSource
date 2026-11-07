using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SqlSource.Parsing;

internal static partial class SqlListUses
{
    // What a word does to the reading of places.
    private enum PlaceWord
    {
        // A word that does nothing to it.
        None,

        // A built-in aggregate, whose call's parentheses are an aggregate's.
        Aggregate,

        // GROUP, which opens GROUP BY before BY, and is that of WITHIN GROUP after WITHIN.
        Group,

        By,

        Output,

        Within,

        // A word that ends, at its depth, the places it names.
        Clause,
    }

    // What the items before have begun, and the next item can finish.
    private enum Begun
    {
        None,

        // The name of a built-in aggregate, before the "(" of its call.
        Call,

        // GROUP, before the BY of GROUP BY.
        Group,

        // The ")" of an aggregate's call, before WITHIN.
        AfterCall,

        // The WITHIN after an aggregate's call, before GROUP.
        Within,

        // The WITHIN GROUP after an aggregate's call, before its "(".
        WithinGroup,
    }

    /// <summary>
    /// Reads where each list stands: whether a <c>NOT</c> reaches it, and whether it is inside an aggregate, a
    /// <c>GROUP BY</c> or an <c>OUTPUT</c> clause, where SQL Server refuses a subquery.
    /// </summary>
    /// <remarks>
    /// A list is under a <c>NOT</c> when the walk of <c>Find</c> reads one open where it stands; a list that
    /// <see cref="Conditions" /> reads as a value is not.  It is inside an aggregate when it is inside the parentheses
    /// of a call to a built-in aggregate, or of its <c>WITHIN GROUP</c>, that no <c>OVER</c> follows: a windowed
    /// aggregate takes a subquery.  <c>GROUP BY</c> and <c>OUTPUT</c> open a place at their depth, which a clause word
    /// at that depth, the <c>)</c> that closes the depth or a <c>;</c> ends.  A place holds in every parenthesis opened
    /// inside it.  A word after <c>.</c> is a name, and a quoted name is no word, so <c>t.output</c> and
    /// <c>dbo.SUM(…)</c> open nothing.  The reader does nothing unless it is started, and its depths and the place of
    /// each use are in arrays rented from the pool, which <see cref="Return" /> gives back.
    /// </remarks>
    private struct Places
    {
        private const string Over = "OVER";

        private const string WithinWord = "WITHIN";

        private const string GroupWord = "GROUP";

        // The words the reader tells apart, and the places that each clause word ends.
        private static readonly (string Word, PlaceWord Kind, SqlListPlace Ends)[] Words =
        [
            ("APPROX_COUNT_DISTINCT", PlaceWord.Aggregate, SqlListPlace.None),
            ("APPROX_PERCENTILE_CONT", PlaceWord.Aggregate, SqlListPlace.None),
            ("APPROX_PERCENTILE_DISC", PlaceWord.Aggregate, SqlListPlace.None),
            ("AVG", PlaceWord.Aggregate, SqlListPlace.None),
            ("CHECKSUM_AGG", PlaceWord.Aggregate, SqlListPlace.None),
            ("COUNT", PlaceWord.Aggregate, SqlListPlace.None),
            ("COUNT_BIG", PlaceWord.Aggregate, SqlListPlace.None),
            ("GROUPING", PlaceWord.Aggregate, SqlListPlace.None),
            ("GROUPING_ID", PlaceWord.Aggregate, SqlListPlace.None),
            ("JSON_ARRAYAGG", PlaceWord.Aggregate, SqlListPlace.None),
            ("JSON_OBJECTAGG", PlaceWord.Aggregate, SqlListPlace.None),
            ("MAX", PlaceWord.Aggregate, SqlListPlace.None),
            ("MIN", PlaceWord.Aggregate, SqlListPlace.None),
            ("STDEV", PlaceWord.Aggregate, SqlListPlace.None),
            ("STDEVP", PlaceWord.Aggregate, SqlListPlace.None),
            ("STRING_AGG", PlaceWord.Aggregate, SqlListPlace.None),
            ("SUM", PlaceWord.Aggregate, SqlListPlace.None),
            ("VAR", PlaceWord.Aggregate, SqlListPlace.None),
            ("VARP", PlaceWord.Aggregate, SqlListPlace.None),
            (GroupWord, PlaceWord.Group, SqlListPlace.None),
            ("BY", PlaceWord.By, SqlListPlace.None),
            ("OUTPUT", PlaceWord.Output, SqlListPlace.None),
            (WithinWord, PlaceWord.Within, SqlListPlace.None),
            ("HAVING", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("ORDER", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("FOR", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("UNION", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("EXCEPT", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("INTERSECT", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("WINDOW", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("WITH", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("INSERT", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("UPDATE", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("DELETE", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("MERGE", PlaceWord.Clause, SqlListPlace.InGroupBy),
            ("SELECT", PlaceWord.Clause, SqlListPlace.InGroupBy | SqlListPlace.InOutput),
            ("OPTION", PlaceWord.Clause, SqlListPlace.InGroupBy | SqlListPlace.InOutput),
            ("INTO", PlaceWord.Clause, SqlListPlace.InOutput),
            ("FROM", PlaceWord.Clause, SqlListPlace.InOutput),
            ("WHERE", PlaceWord.Clause, SqlListPlace.InOutput),
            ("VALUES", PlaceWord.Clause, SqlListPlace.InOutput),
            ("DEFAULT", PlaceWord.Clause, SqlListPlace.InOutput),
            ("EXEC", PlaceWord.Clause, SqlListPlace.InOutput),
            ("EXECUTE", PlaceWord.Clause, SqlListPlace.InOutput),
        ];

        // The words of each length, by length.  Each is compared ignoring case.
        private static readonly (string Word, PlaceWord Kind, SqlListPlace Ends)[][] WordsByLength =
        [
            .. Enumerable
                .Range(0, Words.Max(static entry => entry.Word.Length) + 1)
                .Select(static length => Words.Where(entry => entry.Word.Length == length).ToArray()),
        ];

        private readonly bool _started;

        // The place of each use, by its index.
        private readonly SqlListPlace[] _places;

        // The open depths, the outermost first, up to _top.
        private Depth[] _depths;

        private int _top;

        // Whether the item before was ".", after which a word is a name.
        private bool _afterDot;

        private Begun _begun;

        // Whether the WITHIN GROUP after the aggregate's call that just closed is an aggregate's too.
        private bool _withinIsAggregate;

        private Places(int count)
        {
            _started = true;
            _places = ArrayPool<SqlListPlace>.Shared.Rent(Math.Max(count, 1));
            _depths = ArrayPool<Depth>.Shared.Rent(8);
            _depths[0] = default;
            _top = 0;
            _afterDot = false;
            _begun = Begun.None;
            _withinIsAggregate = false;
        }

        // A reader that reads the places of that many uses.  The default reader reads nothing.
        public static Places Start(int count) => new(count);

        // An item of the query.  A parameter's place is read by Parameter, after this.  The cursor is a copy, so it may
        // look ahead.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(Item item, Cursor cursor)
        {
            if (_started)
            {
                ReadStarted(item, cursor);
            }
        }

        // The use of a parameter, and whether a NOT reaches it.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Parameter(int index, SqlParameterUse use, bool isUnderNot)
        {
            if (_started)
            {
                var place = SqlListPlace.None;
                if (use is SqlParameterUse.List or SqlParameterUse.NegatedList)
                {
                    place = Held(_depths[_top]) | (isUnderNot ? SqlListPlace.UnderNot : SqlListPlace.None);
                }

                _places[index] = place;
            }
        }

        // The places of that many uses read so far.
        public readonly ImmutableArray<SqlListPlace> Result(int count) => ImmutableArray.Create(_places, 0, count);

        // Gives the rented arrays back.
        public readonly void Return()
        {
            if (_started)
            {
                ArrayPool<SqlListPlace>.Shared.Return(_places);
                ArrayPool<Depth>.Shared.Return(_depths);
            }
        }

        private static SqlListPlace Held(Depth depth) => depth.Inherited | depth.Opened;

        private static (PlaceWord Kind, SqlListPlace Ends) KindOf(ReadOnlySpan<char> word)
        {
            if (word.Length >= WordsByLength.Length)
            {
                return (PlaceWord.None, SqlListPlace.None);
            }

            // The words are upper-case, so a first letter of another case is compared without ToUpper.
            var first = (char)(word[0] & ~0x20);
            foreach (var (text, kind, ends) in WordsByLength[word.Length])
            {
                if (text[0] == first && word.Equals(text.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return (kind, ends);
                }
            }

            return (PlaceWord.None, SqlListPlace.None);
        }

        // Whether OVER follows the call whose "(" the cursor has just met, after its WITHIN GROUP (…), if any.  The
        // cursor is a copy, so the walk goes on from the "(".
        private static bool IsWindowed(Cursor cursor)
        {
            if (!PassParentheses(ref cursor))
            {
                return false;
            }

            var item = cursor.Next();
            if (item == Item.Word && cursor.IsWord(WithinWord))
            {
                if (
                    cursor.Next() != Item.Word
                    || !cursor.IsWord(GroupWord)
                    || cursor.Next() != Item.Open
                    || !PassParentheses(ref cursor)
                )
                {
                    return false;
                }

                item = cursor.Next();
            }

            return item == Item.Word && cursor.IsWord(Over);
        }

        // Moves the cursor past the ")" that closes the "(" it has just met, and says whether it found one.
        private static bool PassParentheses(ref Cursor cursor)
        {
            var depth = 1;
            for (var item = cursor.Next(); item != Item.End; item = cursor.Next())
            {
                if (item == Item.Open)
                {
                    depth++;
                }
                else if (item == Item.Close && --depth == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ReadStarted(Item item, Cursor cursor)
        {
            var begun = _begun;
            var afterDot = _afterDot;
            _begun = Begun.None;
            _afterDot = false;
            switch (item)
            {
                case Item.Word:
                    Word(afterDot ? (PlaceWord.None, SqlListPlace.None) : KindOf(cursor.Word), begun);
                    break;
                case Item.Open:
                    Open(begun, cursor);
                    break;
                case Item.Close:
                    Close();
                    break;
                case Item.Other:
                    if (cursor.IsWord(";"))
                    {
                        _top = 0;
                        _depths[0] = default;
                    }

                    _afterDot = cursor.IsWord(".");
                    break;
                case Item.End:
                case Item.OpenBracket:
                case Item.CloseBracket:
                case Item.Comma:
                case Item.Parameter:
                case Item.Operator:
                case Item.Quoted:
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(item);
            }
        }

        private void Word((PlaceWord Kind, SqlListPlace Ends) word, Begun begun)
        {
            ref var top = ref _depths[_top];
            switch (word.Kind)
            {
                case PlaceWord.Aggregate:
                    _begun = Begun.Call;
                    break;
                case PlaceWord.Group:
                    _begun = begun == Begun.Within ? Begun.WithinGroup : Begun.Group;
                    break;
                case PlaceWord.By:
                    if (begun == Begun.Group)
                    {
                        top.Opened |= SqlListPlace.InGroupBy;
                    }

                    break;
                case PlaceWord.Output:
                    top.Opened |= SqlListPlace.InOutput;
                    break;
                case PlaceWord.Within:
                    _begun = begun == Begun.AfterCall ? Begun.Within : Begun.None;
                    break;
                case PlaceWord.Clause:
                    top.Opened &= ~word.Ends;
                    break;
                case PlaceWord.None:
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(word.Kind);
            }
        }

        // A "(" holds the places of its depth.  That of an aggregate's call, or of its WITHIN GROUP, is inside the
        // aggregate too, unless OVER follows the call.
        private void Open(Begun begun, Cursor cursor)
        {
            var isCall = begun == Begun.Call;
            var isAggregate = isCall ? !IsWindowed(cursor) : begun == Begun.WithinGroup && _withinIsAggregate;
            var held = Held(_depths[_top]) | (isAggregate ? SqlListPlace.InAggregate : SqlListPlace.None);
            if (_top + 1 == _depths.Length)
            {
                var grown = ArrayPool<Depth>.Shared.Rent(_depths.Length * 2);
                Array.Copy(_depths, grown, _depths.Length);
                ArrayPool<Depth>.Shared.Return(_depths);
                _depths = grown;
            }

            _top++;
            _depths[_top] = new Depth(held, SqlListPlace.None, isCall, isAggregate);
        }

        // A ")" ends its depth and the places opened there, and one that closes nothing is passed over.
        private void Close()
        {
            if (_top == 0)
            {
                return;
            }

            var closed = _depths[_top];
            _top--;
            if (closed.IsCall)
            {
                _begun = Begun.AfterCall;
                _withinIsAggregate = closed.IsAggregate;
            }
        }

        // A depth of parentheses: the places it holds from outside it, and those opened at it.
        private struct Depth(SqlListPlace inherited, SqlListPlace opened, bool isCall, bool isAggregate)
        {
            public readonly SqlListPlace Inherited = inherited;

            public SqlListPlace Opened = opened;

            // Whether the depth is the parentheses of an aggregate's call, and whether they are inside the aggregate.
            public readonly bool IsCall = isCall;

            public readonly bool IsAggregate = isAggregate;
        }
    }
}
