using System;
using System.Buffers;
using System.Collections.Immutable;

namespace SqlSource.Parsing;

internal static partial class SqlListUses
{
    // What opened a frame.
    private enum FrameKind
    {
        // The outermost level, which no item opens.
        Outermost,

        // "(".
        Parenthesis,

        // "[", of an array or a subscript.
        Bracket,

        // CASE, which END closes.
        Case,
    }

    // What the operands of a frame stand in.
    private enum Context
    {
        // The output of the outermost statement: its SELECT list, ORDER BY, GROUP BY, RETURNING or SET, whose values
        // reach no filter, and any call, CASE or bracket inside it.
        Output,

        // A condition of WHERE, ON, HAVING or WHEN, which FALSE and NULL fail alike.
        Filter,

        // A parenthesis or a CASE result whose value is an operand of the frame around it.
        Group,

        // Anything else: an argument of a call, a subquery's own output, a row, a simple CASE's operand.
        Value,
    }

    // The words the reader tells apart.  A word of none of these is a name, a function or a word it reads as one.
    private enum Keyword
    {
        None,
        And,
        Or,
        Not,
        Where,
        On,
        Having,
        When,
        Then,
        Else,
        Case,
        End,
        Distinct,
        Within,
        Left,
        Right,
        From,
        Group,
        By,
        Select,
        Returning,

        // VALUES, INSERT, UPDATE and DELETE, which start a statement as SELECT does.
        Statement,

        // A word that ends a condition and starts a clause, but for the ones above.
        Clause,

        // A word that tests or compares the value before it, so that a list beside one is a value.
        Test,
    }

    /// <summary>
    /// Reads whether each list stands as a condition or as a value.  An empty list makes <c>x IN @ids</c>
    /// <c>FALSE</c> where the one value it is described as makes it <c>NULL</c>.  A condition fails either way, but a
    /// value can be turned true: <c>(x IN @ids) IS FALSE</c>, <c>= false</c>, a cast or a call.  So a list whose truth
    /// is used as a value is <see cref="SqlParameterUse.NegatedList" />, as one a <c>NOT</c> negates is.
    /// </summary>
    /// <remarks>
    /// A frame is opened at each <c>(</c>, <c>[</c> and <c>CASE</c>.  A list is a condition when it is reached from
    /// <c>WHERE</c>, <c>ON</c>, <c>HAVING</c> or <c>WHEN</c> through <c>AND</c>, <c>OR</c> and parentheses alone, with
    /// no comparison, test, operator or other word in its operand, or when it stands in the outermost statement's
    /// output.  Any other list is a value.  The reading never misses a list that the planner can read as strict, and
    /// marks some that it cannot, such as one in <c>COALESCE</c> or before <c>IS NOT TRUE</c>.  Its frames and the
    /// state of each use are in arrays rented from the pool, which <see cref="Return" /> gives back.
    /// </remarks>
    private struct Conditions
    {
        // The keywords of each length, by length.  Each is compared ignoring case.
        private static readonly (string Word, Keyword Keyword)[][] KeywordsByLength =
        [
            [],
            [],
            [("OR", Keyword.Or), ("ON", Keyword.On), ("BY", Keyword.By), ("IS", Keyword.Test), ("DO", Keyword.Clause)],
            [
                ("AND", Keyword.And),
                ("NOT", Keyword.Not),
                ("END", Keyword.End),
                ("FOR", Keyword.Clause),
                ("SET", Keyword.Clause),
            ],
            [
                ("WHEN", Keyword.When),
                ("THEN", Keyword.Then),
                ("ELSE", Keyword.Else),
                ("CASE", Keyword.Case),
                ("LEFT", Keyword.Left),
                ("FROM", Keyword.From),
                ("JOIN", Keyword.Clause),
                ("INTO", Keyword.Clause),
                ("FULL", Keyword.Clause),
                ("LIKE", Keyword.Test),
            ],
            [
                ("WHERE", Keyword.Where),
                ("GROUP", Keyword.Group),
                ("RIGHT", Keyword.Right),
                ("ORDER", Keyword.Clause),
                ("LIMIT", Keyword.Clause),
                ("FETCH", Keyword.Clause),
                ("UNION", Keyword.Clause),
                ("INNER", Keyword.Clause),
                ("CROSS", Keyword.Clause),
                ("OUTER", Keyword.Clause),
                ("USING", Keyword.Clause),
                ("ILIKE", Keyword.Test),
            ],
            [
                ("HAVING", Keyword.Having),
                ("WITHIN", Keyword.Within),
                ("SELECT", Keyword.Select),
                ("VALUES", Keyword.Statement),
                ("INSERT", Keyword.Statement),
                ("UPDATE", Keyword.Statement),
                ("DELETE", Keyword.Statement),
                ("OFFSET", Keyword.Clause),
                ("WINDOW", Keyword.Clause),
                ("EXCEPT", Keyword.Clause),
                ("ISNULL", Keyword.Test),
            ],
            [
                ("NATURAL", Keyword.Clause),
                ("LATERAL", Keyword.Clause),
                ("NOTNULL", Keyword.Test),
                ("SIMILAR", Keyword.Test),
                ("BETWEEN", Keyword.Test),
            ],
            [("DISTINCT", Keyword.Distinct), ("OVERLAPS", Keyword.Test)],
            [("RETURNING", Keyword.Returning), ("INTERSECT", Keyword.Clause)],
        ];

        // Comparisons, which bind less tightly than IN, so that one beside a list compares its truth.
        private static readonly string[] Comparisons = ["=", "<", ">", "<=", ">=", "<>", "!=", "!<", "!>"];

        // A use not waiting in any frame: a value, a null test, a list a NOT negates, or one already settled.
        private const int Settled = -1;

        private readonly ImmutableArray<SqlParameterUse>.Builder _uses;

        // Where each use waits, by its index: twice the depth of its frame, plus one when that frame carries it out as
        // an operand of the frame around it, or Settled.
        private readonly int[] _waits;

        private readonly int _count;

        // The open frames, the outermost first, up to _top.
        private Frame[] _frames;

        private int _top;

        // Whether a "(" now is a parenthesis of its own, by the item before it.
        private bool _groupFollows;

        // The word before, where a word reads by it: DISTINCT, after which FROM is that of IS DISTINCT FROM and ON
        // that of DISTINCT ON, or WITHIN, after which GROUP is that of WITHIN GROUP.
        private Keyword _before;

        // Whether the item before was ".", after which a word is a name.
        private bool _afterDot;

        public Conditions(ImmutableArray<SqlParameterUse>.Builder uses, int count)
        {
            _uses = uses;
            _count = count;
            _waits = ArrayPool<int>.Shared.Rent(Math.Max(count, 1));
            _waits.AsSpan(0, count).Fill(Settled);
            _frames = ArrayPool<Frame>.Shared.Rent(8);
            _frames[0] = new Frame(FrameKind.Outermost, Context.Output, inOutput: true);
            _top = 0;
            _groupFollows = true;
            _before = Keyword.None;
            _afterDot = false;
        }

        // A word.  The cursor is a copy, so it may look at the item after the word.
        public void Word(Cursor cursor)
        {
            var before = _before;
            var keyword = _afterDot ? Keyword.None : KeywordOf(cursor.Word);
            _before = keyword;
            _afterDot = false;
            _groupFollows = IsBeforeGroup(keyword);
            ref var top = ref _frames[_top];
            switch (keyword)
            {
                case Keyword.And:
                case Keyword.Or:
                    EndOperand(_top);
                    return;
                case Keyword.Where:
                case Keyword.Having:
                case Keyword.When:
                case Keyword.On when before != Keyword.Distinct:
                    EndOperand(_top);
                    top.Context = Context.Filter;
                    return;
                case Keyword.Then:
                case Keyword.Else:
                    if (top.Kind == FrameKind.Case)
                    {
                        EndOperand(_top);
                        top.Context = Context.Group;
                        return;
                    }

                    break;
                case Keyword.Case:
                    Push(FrameKind.Case, Context.Value);
                    return;
                case Keyword.End:
                    if (top.Kind == FrameKind.Case)
                    {
                        Pop();
                        return;
                    }

                    break;
                case Keyword.Test:
                    top.Clean = false;
                    return;
                case Keyword.None:
                case Keyword.Not:
                case Keyword.On:
                case Keyword.Distinct:
                case Keyword.Within:
                case Keyword.Left:
                case Keyword.Right:
                case Keyword.From:
                case Keyword.Group:
                case Keyword.By:
                case Keyword.Select:
                case Keyword.Returning:
                case Keyword.Statement:
                case Keyword.Clause:
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(keyword);
            }

            if (IsClause(keyword, before, cursor))
            {
                EndOperand(_top);
                if (top.Kind != FrameKind.Outermost && keyword is Keyword.Select or Keyword.Statement)
                {
                    top.InOutput = false;
                }

                top.Context = top.InOutput ? Context.Output : Context.Value;
                return;
            }

            MakeUnclean(ref top);
        }

        public void Open()
        {
            Push(FrameKind.Parenthesis, _groupFollows ? Context.Group : Context.Value);
            _groupFollows = true;
        }

        public void OpenBracket()
        {
            Push(FrameKind.Bracket, Context.Value);
            _groupFollows = true;
        }

        // A ")" closes the frames down to the nearest "(", and one that closes nothing is passed over.
        public void Close() => CloseTo(FrameKind.Parenthesis);

        // A "]" closes the frames down to the nearest "[", and one that closes nothing is passed over.
        public void CloseBracket() => CloseTo(FrameKind.Bracket);

        public void Comma()
        {
            ResetBefore(groupFollows: true);
            EndOperand(_top);
            ref var top = ref _frames[_top];
            top.Context = top.InOutput ? Context.Output : Context.Value;
        }

        // A run of operator characters.
        public void Operator(Cursor cursor)
        {
            ResetBefore(groupFollows: true);
            ref var top = ref _frames[_top];
            if (cursor.IsAnyWord(Comparisons))
            {
                top.Clean = false;
            }
            else
            {
                MakeUnclean(ref top);
            }
        }

        // Anything else that is not white space: a character such as "." or ":".  A ";" ends the statement.
        public void Other(Cursor cursor)
        {
            ResetBefore(groupFollows: true);
            if (cursor.IsWord(";"))
            {
                End();
                return;
            }

            _afterDot = cursor.IsWord(".");
            MakeUnclean(ref _frames[_top]);
        }

        // A quoted region.  One before "(" is a name, "f"(, so the parenthesis is a call's.
        public void Quoted()
        {
            ResetBefore(groupFollows: false);
            MakeUnclean(ref _frames[_top]);
        }

        // A parameter, and the use read of it so far: a list that no NOT negates waits for its operand to end.
        public void Parameter(int index, SqlParameterUse use)
        {
            ResetBefore(groupFollows: false);
            ref var top = ref _frames[_top];
            if (use == SqlParameterUse.List)
            {
                _waits[index] = _top * 2;
                top.Pending++;
                return;
            }

            MakeUnclean(ref top);
        }

        // The end of the query or of a statement: every frame is closed, and the outermost level starts again.
        public void End()
        {
            while (_top > 0)
            {
                Pop();
            }

            EndOperand(0);
            _frames[0].Context = Context.Output;
            ResetBefore(groupFollows: true);
        }

        // Gives the rented arrays back.
        public readonly void Return()
        {
            ArrayPool<int>.Shared.Return(_waits);
            ArrayPool<Frame>.Shared.Return(_frames);
        }

        private static Keyword KeywordOf(ReadOnlySpan<char> word)
        {
            if (word.Length >= KeywordsByLength.Length)
            {
                return Keyword.None;
            }

            // The keywords are upper-case letters, so a first letter of another case is compared without ToUpper.
            var first = (char)(word[0] & ~0x20);
            foreach (var (text, keyword) in KeywordsByLength[word.Length])
            {
                if (text[0] == first && word.Equals(text.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return keyword;
                }
            }

            return Keyword.None;
        }

        // Words after which a "(" is a parenthesis of its own, not the arguments of a call.
        private static bool IsBeforeGroup(Keyword keyword) =>
            keyword
                is Keyword.And
                    or Keyword.Or
                    or Keyword.Not
                    or Keyword.Where
                    or Keyword.On
                    or Keyword.Having
                    or Keyword.When
                    or Keyword.Then
                    or Keyword.Else
                    or Keyword.Select
                    or Keyword.By
                    or Keyword.Returning
                    or Keyword.Distinct
                    or Keyword.Case;

        // A word that ends an operand and the condition it is in: a clause word, or THEN, ELSE or END where no CASE is
        // open.  LEFT and RIGHT are clauses only when no "(" makes them calls, FROM is none after DISTINCT and GROUP
        // none after WITHIN.
        private static bool IsClause(Keyword keyword, Keyword before, Cursor cursor) =>
            keyword switch
            {
                Keyword.Left or Keyword.Right => cursor.Next() != Item.Open,
                Keyword.From => before != Keyword.Distinct,
                Keyword.Group => before != Keyword.Within,
                Keyword.Then
                or Keyword.Else
                or Keyword.End
                or Keyword.By
                or Keyword.Select
                or Keyword.Returning
                or Keyword.Statement
                or Keyword.Clause => true,
                Keyword.None
                or Keyword.And
                or Keyword.Or
                or Keyword.Not
                or Keyword.Where
                or Keyword.On
                or Keyword.Having
                or Keyword.When
                or Keyword.Case
                or Keyword.Distinct
                or Keyword.Within
                or Keyword.Test => false,
                _ => throw UnexpectedEnumValueException.Of(keyword),
            };

        // Whether a frame's operands now reach only the outermost output: the outermost level in its output, or a frame
        // opened inside it.
        private static bool SettlesAsOutput(Frame frame) =>
            frame.Kind == FrameKind.Outermost ? frame.Context == Context.Output : frame.InOutput;

        // Anything after a list's parameter applies to its truth, so it makes the operand of a list a value.
        private static void MakeUnclean(ref Frame frame)
        {
            if (frame.Pending > 0)
            {
                frame.Clean = false;
            }
        }

        private void ResetBefore(bool groupFollows)
        {
            _before = Keyword.None;
            _afterDot = false;
            _groupFollows = groupFollows;
        }

        // A frame opened inside the output of the outermost statement is output too, until a word that starts a
        // statement in it, a subquery's SELECT, makes it a value.
        private void Push(FrameKind kind, Context context)
        {
            MakeUnclean(ref _frames[_top]);
            var inOutput = SettlesAsOutput(_frames[_top]);
            if (_top + 1 == _frames.Length)
            {
                var grown = ArrayPool<Frame>.Shared.Rent(_frames.Length * 2);
                Array.Copy(_frames, grown, _frames.Length);
                ArrayPool<Frame>.Shared.Return(_frames);
                _frames = grown;
            }

            _top++;
            _frames[_top] = new Frame(kind, inOutput ? Context.Output : context, inOutput);
            _before = Keyword.None;
            _afterDot = false;
        }

        private void CloseTo(FrameKind kind)
        {
            ResetBefore(groupFollows: false);
            var index = _top;
            while (index > 0 && _frames[index].Kind != kind)
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
        }

        // Ends the frame on top, and makes the lists its parentheses carry operands of the frame around it.
        private void Pop()
        {
            EndOperand(_top);
            if (_frames[_top].Carried > 0)
            {
                var carried = (_top * 2) + 1;
                for (var index = 0; index < _count; index++)
                {
                    if (_waits[index] == carried)
                    {
                        _waits[index] = (_top - 1) * 2;
                        _frames[_top - 1].Pending++;
                    }
                }
            }

            _top--;
        }

        // Settles the lists of the operand that ends here by what the frame's operands stand in.  A frame inside the
        // outermost output settles every list as output, whatever its context: its values, a CASE's result or an
        // aggregate's FILTER, reach no filter of the query.
        private readonly void EndOperand(int depth)
        {
            ref var frame = ref _frames[depth];
            if (frame.Pending > 0)
            {
                var context = SettlesAsOutput(frame) ? Context.Output : frame.Context;
                var pending = depth * 2;
                for (var index = 0; index < _count; index++)
                {
                    if (_waits[index] != pending)
                    {
                        continue;
                    }

                    if (context == Context.Group && frame.Clean)
                    {
                        _waits[index] = pending + 1;
                        frame.Carried++;
                        continue;
                    }

                    if (context == Context.Value || (!frame.Clean && context != Context.Output))
                    {
                        _uses[index] = SqlParameterUse.NegatedList;
                    }

                    _waits[index] = Settled;
                }

                frame.Pending = 0;
            }

            frame.Clean = true;
        }

        private struct Frame(FrameKind kind, Context context, bool inOutput)
        {
            public readonly FrameKind Kind = kind;

            public Context Context = context;

            // Whether the frame stands in the output of the outermost statement, where a comma keeps it output.
            public bool InOutput = inOutput;

            // Whether no comparison, test or other item has met the operand, so far.
            public bool Clean = true;

            // How many lists of the operand, that no NOT negates, wait for it to end.
            public int Pending;

            // How many lists of a Group's operands ended clean, to become operands of the frame around it.
            public int Carried;
        }
    }
}
