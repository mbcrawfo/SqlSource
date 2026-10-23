using System;
using System.Collections.Generic;
using System.Linq;
using Kind = SqlSource.Tool.Describing.Postgres.Walk.PlanTokens.Kind;

namespace SqlSource.Tool.Describing.Postgres.Walk;

// The reader of nullability forms: whether the server can never return NULL for a span, always does, or the span is
// no form, with COALESCE, GREATEST, LEAST and a CASE over values that cannot be NULL.  A span that is no such form is
// not NULL when it is proved of a type (PlanExpression.Proofs.cs).
internal static partial class PlanExpression
{
    private static readonly HashSet<string> Variadic = [with(StringComparer.Ordinal), "COALESCE", "GREATEST", "LEAST"];

    private sealed partial class Reader
    {
        /// <summary>
        /// False, true or null, as <see cref="NullableOf(string, ExpressionContext)" /> answers, for the span as a
        /// whole.
        /// </summary>
        public bool? Form(int from, int to)
        {
            if (to <= from)
            {
                return null;
            }

            if (IsGroup(from, to))
            {
                if (Vetoed(from, to))
                {
                    return null;
                }

                if (IsTest(from + 1, to - 1))
                {
                    return false;
                }

                var outside = EnterGroup(from, to);
                var form = Form(from + 1, to - 1) ?? (Proved(from, to) is null ? null : false);
                _groupsHaveRows = outside;
                return form;
            }

            if (Constant(from, to) is { } constant)
            {
                return constant;
            }

            if (IsCountOrRanking(from, to))
            {
                return _context.CatalogFunctions ? false : null;
            }

            if (IsCatalogsCount(from, to))
            {
                return false;
            }

            if (IsVariadicCall(from, to))
            {
                return AnyArgumentIsNotNull(from + 2, to - 1) || FullJoinSides(from + 2, to - 1) is not null
                    ? false
                    : null;
            }

            if (IsCase(from, to) && CaseResultsAreNotNull(from, to))
            {
                return false;
            }

            // A reference is no form: it is proved only as part of one.
            return !IsReference(from, to) && Proved(from, to) is not null ? false : null;
        }

        // COALESCE(...), GREATEST(...) or LEAST(...), to the end.
        private bool IsVariadicCall(int from, int to) =>
            to - from >= 3
            && Is(from, Kind.Word)
            && Variadic.Contains(Text(from))
            && Is(from + 1, Kind.Open)
            && _closes[from + 1] == to - 1;

        // Whether an argument, between the commas of depth zero of [from, to), cannot be NULL.
        private bool AnyArgumentIsNotNull(int from, int to) =>
            ArgumentSpans(from, to).Any(argument => IsNotNull(argument.From, argument.To));

        // The types of the plain references among the arguments of [from, to) that the walk proves on a side of the
        // full join that computes the entry, when one is on each side; or null.  A row of a full join has a row of one
        // side at least, so one of the two is not NULL, and COALESCE, GREATEST and LEAST are NULL only when every
        // argument is.  Read only where the session finds pg_catalog first, as the forms read by their name are.
        private List<long>? FullJoinSides(int from, int to)
        {
            if (!_context.CatalogFunctions || _context.ProvedOnFullJoinSide is not { } sideOf)
            {
                return null;
            }

            var types = new List<long>();
            var outer = false;
            var inner = false;
            foreach (var (start, end) in ArgumentSpans(from, to))
            {
                if (
                    end > start
                    && OutputReference.Parse(Text(start, end)) is { } reference
                    && sideOf(reference) is { } side
                )
                {
                    outer |= side.OuterSide;
                    inner |= !side.OuterSide;
                    types.Add(side.Type);
                }
            }

            return outer && inner ? types : null;
        }

        // The arguments of [from, to), each between the commas of depth zero: one empty span for an empty span.
        private List<(int From, int To)> ArgumentSpans(int from, int to)
        {
            var arguments = new List<(int From, int To)>();
            var start = from;
            for (var at = from; at < to; at = Next(at))
            {
                if (Is(at, Kind.Comma))
                {
                    arguments.Add((start, at));
                    start = at + 1;
                }
            }

            arguments.Add((start, to));
            return arguments;
        }

        // A value that cannot be NULL: a form that answers false, or a reference the walk proves.
        private bool IsNotNull(int from, int to) =>
            to > from
            && (
                Form(from, to) == false
                || (
                    OutputReference.Parse(Text(from, to)) is { } reference
                    && _context.ProvedTypeOf(reference) is not null
                )
            );

        private bool IsCase(int from, int to) => to - from >= 2 && IsWord(from, "CASE") && IsWord(to - 1, "END");

        // Whether the CASE has an ELSE and its every result cannot be NULL.
        private bool CaseResultsAreNotNull(int from, int to) =>
            CaseResults(from, to) is { } results && results.TrueForAll(result => IsNotNull(result.From, result.To));

        // The results of the outermost CASE are after each THEN and after its ELSE, each up to the next WHEN, ELSE or
        // its END; a CASE nested without parentheses is counted, and one in parentheses is skipped whole.  A CASE whose
        // END comes before the last token is no CASE of the whole span, and one without a THEN or an ELSE has no
        // results: null.
        private List<(int From, int To)>? CaseResults(int from, int to)
        {
            var results = new List<(int From, int To)>();
            var nested = 0;
            var resultStart = -1;
            var sawThen = false;
            var sawElse = false;
            var closed = false;
            for (var at = from + 1; at < to; at = Next(at))
            {
                if (IsWord(at, "CASE"))
                {
                    nested++;
                    continue;
                }

                if (IsWord(at, "END"))
                {
                    if (nested > 0)
                    {
                        nested--;
                        continue;
                    }

                    // The outermost END must be the span's last token.
                    if (at != to - 1)
                    {
                        return null;
                    }

                    closed = true;
                }
                else if (nested > 0 || !(IsWord(at, "WHEN") || IsWord(at, "THEN") || IsWord(at, "ELSE")))
                {
                    continue;
                }

                if (resultStart >= 0)
                {
                    results.Add((resultStart, at));
                }

                resultStart = -1;
                if (IsWord(at, "THEN"))
                {
                    sawThen = true;
                    resultStart = at + 1;
                }
                else if (IsWord(at, "ELSE"))
                {
                    sawElse = true;
                    resultStart = at + 1;
                }
            }

            return closed && sawThen && sawElse ? results : null;
        }
    }
}
