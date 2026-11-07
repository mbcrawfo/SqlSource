using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SqlSource.Parsing;

internal static partial class SqlListUses
{
    // What a word does to the reading of places, beside the places it ends and its traits.
    private enum PlaceWord
    {
        // A word that does nothing more.
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

        // WINDOW, which starts its clause only in WINDOW name AS (.
        Window,

        // INSERT, UPDATE, DELETE and MERGE, which start a statement that changes rows and may have an OUTPUT.
        Change,

        // SELECT and WHERE, after which a statement has no OUTPUT.
        PastOutput,

        // FROM, which is that of IS DISTINCT FROM after DISTINCT.
        From,

        Distinct,

        Not,

        // AND and OR.
        Junction,

        Is,

        Case,

        End,

        // IIF, whose condition is a condition of its own.
        Iif,
    }

    // What else a word is to the reader.
    [Flags]
    private enum WordTraits
    {
        None = 0,

        // A reserved word that starts a clause, a statement or a condition of its own, and so ends a NOT.
        EndsNot = 1,

        // A reserved word after which the word OUTPUT is a name, not a clause.
        BeforeName = 2,
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

        // A WITHIN after anything else, as after PERCENTILE_CONT(…), before GROUP.
        OtherWithin,

        // The WITHIN GROUP after an aggregate's call, before its "(".
        WithinGroup,

        // geometry or geography, before "::".
        SpatialType,

        // geometry: or geography:, before the second ":".
        SpatialColon,

        // geometry:: or geography::, before the name of an aggregate.
        SpatialColons,

        // DISTINCT, before the FROM of IS DISTINCT FROM.
        Distinct,

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
    /// comma at its depth, the <c>)</c> that closes the depth, or a reserved word that starts a clause, a statement
    /// or a condition of its own, such as <c>WHERE</c>, <c>WHEN</c>, <c>GROUP</c> or <c>BEGIN</c>, and it reaches
    /// into every parenthesis opened inside it.  A word that can be a name, such as <c>OUTPUT</c> or <c>WINDOW</c>,
    /// ends none, nor does the <c>GROUP</c> of <c>WITHIN GROUP</c> or the <c>FROM</c> of <c>IS DISTINCT FROM</c>.  A
    /// condition of its own filters <c>UNKNOWN</c> as it does <c>FALSE</c>, so a <c>NOT</c> outside it never changes
    /// what an empty list there gives.  A <c>CASE</c>, and the parentheses of <c>IIF</c>, start with no <c>NOT</c>, and
    /// <c>END</c> gives back the <c>NOT</c> open before its <c>CASE</c>, so <c>NOT CASE … END IN @ids</c> negates the
    /// list.  This reading is not that of <c>Find</c>, which stays open past a clause.
    /// </para>
    /// <para>
    /// A list is inside an aggregate when it is inside the parentheses of a call to a built-in aggregate, or to an
    /// aggregate of <c>geometry::</c> or <c>geography::</c>, or of its <c>WITHIN GROUP</c>, that no <c>OVER</c>
    /// follows: a windowed aggregate takes a subquery.  <c>GROUP BY</c> opens a place at its depth.  So does
    /// <c>OUTPUT</c> where its clause stands, in a statement that changes rows and before its <c>WHERE</c> or
    /// <c>SELECT</c>, unless the word is a name: after an operator, a comma or a reserved word but <c>DELETE</c>,
    /// <c>END</c> and <c>DEFAULT</c>, or before <c>.</c>, an operator, a comma, <c>)</c>, a join or a word that tests
    /// it.  A clause word or a statement at that depth, the <c>)</c> that closes the depth or a <c>;</c> ends the
    /// place, which holds in every parenthesis and <c>CASE</c> opened inside it.  <c>WINDOW</c> ends a
    /// <c>GROUP BY</c> only in <c>WINDOW name AS (</c>.  A word after <c>.</c> is a name, and a quoted name is no
    /// word, so <c>t.output</c> and <c>dbo.SUM(…)</c> open nothing.
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

        private const string AsWord = "AS";

        private const SqlListPlace Both = SqlListPlace.InGroupBy | SqlListPlace.InOutput;

        // A reserved word of a clause or a statement.
        private const WordTraits Clause = WordTraits.EndsNot | WordTraits.BeforeName;

        // The words after which the word OUTPUT is a name: a join, or a word that tests or compares it.
        private static readonly string[] NameFollowers =
        [
            "JOIN",
            "INNER",
            "LEFT",
            "RIGHT",
            "FULL",
            "CROSS",
            "OUTER",
            "ON",
            "IN",
            "IS",
            "LIKE",
            "BETWEEN",
            "NOT",
            "AND",
            "OR",
            "COLLATE",
        ];

        // The words the reader tells apart, the places that each ends at its depth, and its traits.  Ending a NOT too
        // early sends a negated list the empty form that matches no row, a wrong result, where not ending it costs
        // at most an error.  So a word ends a NOT only when it is a reserved word of T-SQL, which can never be a
        // name there, and only in its use that starts a clause: never OUTPUT, WINDOW or THROW, nor GROUP after
        // WITHIN or FROM after DISTINCT.
        private static readonly (string Word, PlaceWord Kind, SqlListPlace Ends, WordTraits Traits)[] Words =
        [
            ("APPROX_COUNT_DISTINCT", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("APPROX_PERCENTILE_CONT", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("APPROX_PERCENTILE_DISC", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("AVG", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("CHECKSUM_AGG", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("COUNT", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("COUNT_BIG", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("GROUPING", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("GROUPING_ID", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("JSON_ARRAYAGG", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("JSON_OBJECTAGG", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("MAX", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("MIN", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("PRODUCT", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("STDEV", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("STDEVP", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("STRING_AGG", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("SUM", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("VAR", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("VARP", PlaceWord.Aggregate, SqlListPlace.None, WordTraits.None),
            ("GEOMETRY", PlaceWord.SpatialType, SqlListPlace.None, WordTraits.None),
            ("GEOGRAPHY", PlaceWord.SpatialType, SqlListPlace.None, WordTraits.None),
            ("UNIONAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, WordTraits.None),
            ("ENVELOPEAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, WordTraits.None),
            ("COLLECTIONAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, WordTraits.None),
            ("CONVEXHULLAGGREGATE", PlaceWord.SpatialAggregate, SqlListPlace.None, WordTraits.None),
            ("OUTPUT", PlaceWord.Output, SqlListPlace.None, WordTraits.None),
            (WithinWord, PlaceWord.Within, SqlListPlace.None, WordTraits.None),
            ("WINDOW", PlaceWord.Window, SqlListPlace.None, WordTraits.None),
            ("IIF", PlaceWord.Iif, SqlListPlace.None, WordTraits.None),
            ("THROW", PlaceWord.None, Both, WordTraits.None),
            (GroupWord, PlaceWord.Group, SqlListPlace.None, Clause),
            ("BY", PlaceWord.By, SqlListPlace.None, WordTraits.BeforeName),
            (AsWord, PlaceWord.None, SqlListPlace.None, WordTraits.BeforeName),
            ("NOT", PlaceWord.Not, SqlListPlace.None, WordTraits.BeforeName),
            ("AND", PlaceWord.Junction, SqlListPlace.None, WordTraits.BeforeName),
            ("OR", PlaceWord.Junction, SqlListPlace.None, WordTraits.BeforeName),
            ("IS", PlaceWord.Is, SqlListPlace.None, WordTraits.BeforeName),
            ("CASE", PlaceWord.Case, SqlListPlace.None, WordTraits.BeforeName),
            ("DISTINCT", PlaceWord.Distinct, SqlListPlace.None, WordTraits.BeforeName),
            ("END", PlaceWord.End, SqlListPlace.None, WordTraits.None),
            ("INSERT", PlaceWord.Change, Both, Clause),
            ("UPDATE", PlaceWord.Change, Both, Clause),
            ("DELETE", PlaceWord.Change, Both, WordTraits.EndsNot),
            ("MERGE", PlaceWord.Change, Both, Clause),
            ("SELECT", PlaceWord.PastOutput, Both, Clause),
            ("WHERE", PlaceWord.PastOutput, SqlListPlace.InOutput, Clause),
            ("FROM", PlaceWord.From, SqlListPlace.InOutput, Clause),
            ("HAVING", PlaceWord.None, SqlListPlace.InGroupBy, Clause),
            ("ORDER", PlaceWord.None, SqlListPlace.InGroupBy, Clause),
            ("FOR", PlaceWord.None, SqlListPlace.InGroupBy, Clause),
            ("UNION", PlaceWord.None, SqlListPlace.InGroupBy, Clause),
            ("EXCEPT", PlaceWord.None, SqlListPlace.InGroupBy, Clause),
            ("INTERSECT", PlaceWord.None, SqlListPlace.InGroupBy, Clause),
            ("WITH", PlaceWord.None, SqlListPlace.InGroupBy, Clause),
            ("OPTION", PlaceWord.None, Both, Clause),
            ("INTO", PlaceWord.None, SqlListPlace.InOutput, Clause),
            ("VALUES", PlaceWord.None, SqlListPlace.InOutput, Clause),
            ("EXEC", PlaceWord.None, SqlListPlace.InOutput, Clause),
            ("EXECUTE", PlaceWord.None, SqlListPlace.InOutput, Clause),
            ("DEFAULT", PlaceWord.None, SqlListPlace.InOutput, WordTraits.EndsNot),
            ("ON", PlaceWord.None, SqlListPlace.None, Clause),
            ("WHEN", PlaceWord.None, SqlListPlace.None, Clause),
            ("THEN", PlaceWord.None, SqlListPlace.None, Clause),
            ("ELSE", PlaceWord.None, SqlListPlace.None, Clause),
            ("SET", PlaceWord.None, Both, Clause),
            ("IF", PlaceWord.None, Both, Clause),
            ("WHILE", PlaceWord.None, Both, Clause),
            ("RETURN", PlaceWord.None, Both, Clause),
            ("BEGIN", PlaceWord.None, Both, Clause),
            ("DECLARE", PlaceWord.None, Both, WordTraits.BeforeName),
            ("PRINT", PlaceWord.None, Both, WordTraits.BeforeName),
            ("RAISERROR", PlaceWord.None, Both, WordTraits.BeforeName),
        ];

        // The words of each length, by length.  Each is compared ignoring case.
        private static readonly (string Word, PlaceWord Kind, SqlListPlace Ends, WordTraits Traits)[][] WordsByLength =
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

        // Whether the item before makes a word OUTPUT after it a name.
        private bool _beforeName;

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
            _beforeName = false;
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

        private static (PlaceWord Kind, SqlListPlace Ends, WordTraits Traits) KindOf(ReadOnlySpan<char> word)
        {
            if (word.Length >= WordsByLength.Length)
            {
                return default;
            }

            // The words are upper-case, so a first letter of another case is compared without ToUpper.
            var first = (char)(word[0] & ~0x20);
            foreach (var (text, kind, ends, traits) in WordsByLength[word.Length])
            {
                if (text[0] == first && word.Equals(text.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return (kind, ends, traits);
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

        // Whether the item after the word OUTPUT, that the cursor has just met, makes it a name: ".", an operator, a
        // comma or ")", as in output.id, SET output = 1, a = output, b = 2 or (output), or a word of NameFollowers,
        // as after a table's alias or before a test.  The cursor is a copy, so the walk goes on from the word.
        private static bool IsNameAhead(Cursor cursor)
        {
            var item = cursor.Next();
            return item switch
            {
                Item.Other => cursor.IsWord("."),
                Item.Operator or Item.Comma or Item.Close => true,
                Item.Word => cursor.IsAnyWord(NameFollowers),
                Item.End or Item.Open or Item.OpenBracket or Item.CloseBracket or Item.Parameter or Item.Quoted =>
                    false,
                _ => throw UnexpectedEnumValueException.Of(item),
            };
        }

        // Whether the words that the cursor meets after WINDOW are a name, AS and "(", as in WINDOW w AS (…).  The
        // cursor is a copy, so the walk goes on from the word.
        private static bool IsWindowClause(Cursor cursor)
        {
            if (cursor.Next() != Item.Word)
            {
                return false;
            }

            return cursor.Next() == Item.Word && cursor.IsWord(AsWord) && cursor.Next() == Item.Open;
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
            var beforeName = _beforeName;
            _begun = Begun.None;
            _afterDot = false;
            _beforeName = item is Item.Operator or Item.Comma;
            switch (item)
            {
                case Item.Word:
                    Word(afterDot ? default : KindOf(cursor.Word), begun, beforeName, cursor);
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

        private void Word(
            (PlaceWord Kind, SqlListPlace Ends, WordTraits Traits) word,
            Begun begun,
            bool beforeName,
            Cursor cursor
        )
        {
            // The GROUP of WITHIN GROUP and the FROM of IS DISTINCT FROM start no clause.
            if (
                (word.Kind == PlaceWord.Group && begun is Begun.Within or Begun.OtherWithin)
                || (word.Kind == PlaceWord.From && begun == Begun.Distinct)
            )
            {
                word = (word.Kind, SqlListPlace.None, word.Traits & ~WordTraits.EndsNot);
            }

            ref var top = ref _frames[_top];
            top.Opened &= ~word.Ends;
            if ((word.Traits & WordTraits.EndsNot) != WordTraits.None)
            {
                _notHere = false;
                _notAbove = false;
            }

            _beforeName = (word.Traits & WordTraits.BeforeName) != WordTraits.None;
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
                    if (begun == Begun.Within)
                    {
                        _begun = Begun.WithinGroup;
                    }
                    else if (begun != Begun.OtherWithin)
                    {
                        _begun = Begun.Group;
                    }

                    break;
                case PlaceWord.By:
                    if (begun == Begun.Group)
                    {
                        top.Opened |= SqlListPlace.InGroupBy;
                    }

                    break;
                case PlaceWord.Output:
                    if (top.MayOutput && !beforeName && !IsNameAhead(cursor))
                    {
                        top.Opened |= SqlListPlace.InOutput;
                    }

                    break;
                case PlaceWord.Within:
                    _begun = begun == Begun.AfterCall ? Begun.Within : Begun.OtherWithin;
                    break;
                case PlaceWord.Window:
                    if (IsWindowClause(cursor))
                    {
                        top.Opened &= ~SqlListPlace.InGroupBy;
                    }

                    break;
                case PlaceWord.Change:
                    top.MayOutput = true;
                    break;
                case PlaceWord.PastOutput:
                    top.MayOutput = false;
                    break;
                case PlaceWord.Distinct:
                    _begun = Begun.Distinct;
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
                case PlaceWord.From:
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

            // Whether the frame's statement changes rows and has not reached its WHERE or SELECT, where its OUTPUT
            // clause may stand.
            public bool MayOutput;
        }
    }
}
