using System;
using System.Collections.Generic;
using Kind = SqlSource.Tool.Describing.Postgres.Walk.PlanTokens.Kind;

namespace SqlSource.Tool.Describing.Postgres.Walk;

// The proofs of types: the type of a span that is a value never NULL of a known type, and the type of a constant, a
// test, count or a ranking that the text alone fixes, with the built-in types a quoted literal's cast names.  The
// catalog's operators, functions, casts and aggregates that a proof reads are in PlanExpression.Forms.cs.
internal static partial class PlanExpression
{
    // The built-in types of a quoted literal's cast, as the plan spells them without a modifier.
    private static readonly Dictionary<string, long> TypesByName = new(StringComparer.Ordinal)
    {
        ["text"] = BuiltInTypes.Text,
        ["character varying"] = BuiltInTypes.Varchar,
        ["bpchar"] = BuiltInTypes.Bpchar,
        ["character"] = BuiltInTypes.Bpchar,
        ["smallint"] = BuiltInTypes.Int2,
        ["integer"] = BuiltInTypes.Int4,
        ["bigint"] = BuiltInTypes.Int8,
        ["numeric"] = BuiltInTypes.Numeric,
        ["real"] = BuiltInTypes.Float4,
        ["double precision"] = BuiltInTypes.Float8,
        ["date"] = BuiltInTypes.Date,
        ["timestamp without time zone"] = BuiltInTypes.Timestamp,
        ["timestamp with time zone"] = BuiltInTypes.Timestamptz,
        ["boolean"] = BuiltInTypes.Bool,
    };

    private sealed partial class Reader
    {
        /// <summary>
        /// The type of the span as a whole when it is a value that is never <c>NULL</c> of a known type, or null.  A
        /// constant, a test, <c>count(*)</c>, a ranking or a <c>count</c> only <c>pg_catalog</c> holds is of the type
        /// <see cref="TypeOid" /> gives; a reference that is part of the entry is of the type the walk proves; an
        /// operator, a function or a cast of <c>pg_catalog</c> over such values is of the type
        /// <see cref="TotalFunctions" /> or the cast gives, and a call of <see cref="CatalogOverloads" /> of the type
        /// it gives.  A value of no known type, <see cref="TotalFunctions.UnknownType" />, matches no operator,
        /// function or cast.
        /// </summary>
        public long? Proved(int from, int to)
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
                    return BuiltInTypes.Bool;
                }

                // A reference in parentheses of its own is a value a node took from elsewhere: the reference is no
                // proof of it.
                if (IsReference(from + 1, to - 1))
                {
                    return null;
                }

                if (SubPlanAt(from + 1, to - 1) is { Hashed: false } scalar)
                {
                    return _context.ScalarSubPlanTypeOf?.Invoke(scalar.Name);
                }

                var outside = EnterGroup(from, to);
                var proved =
                    OperatorAt(from + 1, to - 1) is null && !IsWord(from + 1, "ANY")
                        ? Proved(from + 1, to - 1)
                        : CatalogForm(from, to);
                _groupsHaveRows = outside;
                return proved;
            }

            if (Constant(from, to) == false || IsCountOrRanking(from, to) || IsCatalogsCount(from, to))
            {
                return TypeOid(from, to);
            }

            // EXISTS(SubPlan n) starts false and turns true at the subplan's first row, and MERGE_ACTION() is the
            // action a MERGE took or an error: neither is a function a search path can find, and a function of the name
            // prints in quotes.
            if (IsWord(from, "EXISTS") && ClosesAtTheEnd(from, to) && SubPlanAt(from + 2, to - 1) is { Hashed: false })
            {
                return BuiltInTypes.Bool;
            }

            if (IsWord(from, "MERGE_ACTION") && ClosesAtTheEnd(from, to) && to - from == 3)
            {
                return BuiltInTypes.Text;
            }

            if (OutputReference.Parse(Text(from, to)) is { } reference)
            {
                return from > 0 || to < _tokens.Count ? _context.ProvedTypeOf(reference) : null;
            }

            return CatalogForm(from, to);
        }

        /// <summary>The OID, or null, as <see cref="TypeOf" /> answers, for the span as a whole.</summary>
        public long? TypeOid(int from, int to)
        {
            if (to <= from)
            {
                return null;
            }

            if (IsGroup(from, to))
            {
                return IsTest(from + 1, to - 1) ? BuiltInTypes.Bool : TypeOid(from + 1, to - 1);
            }

            if (to - from == 1)
            {
                if (IsWord(from, "true") || IsWord(from, "false"))
                {
                    return BuiltInTypes.Bool;
                }

                // A number the plan prints bare is an integer without a point and a numeric with one: a bigint or a
                // float is a literal with its cast.
                if (!Is(from, Kind.Number))
                {
                    return null;
                }

                return Text(from).Contains('.', StringComparison.Ordinal) ? BuiltInTypes.Numeric : BuiltInTypes.Int4;
            }

            // The plan prints a type's name bare whenever the search path finds it, text and date as any other: a
            // user's type of that name in a schema ahead of pg_catalog prints the same.
            if (
                _context.CatalogFunctions
                && to - from >= 3
                && Is(from, Kind.String)
                && Is(from + 1, Kind.Cast)
                && IsTypeName(from + 2, to)
            )
            {
                return BuiltInType(from + 2, to);
            }

            if (_context.CatalogFunctions && IsCountOrRanking(from, to, anyArgument: true))
            {
                return Text(from) is "percent_rank" or "cume_dist" ? BuiltInTypes.Float8 : BuiltInTypes.Int8;
            }

            return null;
        }

        // The type a name of one or more words spells, without a modifier in parentheses, if it is a built-in one that
        // is listed.  An array, a schema or a quoted name is none.
        private long? BuiltInType(int from, int to)
        {
            var words = new List<string>();
            var at = from;
            while (at < to)
            {
                if (Is(at, Kind.Word))
                {
                    words.Add(Text(at));
                    at++;
                }
                else if (Is(at, Kind.Open))
                {
                    at = _closes[at] + 1;
                }
                else
                {
                    return null;
                }
            }

            return TypesByName.TryGetValue(string.Join(' ', words), out var oid) ? oid : null;
        }
    }
}
