using System.Collections.Generic;
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
    /// marks some that it cannot, such as one in <c>COALESCE</c> or before <c>IS NOT TRUE</c>.
    /// </remarks>
    private sealed class Conditions(ImmutableArray<SqlParameterUse>.Builder uses)
    {
        // Words that end an operand and the condition it is in: what follows is output at the outermost level, and a
        // value in a frame.  A word after "." is a name, never one of these.
        private static readonly string[] Clauses =
        [
            "SELECT",
            "FROM",
            "GROUP",
            "ORDER",
            "BY",
            "LIMIT",
            "OFFSET",
            "FETCH",
            "FOR",
            "UNION",
            "INTERSECT",
            "EXCEPT",
            "WINDOW",
            "RETURNING",
            "SET",
            "VALUES",
            "USING",
            "DO",
            "JOIN",
            "INNER",
            "CROSS",
            "FULL",
            "NATURAL",
            "OUTER",
            "LATERAL",
            "INTO",
            "UPDATE",
            "DELETE",
            "INSERT",
        ];

        // Words that start a statement, which make a frame inside the output a subquery, whose own output is a value.
        private static readonly string[] Statements = ["SELECT", "VALUES", "INSERT", "UPDATE", "DELETE"];

        // Words that end an operand where no CASE is open: THEN and ELSE of a MERGE, and the END of a block.
        private static readonly string[] Ends = ["THEN", "ELSE", "END"];

        // Words that test or compare the value before them, so that a list beside one is a value.
        private static readonly string[] Tests =
        [
            "IS",
            "ISNULL",
            "NOTNULL",
            "LIKE",
            "ILIKE",
            "SIMILAR",
            "BETWEEN",
            "OVERLAPS",
        ];

        // Words after which a "(" is a parenthesis of its own, not the arguments of a call.
        private static readonly string[] BeforeGroup =
        [
            And,
            Or,
            Not,
            "WHERE",
            "ON",
            "HAVING",
            "WHEN",
            "THEN",
            "ELSE",
            "SELECT",
            "BY",
            "RETURNING",
            "DISTINCT",
            "CASE",
        ];

        // Comparisons, which bind less tightly than IN, so that one beside a list compares its truth.
        private static readonly string[] Comparisons = ["=", "<", ">", "<=", ">=", "<>", "!=", "!<", "!>"];

        // The open frames, the outermost first.  It is never empty.
        private readonly List<Frame> _frames = [new Frame(FrameKind.Outermost, Context.Output, inOutput: true)];

        // Whether a "(" now is a parenthesis of its own, by the item before it.
        private bool _groupFollows = true;

        // What the item before was, where a word reads by it.
        private Before _before;

        // The items before a word that change what it is.
        private enum Before
        {
            Other,

            // DISTINCT, after which FROM is that of IS DISTINCT FROM.
            Distinct,

            // WITHIN, after which GROUP is that of WITHIN GROUP.
            Within,

            // ".", after which a word is a name.
            Dot,
        }

        private Frame Top => _frames[_frames.Count - 1];

        // A word.  The cursor is a copy, so it may look at the item after the word.
        public void Word(Cursor cursor)
        {
            var before = _before;
            _before = Before.Other;
            if (cursor.IsWord("DISTINCT"))
            {
                _before = Before.Distinct;
            }
            else if (cursor.IsWord("WITHIN"))
            {
                _before = Before.Within;
            }

            _groupFollows = before != Before.Dot && cursor.IsAnyWord(BeforeGroup);
            var top = Top;
            if (before == Before.Dot)
            {
                MakeUnclean(top);
            }
            else if (cursor.IsWord(And) || cursor.IsWord(Or))
            {
                EndOperand(top);
            }
            else if (
                cursor.IsWord("WHERE")
                || (cursor.IsWord("ON") && before != Before.Distinct)
                || cursor.IsWord("HAVING")
                || cursor.IsWord("WHEN")
            )
            {
                EndOperand(top);
                top.Context = Context.Filter;
            }
            else if ((cursor.IsWord("THEN") || cursor.IsWord("ELSE")) && top.Kind == FrameKind.Case)
            {
                EndOperand(top);
                top.Context = Context.Group;
            }
            else if (cursor.IsWord("CASE"))
            {
                Push(FrameKind.Case, Context.Value);
            }
            else if (cursor.IsWord("END") && top.Kind == FrameKind.Case)
            {
                Pop();
            }
            else if (cursor.IsAnyWord(Ends) || IsClause(cursor, before))
            {
                EndOperand(top);
                if (top.Kind != FrameKind.Outermost && cursor.IsAnyWord(Statements))
                {
                    top.InOutput = false;
                }

                top.Context = AfterClause(top);
            }
            else if (cursor.IsAnyWord(Tests))
            {
                top.Clean = false;
            }
            else
            {
                MakeUnclean(top);
            }
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
            _before = Before.Other;
            _groupFollows = true;
            var top = Top;
            EndOperand(top);
            top.Context = AfterClause(top);
        }

        // A run of operator characters.
        public void Operator(Cursor cursor)
        {
            _before = Before.Other;
            _groupFollows = true;
            var top = Top;
            if (cursor.IsAnyWord(Comparisons))
            {
                top.Clean = false;
            }
            else
            {
                MakeUnclean(top);
            }
        }

        // Anything else that is not white space: a character such as "." or ":".  A ";" ends the statement.
        public void Other(Cursor cursor)
        {
            _before = cursor.IsWord(".") ? Before.Dot : Before.Other;
            _groupFollows = true;
            if (cursor.IsWord(";"))
            {
                End();
                return;
            }

            MakeUnclean(Top);
        }

        // A quoted region.  One before "(" is a name, "f"(, so the parenthesis is a call's.
        public void Quoted()
        {
            _before = Before.Other;
            _groupFollows = false;
            MakeUnclean(Top);
        }

        // A parameter, and the use read of it so far: a list that no NOT negates waits for its operand to end.
        public void Parameter(int index, SqlParameterUse use)
        {
            _before = Before.Other;
            _groupFollows = false;
            var top = Top;
            if (use == SqlParameterUse.List)
            {
                (top.Pending ??= []).Add(index);
                return;
            }

            MakeUnclean(top);
        }

        // The end of the query or of a statement: every frame is closed, and the outermost level starts again.
        public void End()
        {
            while (_frames.Count > 1)
            {
                Pop();
            }

            var top = Top;
            EndOperand(top);
            top.Context = Context.Output;
            _groupFollows = true;
            _before = Before.Other;
        }

        // A clause word, but for LEFT and RIGHT, which are clauses only when no "(" makes them calls, FROM after
        // DISTINCT and GROUP after WITHIN.
        private static bool IsClause(Cursor cursor, Before before)
        {
            if (cursor.IsWord("LEFT") || cursor.IsWord("RIGHT"))
            {
                return cursor.Next() != Item.Open;
            }

            if (
                (cursor.IsWord("FROM") && before == Before.Distinct)
                || (cursor.IsWord("GROUP") && before == Before.Within)
            )
            {
                return false;
            }

            return cursor.IsAnyWord(Clauses);
        }

        // A frame opened inside the output of the outermost statement is output too, until a word that starts a
        // statement in it, a subquery's SELECT, makes it a value.
        private void Push(FrameKind kind, Context context)
        {
            var top = Top;
            MakeUnclean(top);
            var inOutput = SettlesAsOutput(top);
            _frames.Add(new Frame(kind, inOutput ? Context.Output : context, inOutput));
            _before = Before.Other;
        }

        private void CloseTo(FrameKind kind)
        {
            _before = Before.Other;
            _groupFollows = false;
            var index = _frames.Count - 1;
            while (index > 0 && _frames[index].Kind != kind)
            {
                index--;
            }

            if (index == 0)
            {
                return;
            }

            while (_frames.Count > index)
            {
                Pop();
            }
        }

        // Whether a frame's operands now reach only the outermost output: the outermost level in its output, or a frame
        // opened inside it.
        private static bool SettlesAsOutput(Frame frame) =>
            frame.Kind == FrameKind.Outermost ? frame.Context == Context.Output : frame.InOutput;

        private static Context AfterClause(Frame frame) => frame.InOutput ? Context.Output : Context.Value;

        // Anything after a list's parameter applies to its truth, so it makes the operand of a list a value.
        private static void MakeUnclean(Frame frame)
        {
            if (frame.Pending is { Count: > 0 })
            {
                frame.Clean = false;
            }
        }

        // Ends the frame on top, and makes the lists its parentheses carried operands of the frame around it.
        private void Pop()
        {
            var frame = Top;
            EndOperand(frame);
            _frames.RemoveAt(_frames.Count - 1);
            if (frame.Carried is { Count: > 0 })
            {
                (Top.Pending ??= []).AddRange(frame.Carried);
            }
        }

        // Settles the lists of the operand that ends here by what the frame's operands stand in.
        // A frame inside the outermost output settles every list as output, whatever its context: its values, a CASE's
        // result or an aggregate's FILTER, reach no filter of the query.
        private void EndOperand(Frame frame)
        {
            if (frame.Pending is { Count: > 0 } pending)
            {
                var context = SettlesAsOutput(frame) ? Context.Output : frame.Context;
                foreach (var index in pending)
                {
                    if (context == Context.Group && frame.Clean)
                    {
                        (frame.Carried ??= []).Add(index);
                    }
                    else if (context == Context.Value || (!frame.Clean && context != Context.Output))
                    {
                        uses[index] = SqlParameterUse.NegatedList;
                    }
                }

                pending.Clear();
            }

            frame.Clean = true;
        }

        private sealed class Frame(FrameKind kind, Context context, bool inOutput)
        {
            public FrameKind Kind { get; } = kind;

            public Context Context { get; set; } = context;

            // Whether the frame stands in the output of the outermost statement, where a comma keeps it output.
            public bool InOutput { get; set; } = inOutput;

            // Whether no comparison, test or other item has met the operand, so far.
            public bool Clean { get; set; } = true;

            // The lists of the operand, by their index among the uses, that no NOT negates.
            public List<int>? Pending { get; set; }

            // The lists of a Group's operands that ended clean, which become operands of the frame around it.
            public List<int>? Carried { get; set; }
        }
    }
}
