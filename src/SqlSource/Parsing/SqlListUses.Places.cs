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
        // A word that does nothing to it but what its entry ends.
        None,

        // A built-in aggregate, whose call's parentheses are an aggregate's.
        Aggregate,

        // geometry or geography, whose aggregates are named after "::".
        SpatialType,

        // An aggregate of a spatial type, after its "::".
        SpatialAggregate,

        // GROUP, which opens GROUP BY before BY, and is that of WITHIN GROUP after WITHIN.
        Group,

        By,

        Output,

        Within,

        // INSERT, UPDATE, DELETE and MERGE, which start a statement that changes rows and may have an OUTPUT.
        Change,

        // SELECT, before which a statement's OUTPUT comes.
        Select,

        As,

        Not,

        // AND and OR.
        Junction,

        Is,

        Case,

        End,

        // IIF, whose condition is a condition of its own.
        Iif,
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

        // geometry or geography, before "::".
        SpatialType,

        // geometry: or geography:, before the second ":".
        SpatialColon,

        // geometry:: or geography::, before the name of an aggregate.
        SpatialColons,

        // AS, before the name it gives.
        As,

        // IS, before the NOT of IS NOT.
        Is,

        // IIF, before its "(".
        Iif,
    }

    /// <summary>
    /// Reads where each list stands: whether a <c>NOT</c> reaches it, and whether it is inside an aggregate, a
    /// <c>GROUP BY</c> or an <c>OUTPUT</c> clause, where SQL Server refuses a subquery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>NOT</c>, but for that of <c>IS NOT</c>, reaches what follows it until an <c>AND</c>, an <c>OR</c> or a
    /// comma at its depth, the <c>)</c> that closes the depth, or a word that starts a clause, a statement or a
    /// condition of its own, such as <c>WHERE</c>, <c>WHEN</c>, <c>GROUP</c> or <c>BEGIN</c>, and it reaches into every
    /// parenthesis opened inside it.  A condition of its own filters <c>UNKNOWN</c> as it does <c>FALSE</c>, so a
    /// <c>NOT</c> outside it never changes what an empty list there gives.  A <c>CASE</c>, and the parentheses of
    /// <c>IIF</c>, start with no <c>NOT</c>, and <c>END</c> gives back the <c>NOT</c> open before its <c>CASE</c>, so
    /// <c>NOT CASE … END IN @ids</c> negates the list.  This reading is not that of <c>Find</c>, which stays open
    /// past a clause.
    /// </para>
    /// <para>
    /// A list is inside an aggregate when it is inside the parentheses of a call to a built-in aggregate, or to an
    /// aggregate of <c>geometry::</c> or <c>geography::</c>, or of its <c>WITHIN GROUP</c>, that no <c>OVER</c>
    /// follows: a windowed aggregate takes a subquery.  <c>GROUP BY</c> opens a place at its depth, and so does
    /// <c>OUTPUT</c> in a statement that changes rows, before its <c>SELECT</c>, unless the word is a name: after
    /// <c>AS</c>, before <c>.</c>, an operator, a comma or a join.  A
    /// clause word at that depth, the <c>)</c> that closes the depth or a <c>;</c> ends the place, which holds in every
    /// parenthesis and <c>CASE</c> opened inside it.  A word after <c>.</c> is a name, and a quoted name is no word, so
    /// <c>t.output</c> and <c>dbo.SUM(…)</c> open nothing.
    /// </para>
    /// <para>
    /// The reader does nothing unless it is started, and its frames and the place of each use are in arrays rented
    /// from the pool, which <see cref="Return" /> gives back.
    /// </para>
    /// </remarks>
    private struct Places
    {
        private const string Over = "OVER";

        private const string WithinWord = "WITHIN";

        private const string GroupWord = "GROUP";

        private const SqlListPlace Both = SqlListPlace.InGroupBy | SqlListPlace.InOutput;

        // Words after which OUTPUT is the name of a table, not a clause.
        private static readonly string[] JoinWords = ["JOIN", "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "OUTER", "ON"];

        // The words the reader tells apart, the places that each ends at its depth, and whether it ends a NOT there.
        private static readonly (string Word, PlaceWord Kind, SqlListPlace Ends, bool EndsNot)[] Words =
        [
            ("APPROX_COUNT_DISTINCT", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("APPROX_PERCENTILE_CONT", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("APPROX_PERCENTILE_DISC", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("AVG", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("CHECKSUM_AGG", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("COUNT", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("COUNT_BIG", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("GROUPING", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("GROUPING_ID", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("JSON_ARRAYAGG", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("JSON_OBJECTAGG", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("MAX", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("MIN", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("PRODUCT", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("STDEV", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("STDEVP", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("STRING_AGG", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("SUM", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("VAR", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("VARP", PlaceWord.Aggregate, SqlListPlace.None, false),
            ("GEOMETRY", PlaceWord.SpatialType, SqlListPlace.None, false),
            ("GEOGRAPHY", PlaceWord.SpatialType, SqlListPlace.None, false),
            ("UNIONAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, false),
            ("ENVELOPEAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, false),
            ("COLLECTIONAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, false),
            ("CONVEXHULLAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, false),
            (GroupWord, PlaceWord.Group, SqlListPlace.None, true),
            ("BY", PlaceWord.By, SqlListPlace.None, false),
            ("OUTPUT", PlaceWord.Output, SqlListPlace.None, true),
            (WithinWord, PlaceWord.Within, SqlListPlace.None, false),
            ("AS", PlaceWord.As, SqlListPlace.None, false),
            ("NOT", PlaceWord.Not, SqlListPlace.None, false),
            ("AND", PlaceWord.Junction, SqlListPlace.None, false),
            ("OR", PlaceWord.Junction, SqlListPlace.None, false),
            ("IS", PlaceWord.Is, SqlListPlace.None, false),
            ("CASE", PlaceWord.Case, SqlListPlace.None, false),
            ("END", PlaceWord.End, SqlListPlace.None, false),
            ("IIF", PlaceWord.Iif, SqlListPlace.None, false),
            ("INSERT", PlaceWord.Change, SqlListPlace.InGroupBy, true),
            ("UPDATE", PlaceWord.Change, SqlListPlace.InGroupBy, true),
            ("DELETE", PlaceWord.Change, SqlListPlace.InGroupBy, true),
            ("MERGE", PlaceWord.Change, SqlListPlace.InGroupBy, true),
            ("SELECT", PlaceWord.Select, Both, true),
            ("HAVING", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("ORDER", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("FOR", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("UNION", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("EXCEPT", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("INTERSECT", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("WINDOW", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("WITH", PlaceWord.None, SqlListPlace.InGroupBy, true),
            ("OPTION", PlaceWord.None, Both, true),
            ("INTO", PlaceWord.None, SqlListPlace.InOutput, true),
            ("FROM", PlaceWord.None, SqlListPlace.InOutput, true),
            ("WHERE", PlaceWord.None, SqlListPlace.InOutput, true),
            ("VALUES", PlaceWord.None, SqlListPlace.InOutput, true),
            ("DEFAULT", PlaceWord.None, SqlListPlace.InOutput, true),
            ("EXEC", PlaceWord.None, SqlListPlace.InOutput, true),
            ("EXECUTE", PlaceWord.None, SqlListPlace.InOutput, true),
            ("ON", PlaceWord.None, SqlListPlace.None, true),
            ("WHEN", PlaceWord.None, SqlListPlace.None, true),
            ("THEN", PlaceWord.None, SqlListPlace.None, true),
            ("ELSE", PlaceWord.None, SqlListPlace.None, true),
            ("SET", PlaceWord.None, SqlListPlace.None, true),
            ("IF", PlaceWord.None, SqlListPlace.None, true),
            ("WHILE", PlaceWord.None, SqlListPlace.None, true),
            ("RETURN", PlaceWord.None, SqlListPlace.None, true),
            ("BEGIN", PlaceWord.None, SqlListPlace.None, true),
        ];

        // The words of each length, by length.  Each is compared ignoring case.
        private static readonly (string Word, PlaceWord Kind, SqlListPlace Ends, bool EndsNot)[][] WordsByLength =
        [
            .. Enumerable
                .Range(0, Words.Max(static entry => entry.Word.Length) + 1)
                .Select(static length => Words.Where(entry => entry.Word.Length == length).ToArray()),
        ];

        private readonly bool _started;

        // The place of each use, by its index.
        private readonly SqlListPlace[] _places;

        // The open frames, the outermost first, up to _top: the outermost level, each "(" and each CASE.
        private Frame[] _frames;

        private int _top;

        // Whether a NOT is open in the frame on top, and whether one is open in a frame that holds it.
        private bool _notHere;

        private bool _notAbove;

        // Whether the item before was ".", after which a word is a name.
        private bool _afterDot;

        private Begun _begun;

        // Whether the WITHIN GROUP after the aggregate's call that just closed is an aggregate's too.
        private bool _withinIsAggregate;

        private Places(int count)
        {
            _started = true;
            _places = ArrayPool<SqlListPlace>.Shared.Rent(Math.Max(count, 1));
            _frames = ArrayPool<Frame>.Shared.Rent(8);
            _frames[0] = default;
            _top = 0;
            _notHere = false;
            _notAbove = false;
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

        // The use of a parameter.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Parameter(int index, SqlParameterUse use)
        {
            if (_started)
            {
                var place = SqlListPlace.None;
                if (use is SqlParameterUse.List or SqlParameterUse.NegatedList)
                {
                    place = Held(_frames[_top]) | (_notHere || _notAbove ? SqlListPlace.UnderNot : SqlListPlace.None);
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
                ArrayPool<Frame>.Shared.Return(_frames);
            }
        }

        private static SqlListPlace Held(Frame frame) => frame.Inherited | frame.Opened;

        private static (PlaceWord Kind, SqlListPlace Ends, bool EndsNot) KindOf(ReadOnlySpan<char> word)
        {
            if (word.Length >= WordsByLength.Length)
            {
                return default;
            }

            // The words are upper-case, so a first letter of another case is compared without ToUpper.
            var first = (char)(word[0] & ~0x20);
            foreach (var (text, kind, ends, endsNot) in WordsByLength[word.Length])
            {
                if (text[0] == first && word.Equals(text.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return (kind, ends, endsNot);
                }
            }

            return default;
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

        // Whether the item after the word OUTPUT, that the cursor has just met, makes it a name: ".", an operator or a
        // comma, as in output.id, SET output = 1 or a = output, b = 2, or a join after it, as after a table's alias.
        // The cursor is a copy, so the walk goes on from the word.
        private static bool IsNameAhead(Cursor cursor)
        {
            var item = cursor.Next();
            return item switch
            {
                Item.Other => cursor.IsWord("."),
                Item.Operator or Item.Comma => true,
                Item.Word => cursor.IsAnyWord(JoinWords),
                Item.End
                or Item.Open
                or Item.Close
                or Item.OpenBracket
                or Item.CloseBracket
                or Item.Parameter
                or Item.Quoted => false,
                _ => throw UnexpectedEnumValueException.Of(item),
            };
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
                    Word(afterDot ? default : KindOf(cursor.Word), begun, cursor);
                    break;
                case Item.Open:
                    Open(begun, cursor);
                    break;
                case Item.Close:
                    Close();
                    break;
                case Item.Comma:
                    _notHere = false;
                    break;
                case Item.Other:
                    Other(begun, cursor);
                    break;
                case Item.End:
                case Item.OpenBracket:
                case Item.CloseBracket:
                case Item.Parameter:
                case Item.Operator:
                case Item.Quoted:
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(item);
            }
        }

        // A character such as "." or ":".  A ";" ends the statement, and every frame and place with it.
        private void Other(Begun begun, Cursor cursor)
        {
            if (cursor.IsWord(";"))
            {
                _top = 0;
                _frames[0] = default;
                _notHere = false;
                _notAbove = false;
                return;
            }

            if (cursor.IsWord(":"))
            {
                if (begun == Begun.SpatialType)
                {
                    _begun = Begun.SpatialColon;
                }
                else if (begun == Begun.SpatialColon)
                {
                    _begun = Begun.SpatialColons;
                }

                return;
            }

            _afterDot = cursor.IsWord(".");
        }

        private void Word((PlaceWord Kind, SqlListPlace Ends, bool EndsNot) word, Begun begun, Cursor cursor)
        {
            ref var top = ref _frames[_top];
            top.Opened &= ~word.Ends;
            if (word.EndsNot)
            {
                _notHere = false;
                _notAbove = false;
            }

            switch (word.Kind)
            {
                case PlaceWord.Aggregate:
                    _begun = Begun.Call;
                    break;
                case PlaceWord.SpatialType:
                    _begun = Begun.SpatialType;
                    break;
                case PlaceWord.SpatialAggregate:
                    _begun = begun == Begun.SpatialColons ? Begun.Call : Begun.None;
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
                    if (top.ChangesRows && begun != Begun.As && !IsNameAhead(cursor))
                    {
                        top.Opened |= SqlListPlace.InOutput;
                    }

                    break;
                case PlaceWord.Within:
                    _begun = begun == Begun.AfterCall ? Begun.Within : Begun.None;
                    break;
                case PlaceWord.Change:
                    top.ChangesRows = true;
                    break;
                case PlaceWord.Select:
                    top.ChangesRows = false;
                    break;
                case PlaceWord.As:
                    _begun = Begun.As;
                    break;
                case PlaceWord.Not:
                    _notHere |= begun != Begun.Is;
                    break;
                case PlaceWord.Junction:
                    _notHere = false;
                    break;
                case PlaceWord.Is:
                    _begun = Begun.Is;
                    break;
                case PlaceWord.Case:
                    Push(new Frame(Held(top), isCase: true, isCall: false, isAggregate: false, _notHere, _notAbove));
                    _notHere = false;
                    _notAbove = false;
                    break;
                case PlaceWord.End:
                    if (top.IsCase)
                    {
                        Pop();
                    }
                    else
                    {
                        _notHere = false;
                        _notAbove = false;
                    }

                    break;
                case PlaceWord.Iif:
                    _begun = Begun.Iif;
                    break;
                case PlaceWord.None:
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(word.Kind);
            }
        }

        // A "(" holds the places of its frame, and the NOT open there.  That of an aggregate's call, or of its WITHIN
        // GROUP, is inside the aggregate too, unless OVER follows the call, and that of IIF starts with no NOT.
        private void Open(Begun begun, Cursor cursor)
        {
            var isCall = begun == Begun.Call;
            var isAggregate = isCall ? !IsWindowed(cursor) : begun == Begun.WithinGroup && _withinIsAggregate;
            var held = Held(_frames[_top]) | (isAggregate ? SqlListPlace.InAggregate : SqlListPlace.None);
            Push(new Frame(held, isCase: false, isCall, isAggregate, _notHere, _notAbove));
            _notAbove = begun != Begun.Iif && (_notAbove || _notHere);
            _notHere = false;
        }

        // A ")" closes the frames down to the nearest "(", and one that closes nothing is passed over.
        private void Close()
        {
            var index = _top;
            while (index > 0 && _frames[index].IsCase)
            {
                index--;
            }

            if (index == 0)
            {
                return;
            }

            while (_top >= index)
            {
                Pop();
            }

            var closed = _frames[index];
            if (closed.IsCall)
            {
                _begun = Begun.AfterCall;
                _withinIsAggregate = closed.IsAggregate;
            }
        }

        private void Push(Frame frame)
        {
            if (_top + 1 == _frames.Length)
            {
                var grown = ArrayPool<Frame>.Shared.Rent(_frames.Length * 2);
                Array.Copy(_frames, grown, _frames.Length);
                ArrayPool<Frame>.Shared.Return(_frames);
                _frames = grown;
            }

            _top++;
            _frames[_top] = frame;
        }

        // Ends the frame on top, and gives back the NOT that was open outside it.
        private void Pop()
        {
            ref var frame = ref _frames[_top];
            _notHere = frame.NotOutside;
            _notAbove = frame.NotAboveOutside;
            _top--;
        }

        // The outermost level, a "(" or a CASE: the places it holds from outside it, those opened in it, and the NOT
        // open outside it.
        private struct Frame(
            SqlListPlace inherited,
            bool isCase,
            bool isCall,
            bool isAggregate,
            bool notOutside,
            bool notAboveOutside
        )
        {
            public readonly SqlListPlace Inherited = inherited;

            public readonly bool IsCase = isCase;

            // Whether the frame is the parentheses of an aggregate's call, and whether they are inside the aggregate.
            public readonly bool IsCall = isCall;

            public readonly bool IsAggregate = isAggregate;

            public readonly bool NotOutside = notOutside;

            public readonly bool NotAboveOutside = notAboveOutside;

            public SqlListPlace Opened;

            // Whether the statement of the frame changes rows, and has not reached its SELECT.
            public bool ChangesRows;
        }
    }
}
