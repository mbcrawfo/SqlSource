using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource.Parsing;

internal static partial class SqlListUses
{
    // What the operands of a frame stand in.
    private enum Context
    {
        // The output of the outermost statement: its SELECT list, ORDER BY, GROUP BY, RETURNING or SET, whose values
        // reach no filter.
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
    /// A frame is opened at each <c>(</c> and each <c>CASE</c>.  A list is a condition when it is reached from
    /// <c>WHERE</c>, <c>ON</c>, <c>HAVING</c> or <c>WHEN</c> through <c>AND</c>, <c>OR</c> and parentheses alone, with
    /// no comparison, test, operator or other word in its operand, or when it stands in the outermost statement's
    /// output.  Any other list is a value.  The reading never misses a list that the planner can read as strict, and
    /// marks some that it cannot, such as one in <c>COALESCE</c> or before <c>IS NOT TRUE</c>.
    /// </remarks>
    private sealed class Conditions(ImmutableArray<SqlParameterUse>.Builder uses)
    {
        // Words that end an operand and the condition it is in: what follows is output at the outermost level, and a
        // value in a frame.
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
            "OUTPUT",
            "OPTION",
            "UPDATE",
            "DELETE",
            "INSERT",
        ];

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
        private readonly List<Frame> _frames = [new Frame(Context.Output, isCase: false)];

        // Whether a "(" now is a parenthesis of its own, by the item before it.
        private bool _groupFollows = true;

        // Whether the word before is DISTINCT, after which FROM is that of IS DISTINCT FROM.
        private bool _afterDistinct;

        private Frame Top => _frames[_frames.Count - 1];

        private bool IsOutermost => _frames.Count == 1;

        // A word.  The cursor is a copy, so it may look at the item after the word.
        public void Word(Cursor cursor)
        {
            var afterDistinct = _afterDistinct;
            _afterDistinct = cursor.IsWord("DISTINCT");
            _groupFollows = cursor.IsAnyWord(BeforeGroup);
            var top = Top;
            if (cursor.IsWord(And) || cursor.IsWord(Or))
            {
                EndOperand(top);
            }
            else if (cursor.IsWord("WHERE") || cursor.IsWord("ON") || cursor.IsWord("HAVING") || cursor.IsWord("WHEN"))
            {
                EndOperand(top);
                top.Context = Context.Filter;
            }
            else if (cursor.IsWord("THEN") || cursor.IsWord("ELSE"))
            {
                EndOperand(top);
                top.Context = top.IsCase ? Context.Group : AfterClause();
            }
            else if (cursor.IsWord("CASE"))
            {
                MakeUnclean(top);
                _frames.Add(new Frame(Context.Value, isCase: true));
            }
            else if (cursor.IsWord("END") && top.IsCase)
            {
                Pop();
            }
            else if (cursor.IsWord("END") || IsClause(cursor, afterDistinct))
            {
                EndOperand(top);
                top.Context = AfterClause();
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
            var top = Top;
            MakeUnclean(top);
            _frames.Add(new Frame(_groupFollows ? Context.Group : Context.Value, isCase: false));
            _groupFollows = true;
            _afterDistinct = false;
        }

        // A ")" closes the frames down to the nearest "(", one that closes nothing is passed over.
        public void Close()
        {
            _afterDistinct = false;
            _groupFollows = false;
            var index = _frames.Count - 1;
            while (index > 0 && _frames[index].IsCase)
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

        public void Comma()
        {
            _afterDistinct = false;
            _groupFollows = true;
            var top = Top;
            EndOperand(top);
            top.Context = AfterClause();
        }

        // A run of operator characters.
        public void Operator(Cursor cursor)
        {
            _afterDistinct = false;
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

        // Anything else that is not white space: a quoted region, or a character such as "." or ":".  A ";" ends the
        // statement.
        public void Other(Cursor cursor)
        {
            _afterDistinct = false;
            _groupFollows = true;
            if (cursor.IsWord(";"))
            {
                End();
                return;
            }

            MakeUnclean(Top);
        }

        // A parameter, and the use read of it so far: a list that no NOT negates waits for its operand to end.
        public void Parameter(int index, SqlParameterUse use)
        {
            _afterDistinct = false;
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
            _afterDistinct = false;
        }

        // A clause word, but for LEFT and RIGHT, which are clauses only when no "(" makes them calls, and FROM after
        // DISTINCT.
        private static bool IsClause(Cursor cursor, bool afterDistinct)
        {
            if (cursor.IsWord("LEFT") || cursor.IsWord("RIGHT"))
            {
                return cursor.Next() != Item.Open;
            }

            if (cursor.IsWord("FROM") && afterDistinct)
            {
                return false;
            }

            return cursor.IsAnyWord(Clauses);
        }

        private Context AfterClause() => IsOutermost ? Context.Output : Context.Value;

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
        private void EndOperand(Frame frame)
        {
            if (frame.Pending is { Count: > 0 } pending)
            {
                foreach (var index in pending)
                {
                    if (frame.Context == Context.Group && frame.Clean)
                    {
                        (frame.Carried ??= []).Add(index);
                    }
                    else if (frame.Context == Context.Value || (!frame.Clean && frame.Context != Context.Output))
                    {
                        uses[index] = SqlParameterUse.NegatedList;
                    }
                }

                pending.Clear();
            }

            frame.Clean = true;
        }

        private sealed class Frame(Context context, bool isCase)
        {
            public Context Context { get; set; } = context;

            public bool IsCase { get; } = isCase;

            // Whether no comparison, test or other item has met the operand, so far.
            public bool Clean { get; set; } = true;

            // The lists of the operand, by their index among the uses, that no NOT negates.
            public List<int>? Pending { get; set; }

            // The lists of a Group's operands that ended clean, which become operands of the frame around it.
            public List<int>? Carried { get; set; }
        }
    }
}
