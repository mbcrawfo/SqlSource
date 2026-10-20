using System;
using System.Collections.Generic;
using System.Globalization;

namespace SqlSource.Tool.Describing.Postgres.Walk;

// The reader's forms of pg_catalog, which a search path decides: an operator, a function, a cast, an aggregate and the
// ANY over a subplan's column, with how a subplan's name and its column are printed.
internal static partial class PlanExpression
{
    private sealed partial class Reader
    {
        // An operator in a group of its own, a cast or a function call, each the catalog's only when the session finds
        // pg_catalog first, and so is the name of the type a cast prints: the one place that reads any of them.
        private long? CatalogForm(int from, int to)
        {
            if (!_context.CatalogFunctions)
            {
                return null;
            }

            if (IsGroup(from, to))
            {
                if (IsWord(from + 1, "ANY"))
                {
                    return Any(from + 2, to - 1);
                }

                return OperatorAt(from + 1, to - 1) is { } at ? Operator(from + 1, at, to - 1) : null;
            }

            return Is(from, Kind.Open) ? Cast(from, to) : Function(from, to) ?? Aggregate(from, to);
        }

        // The group of ANY's one comparison, (L op (SubPlan n).colk) or with hashed, which is NULL only when a
        // comparison of L with a row of the subplan is: the executor ORs them, and the hashed form is NULL only for a
        // NULL on either side.  L and the subplan's column must be proved, and op a comparison of the table over their
        // types.  A comparison of rows, or a column with a cast, is none.
        private long? Any(int from, int to)
        {
            if (
                !IsGroup(from, to)
                || Vetoed(from, to)
                || OperatorAt(from + 1, to - 1) is not { } at
                || SubPlanColumnAt(at + 1, to - 1) is not var (name, column)
            )
            {
                return null;
            }

            var outside = EnterGroup(from, to);
            var left = Proved(from + 1, at);
            _groupsHaveRows = outside;
            return
                left is { } proved
                && _context.SubPlanColumnTypeOf?.Invoke(name, column) is { } right
                && TotalFunctions.OperatorResult(Text(at), proved, right) == BuiltInTypes.Bool
                ? BuiltInTypes.Bool
                : null;
        }

        // SubPlan n, or hashed SubPlan n, as the whole span: the name of a subplan as the plan prints it inside the
        // parentheses of its value.
        private (string Name, bool Hashed)? SubPlanAt(int from, int to)
        {
            var hashed = IsWord(from, "hashed");
            var at = hashed ? from + 1 : from;
            return to - at == 2 && IsWord(at, "SubPlan") && Is(at + 1, Kind.Number)
                ? ("SubPlan " + Text(at + 1), hashed)
                : null;
        }

        // (SubPlan n).colk, or with hashed, as the whole span: the subplan's name and the column's number from 1.
        private (string Name, int Column)? SubPlanColumnAt(int from, int to)
        {
            if (!Is(from, Kind.Open))
            {
                return null;
            }

            var close = _closes[from];
            if (
                close + 3 != to
                || SubPlanAt(from + 1, close) is not { } subPlan
                || !Is(close + 1, Kind.Dot)
                || !Is(close + 2, Kind.Word)
            )
            {
                return null;
            }

            var word = Text(close + 2);
            return
                word.StartsWith("col", StringComparison.Ordinal)
                && int.TryParse(word.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var column)
                && column > 0
                && string.Equals(word, "col" + column.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                ? (subPlan.Name, column)
                : null;
        }

        // The index of the one operator between two operands of [from, to), the one token of no other kind at depth
        // zero; or null when there is none or several.  EXPLAIN prints each operator in parentheses of its own, so a
        // span with two is no single operator.
        private int? OperatorAt(int from, int to)
        {
            int? found = null;
            for (var at = from; at < to; at = Next(at))
            {
                if (!Is(at, Kind.Other))
                {
                    continue;
                }

                if (found is not null)
                {
                    return null;
                }

                found = at;
            }

            return found;
        }

        // A binary operator of the table over two proved operands; a prefix operator, with no left operand, is none.
        private long? Operator(int from, int at, int to)
        {
            if (Proved(from, at) is not { } left || Proved(at + 1, to) is not { } right)
            {
                return null;
            }

            return TotalFunctions.OperatorResult(Text(at), left, right);
        }

        // (E)::T, where E is proved of a type that carries to T within a family of SetOperationTypes: pg_cast holds
        // one row for a pair, the server ships one for every ordered pair of a family and pins it, and none returns
        // NULL for a value that is not.  T is a built-in type's name, with a modifier allowed.
        private long? Cast(int from, int to)
        {
            var close = _closes[from];
            if (
                close < 0
                || close + 2 >= to
                || !Is(close + 1, Kind.Cast)
                || !IsTypeName(close + 2, to)
                || Vetoed(from, close + 1)
                || BuiltInType(close + 2, to) is not { } target
            )
            {
                return null;
            }

            var outside = EnterGroup(from, close + 1);
            var source = Proved(from + 1, close);
            _groupsHaveRows = outside;
            return source is { } proved && SetOperationTypes.Carries(proved, target) ? target : null;
        }

        // name(a, ...) with a lower-case bare name, to the end, a function of the table over proved arguments.  A name
        // with a schema or in quotes is not the one the session resolves from the bare name.
        private long? Function(int from, int to)
        {
            if (
                !IsCall(from, to)
                || !TotalFunctions.HasFunction(Text(from), ArgumentCount(from + 2, to - 1))
                || Arguments(from + 2, to - 1) is not { } arguments
            )
            {
                return null;
            }

            return TotalFunctions.FunctionResult(Text(from), arguments);
        }

        // name([DISTINCT] a, ... [ORDER BY ...]) as a function is, an aggregate of the table over proved arguments,
        // read only where the node's groups each have a row.  Nothing follows its parenthesis: a FILTER, an OVER or a
        // WITHIN GROUP makes it no such form.  The ORDER BY and the DISTINCT change neither whether it can be NULL.
        private long? Aggregate(int from, int to)
        {
            if (!_groupsHaveRows || !IsCall(from, to))
            {
                return null;
            }

            var close = to - 1;
            var start = IsWord(from + 2, "DISTINCT") ? from + 3 : from + 2;
            var end = close;
            for (var at = start; at < close; at = Next(at))
            {
                if (IsWord(at, "ORDER") && IsWord(at + 1, "BY"))
                {
                    end = at;
                    break;
                }
            }

            return
                end > start
                && TotalFunctions.HasAggregate(Text(from), ArgumentCount(start, end))
                && Arguments(start, end) is { } arguments
                ? TotalFunctions.AggregateResult(Text(from), arguments)
                : null;
        }

        // The number of arguments of [from, to), between the commas of depth zero: none for an empty span.
        private int ArgumentCount(int from, int to)
        {
            if (to <= from)
            {
                return 0;
            }

            var count = 1;
            for (var at = from; at < to; at = Next(at))
            {
                if (Is(at, Kind.Comma))
                {
                    count++;
                }
            }

            return count;
        }

        // Whether the span is a lower-case bare name and the parentheses of its call, to the end.
        private bool IsCall(int from, int to) => IsLowerWord(from) && ClosesAtTheEnd(from, to);

        // Whether the token after the first opens parentheses that the span's last token closes.
        private bool ClosesAtTheEnd(int from, int to) =>
            from + 1 < to && Is(from + 1, Kind.Open) && _closes[from + 1] == to - 1;

        // The type of each argument of [from, to), between the commas of depth zero, when every one is proved; none
        // for an empty span; or null.
        private List<long>? Arguments(int from, int to)
        {
            var arguments = new List<long>();
            var start = from;
            for (var at = start; at < to; at = Next(at))
            {
                if (Is(at, Kind.Comma))
                {
                    if (Proved(start, at) is not { } argument)
                    {
                        return null;
                    }

                    arguments.Add(argument);
                    start = at + 1;
                }
            }

            if (start < to || arguments.Count > 0)
            {
                if (Proved(start, to) is not { } last)
                {
                    return null;
                }

                arguments.Add(last);
            }

            return arguments;
        }
    }
}
