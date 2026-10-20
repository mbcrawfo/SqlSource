using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// An entry of a plan's <c>Output</c> read as a whole: whether the server can ever return <c>NULL</c> for it, for the
/// few forms whose answer the text alone settles, and the type of the few whose type it settles too.
/// </summary>
/// <remarks>
/// <para>
/// <c>EXPLAIN</c> prints each entry without its pretty-printing, so every operator, every test and every boolean
/// combination is in parentheses of its own: <c>(a IS NULL) AND b</c> prints as <c>((a IS NULL) AND b)</c>.  A form
/// is recognised only as the whole of a span, with its quotes and its parentheses balanced, and never as a prefix:
/// <c>(count(*) + NULL::integer)</c> is no <c>count</c>.  A span wrapped whole in parentheses is the span inside
/// them, which is how a node prints a value its child computed.
/// </para>
/// <para>
/// The forms: <c>count(*)</c>, with an optional <c>FILTER</c> and an optional <c>OVER</c>; <c>row_number()</c>,
/// <c>rank()</c>, <c>dense_rank()</c>, <c>percent_rank()</c> and <c>cume_dist()</c> with an <c>OVER</c>; a constant,
/// a quoted literal with its cast, a number, <c>true</c> or <c>false</c>, and <c>NULL::type</c>, which is the one
/// that can be <c>NULL</c>; the tests <c>IS NULL</c>, <c>IS NOT NULL</c> and <c>IS DISTINCT FROM</c>, and the
/// <c>NOT</c> of the last, which is how <c>IS NOT DISTINCT FROM</c> prints; <c>COALESCE</c>, <c>GREATEST</c> and
/// <c>LEAST</c> with an argument that cannot be <c>NULL</c>; and a <c>CASE</c> with an <c>ELSE</c> whose every result
/// cannot be.  A <c>CASE</c> written without an <c>ELSE</c> prints <c>ELSE NULL::type</c>.
/// </para>
/// <para>
/// A value is also proved with its type (<see cref="ProvedTypeOf" />): a constant, a test, <c>count(*)</c> or a
/// ranking of the type <see cref="TypeOf" /> gives, and a reference that is part of the entry of the type the walk
/// proves.  Over such values, an operator in parentheses of its own, <c>(A op B)</c>, and a function,
/// <c>name(a, ...)</c>, are not <c>NULL</c> when <see cref="TotalFunctions" /> holds their name with exactly the
/// argument types read, of the result it gives: the plan prints each argument with its implicit cast, so its type is
/// what the text shows.  A cast, <c>(E)::T</c>, is not <c>NULL</c> when <c>E</c> is proved of a type that is
/// <c>T</c> or in one family of <see cref="SetOperationTypes" /> with it; any other cast is unknown.
/// <c>COALESCE</c>, <c>GREATEST</c>, <c>LEAST</c> and <c>CASE</c> are not <c>NULL</c> but of no known type, so they
/// feed no operator, and a reference in parentheses of its own, <c>(u.id)</c>, is unknown.
/// </para>
/// <para>
/// An aggregate, <c>name([DISTINCT] a, ... [ORDER BY ...])</c> with nothing after its parenthesis, is not <c>NULL</c>
/// when the node that computes the entry is one each of whose groups has a row
/// (<see cref="ExpressionContext.GroupsHaveRows" />), every argument before the <c>ORDER BY</c> is proved, and
/// <see cref="TotalFunctions" /> holds its name with exactly their types.  A <c>FILTER</c>, an <c>OVER</c> or a
/// <c>WITHIN GROUP</c> makes it no such form: a filter may leave a group no row, and a window's frame may be empty.
/// Inside a group that a child of the node printed (<see cref="ExpressionContext.GroupFromChild" />) no aggregate is
/// read, since the child computed it under its own grouping.
/// </para>
/// <para>
/// A subquery is a <c>SubPlan</c> that the plan prints by its name, and the walk says what its plan proves.
/// <c>(SubPlan n)</c> is of the type <see cref="ExpressionContext.ScalarSubPlanTypeOf" /> gives.  <c>EXISTS(SubPlan
/// n)</c> is a boolean that is never <c>NULL</c>, and so is <c>(ANY (L op (SubPlan n).colk))</c>, with
/// <c>hashed</c> or without, when <c>L</c> is proved, <see cref="ExpressionContext.SubPlanColumnTypeOf" /> gives the
/// type of column <c>k</c>, and <c>op</c> over the two is a comparison of <see cref="TotalFunctions" />.
/// <c>MERGE_ACTION()</c> is a text that is never <c>NULL</c>.  <c>ARRAY</c>, <c>ALL</c>, a comparison of rows and the
/// <c>rescan</c> of a multiple assignment are no form.
/// </para>
/// <para>
/// A function name is the catalog's only when the plan prints it unqualified and the session finds
/// <c>pg_catalog</c>'s functions first (<see cref="PostgresSearchPath" />): the plan prints a function bare whenever
/// the search path finds it, so <c>count(*)</c> may be another schema's aggregate.  An operator, a cast and a
/// literal's type are read only then too, since the plan prints an operator and a type's name bare whenever the path
/// finds them.  <c>COALESCE</c>, <c>GREATEST</c>, <c>LEAST</c>, <c>CASE</c> and the tests are syntax, and no search
/// path changes them.
/// </para>
/// <para>
/// The text alone cannot tell a value the root computes from one a node under a join computed and printed by its
/// expression, so the caller asks only of a plan where no set operation prints its first branch at the root, and
/// either no join null-extends a side or the entry is read at the node that computes it, where a span in parentheses
/// that a node under a join computed may be marked unknown (<see cref="NullabilityWalk" />).
/// </para>
/// </remarks>
internal static class PlanExpression
{
    private static readonly HashSet<string> Rankings =
    [
        with(StringComparer.Ordinal),
        "row_number",
        "rank",
        "dense_rank",
        "percent_rank",
        "cume_dist",
    ];

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

    private static readonly HashSet<string> Variadic = [with(StringComparer.Ordinal), "COALESCE", "GREATEST", "LEAST"];

    // A context that proves no reference and reads no name as the catalog's.
    private static readonly ExpressionContext NoContext = new(false, static _ => null);

    /// <summary>What the entry says of its column.</summary>
    /// <param name="entry">The entry as the plan prints it.</param>
    /// <param name="context">What the walk says of the plan the entry is read in.</param>
    /// <returns>
    /// False when the server can never return <c>NULL</c> for the entry, true for a <c>NULL</c> constant, and null
    /// when the entry as a whole is no form this class knows.
    /// </returns>
    public static bool? NullableOf(string entry, ExpressionContext context) =>
        Tokens.Read(entry) is { Count: > 0 } tokens ? new Reader(entry, tokens, context).Form(0, tokens.Count) : null;

    /// <summary>
    /// What the entry says of its column, as <see cref="NullableOf(string, ExpressionContext)" /> answers, with a
    /// reference the walk proves of no known type.
    /// </summary>
    /// <param name="entry">The entry as the plan prints it.</param>
    /// <param name="catalogFunctions">Whether an unqualified function name is <c>pg_catalog</c>'s.</param>
    /// <param name="provedNotNull">
    /// Whether the walk proves a reference that is part of the entry not <c>NULL</c>.  A reference that is the whole
    /// entry is never asked.
    /// </param>
    /// <param name="groupPreserved">As <see cref="ExpressionContext.GroupPreserved" /> says.</param>
    public static bool? NullableOf(
        string entry,
        bool catalogFunctions,
        Func<OutputReference, bool> provedNotNull,
        Func<string, bool>? groupPreserved = null
    ) =>
        NullableOf(
            entry,
            new ExpressionContext(
                catalogFunctions,
                reference => provedNotNull(reference) ? TotalFunctions.UnknownType : null,
                groupPreserved
            )
        );

    /// <summary>
    /// The type of an entry that is, as a whole, a value proved not <c>NULL</c> of a known type, or null: a constant,
    /// a test, <c>count(*)</c> or a ranking as <see cref="TypeOf" /> types them, or an operator, a function or a cast
    /// of the forms the remarks give.  <c>COALESCE</c>, <c>GREATEST</c>, <c>LEAST</c> and <c>CASE</c> are not
    /// <c>NULL</c> but of no known type, and a reference that is the whole entry is never asked.
    /// </summary>
    /// <param name="entry">The entry as the plan prints it.</param>
    /// <param name="context">What the walk says of the plan the entry is read in.</param>
    /// <returns>The OID of the type, or null.</returns>
    public static long? ProvedTypeOf(string entry, ExpressionContext context) =>
        Tokens.Read(entry) is { Count: > 0 } tokens ? new Reader(entry, tokens, context).Proved(0, tokens.Count) : null;

    /// <summary>
    /// The type of an entry that is a whole constant or a form of this class whose type is fixed, or null.  The forms
    /// are those of <see cref="NullableOf(string, ExpressionContext)" /> with a type that does not depend on its
    /// arguments: a quoted literal with a cast to a built-in type, a number, <c>true</c> or <c>false</c>, a test,
    /// <c>count</c> and the rankings.  <c>NULL::type</c>, <c>COALESCE</c>, <c>CASE</c>, a reference and a cast of
    /// anything else have none, and neither has a text that merely starts with a form.  A literal's type, and a
    /// function's, is known only when the session finds <c>pg_catalog</c> first, since the plan prints a type's name
    /// and a function's bare whenever the path finds them.  The <c>bigint</c> of <c>count</c> with an argument is an
    /// assumption about which <c>count</c> the server chose, and never by itself a proof that the entry is not
    /// <c>NULL</c>: that is <see cref="NullableOf(string, ExpressionContext)" />'s to say.
    /// </summary>
    /// <param name="entry">The entry as the plan prints it.</param>
    /// <param name="catalogFunctions">Whether an unqualified function or type name is <c>pg_catalog</c>'s.</param>
    /// <returns>The OID of the type, or null when the entry as a whole is no form that has one.</returns>
    public static long? TypeOf(string entry, bool catalogFunctions) =>
        Tokens.Read(entry) is { Count: > 0 } tokens
            ? new Reader(entry, tokens, NoContext with { CatalogFunctions = catalogFunctions }).TypeOid(0, tokens.Count)
            : null;

    /// <summary>
    /// The text inside the parentheses of an entry that is, as a whole, a group with its cast, <c>(E)::T</c>, or null:
    /// how a node prints its cast of a value, and how a node above a set operation prints the cast of a branch's value
    /// to the result's type, <c>(("*SELECT* 1".id)::bigint)</c>, once the parentheses that wrap it whole are taken off.
    /// </summary>
    public static string? CastOperand(string entry)
    {
        if (Tokens.Read(entry) is not [{ Kind: Kind.Open, Start: 0 }, ..] tokens)
        {
            return null;
        }

        var reader = new Reader(entry, tokens, NoContext);
        return reader.CastOf() is { } close ? entry[tokens[0].End..tokens[close].Start] : null;
    }

    /// <summary>
    /// The text inside the one pair of parentheses that wraps the whole entry, with its quotes and its parentheses
    /// balanced, or null when none does: how a node prints a value a node under it computed.
    /// </summary>
    public static string? Unwrapped(string entry) =>
        Tokens.Read(entry) is [{ Kind: Kind.Open, Start: 0 }, .., { Kind: Kind.Close } last] tokens
        && last.End == entry.Length
        && new Reader(entry, tokens, NoContext).WrapsWhole(tokens.Count)
            ? entry[1..^1]
            : null;

    /// <summary>
    /// Whether an entry that is no plain reference may still print a column of that name: it names it, bare or quoted,
    /// or holds a parameter, <c>$1</c>, or its quotes or its parentheses do not balance.  A value a node takes from the
    /// query above, a <c>LATERAL</c> reference, prints as <c>(u.id)</c>, and so does a placeholder over a column.
    /// </summary>
    public static bool MayNameColumn(string entry, string column)
    {
        if (Tokens.Read(entry) is not { } tokens)
        {
            return true;
        }

        foreach (var token in tokens)
        {
            var text = entry[token.Start..token.End];
            string? named = null;
            if (token.Kind == Kind.Word)
            {
                named = text;
            }
            else if (token.Kind == Kind.QuotedName)
            {
                named = PostgresIdentifiers.Unquote(text);
            }

            if (
                string.Equals(named, column, StringComparison.Ordinal)
                || (token.Kind == Kind.Other && text.Contains('$', StringComparison.Ordinal))
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The text inside each pair of parentheses of the entry, outermost first, or none when its quotes or its
    /// parentheses do not balance: among them, each value a node takes from a node under it that computed it.
    /// </summary>
    public static IReadOnlyList<string> Groups(string entry)
    {
        if (Tokens.Read(entry) is not { } tokens)
        {
            return [];
        }

        var groups = new List<string>();
        var open = new Stack<Token>();
        foreach (var token in tokens)
        {
            if (token.Kind == Kind.Open)
            {
                open.Push(token);
            }
            else if (token.Kind == Kind.Close && open.TryPop(out var opening) && opening.Kind == Kind.Open)
            {
                groups.Add(entry[opening.End..token.Start]);
            }
        }

        groups.Reverse();
        return groups;
    }

    internal enum Kind
    {
        Word,
        QuotedName,
        String,
        Number,
        Open,
        Close,
        OpenBracket,
        CloseBracket,
        Comma,
        Dot,
        Cast,
        Other,
    }

    internal readonly record struct Token(Kind Kind, int Start, int End);

    // The entry as tokens, or null when a quote does not close or a parenthesis or a bracket does not balance.  A
    // string is '...' with '' for a quote, and a quoted name "..." with "" for a quote; a space only separates.
    internal static class Tokens
    {
        public static List<Token>? Read(string entry)
        {
            var tokens = new List<Token>();
            var depth = new Stack<Kind>();
            var at = 0;
            while (at < entry.Length)
            {
                var character = entry[at];
                var start = at;
                if (char.IsWhiteSpace(character))
                {
                    at++;
                    continue;
                }

                Kind kind;
                if (character is '\'' or '"')
                {
                    if (!PostgresIdentifiers.SkipQuoted(entry, at, out at))
                    {
                        return null;
                    }

                    kind = character == '\'' ? Kind.String : Kind.QuotedName;
                }
                else if (PostgresIdentifiers.IsWordStart(character))
                {
                    while (at < entry.Length && PostgresIdentifiers.IsWordPart(entry[at]))
                    {
                        at++;
                    }

                    kind = Kind.Word;
                }
                else if (char.IsAsciiDigit(character))
                {
                    kind = Number(entry, ref at);
                }
                else if (character == ':' && at + 1 < entry.Length && entry[at + 1] == ':')
                {
                    at += 2;
                    kind = Kind.Cast;
                }
                else if (Punctuation(character) is { } punctuation)
                {
                    at++;
                    kind = punctuation;
                    if (!Balances(depth, kind))
                    {
                        return null;
                    }
                }
                else
                {
                    while (at < entry.Length && IsSymbol(entry[at]))
                    {
                        at++;
                    }

                    at = Math.Max(at, start + 1);
                    kind = Kind.Other;
                }

                tokens.Add(new Token(kind, start, at));
            }

            return depth.Count == 0 ? tokens : null;
        }

        // Digits with an optional fraction; one that runs on into a letter is no number this class reads.
        private static Kind Number(string entry, ref int at)
        {
            while (at < entry.Length && char.IsAsciiDigit(entry[at]))
            {
                at++;
            }

            if (at + 1 < entry.Length && entry[at] == '.' && char.IsAsciiDigit(entry[at + 1]))
            {
                at++;
                while (at < entry.Length && char.IsAsciiDigit(entry[at]))
                {
                    at++;
                }
            }

            if (at < entry.Length && (PostgresIdentifiers.IsWordPart(entry[at]) || entry[at] == '.'))
            {
                while (at < entry.Length && (PostgresIdentifiers.IsWordPart(entry[at]) || entry[at] == '.'))
                {
                    at++;
                }

                return Kind.Other;
            }

            return Kind.Number;
        }

        private static bool Balances(Stack<Kind> depth, Kind kind)
        {
            if (kind is Kind.Open or Kind.OpenBracket)
            {
                depth.Push(kind);
                return true;
            }

            if (kind is not (Kind.Close or Kind.CloseBracket))
            {
                return true;
            }

            var opens = kind == Kind.Close ? Kind.Open : Kind.OpenBracket;
            return depth.TryPop(out var open) && open == opens;
        }

        private static Kind? Punctuation(char character) =>
            character switch
            {
                '(' => Kind.Open,
                ')' => Kind.Close,
                '[' => Kind.OpenBracket,
                ']' => Kind.CloseBracket,
                ',' => Kind.Comma,
                '.' => Kind.Dot,
                _ => null,
            };

        // A "$" is one, as in "+$1"; a character that starts a word is not.
        private static bool IsSymbol(char character) =>
            !char.IsWhiteSpace(character)
            && !PostgresIdentifiers.IsWordStart(character)
            && !char.IsAsciiDigit(character)
            && character is not ('\'' or '"' or ':')
            && Punctuation(character) is null;
    }

    // Reads spans of the tokens, each [from, to), as one form or none.
    private sealed class Reader
    {
        private readonly string _entry;
        private readonly List<Token> _tokens;
        private readonly ExpressionContext _context;

        // The index of the token that closes each opening parenthesis or bracket, and -1 for any other.
        private readonly int[] _closes;

        // Whether an aggregate of the span being read is one of the node's own, over a group that has a row: false
        // inside a group a child printed.
        private bool _groupsHaveRows;

        public Reader(string entry, List<Token> tokens, ExpressionContext context)
        {
            _entry = entry;
            _tokens = tokens;
            _context = context;
            _groupsHaveRows = context.GroupsHaveRows;
            _closes = new int[tokens.Count];
            var open = new Stack<int>();
            for (var index = 0; index < tokens.Count; index++)
            {
                _closes[index] = -1;
                if (tokens[index].Kind is Kind.Open or Kind.OpenBracket)
                {
                    open.Push(index);
                }
                else if (tokens[index].Kind is Kind.Close or Kind.CloseBracket)
                {
                    _closes[open.Pop()] = index;
                }
            }
        }

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

            if (IsVariadicCall(from, to))
            {
                return AnyArgumentIsNotNull(from + 2, to - 1) ? false : null;
            }

            if (IsCase(from, to) && CaseResultsAreNotNull(from, to))
            {
                return false;
            }

            // A reference is no form: it is proved only as part of one.
            return !IsReference(from, to) && Proved(from, to) is not null ? false : null;
        }

        /// <summary>
        /// The type of the span as a whole when it is a value that is never <c>NULL</c> of a known type, or null.  A
        /// constant, a test, <c>count(*)</c> or a ranking is of the type <see cref="TypeOid" /> gives; a reference
        /// that is part of the entry is of the type the walk proves; an operator, a function or a cast of
        /// <c>pg_catalog</c> over such values is of the type <see cref="TotalFunctions" /> or the cast gives.  A
        /// value of no known type, <see cref="TotalFunctions.UnknownType" />, matches no operator, function or cast.
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

            if (Constant(from, to) == false || IsCountOrRanking(from, to))
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

        // Whether the walk says the group may not be read.  The entry as a whole is never asked.
        private bool Vetoed(int from, int to) =>
            (from > 0 || to < _tokens.Count) && _context.GroupPreserved?.Invoke(Inside(from, to)) == false;

        // Marks the aggregates inside the group unread when a child of the node printed it, and returns what to
        // restore once the group is read.
        private bool EnterGroup(int from, int to)
        {
            var outside = _groupsHaveRows;
            if (outside && _context.GroupFromChild?.Invoke(Inside(from, to)) == true)
            {
                _groupsHaveRows = false;
            }

            return outside;
        }

        // The text inside the group's parentheses.
        private string Inside(int from, int to) => _entry[_tokens[from].End.._tokens[to - 1].Start];

        private bool IsReference(int from, int to) => to > from && OutputReference.Parse(Text(from, to)) is not null;

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

        // Whether the first token is a parenthesis that the last closes.
        public bool WrapsWhole(int count) => IsGroup(0, count);

        // The index of the parenthesis that closes the first token, when what follows it to the end is a cast to a
        // type name; or null.
        public int? CastOf()
        {
            var close = _closes[0];
            return
                close >= 0
                && close + 2 < _tokens.Count
                && Is(close + 1, Kind.Cast)
                && IsTypeName(close + 2, _tokens.Count)
                ? close
                : null;
        }

        // Whether the span is one parenthesis and the one that closes it.
        private bool IsGroup(int from, int to) => Is(from, Kind.Open) && _closes[from] == to - 1;

        private bool Is(int index, Kind kind) => _tokens[index].Kind == kind;

        private bool IsWord(int index, string word) =>
            index >= 0
            && index < _tokens.Count
            && Is(index, Kind.Word)
            && _entry.AsSpan(_tokens[index].Start, _tokens[index].End - _tokens[index].Start).SequenceEqual(word);

        private string Text(int index) => _entry[_tokens[index].Start.._tokens[index].End];

        private string Text(int from, int to) => _entry[_tokens[from].Start.._tokens[to - 1].End];

        // A literal with its cast, a number, true or false: never NULL.  NULL with its cast: always.
        private bool? Constant(int from, int to)
        {
            if (to - from == 1)
            {
                return Is(from, Kind.Number) || IsWord(from, "true") || IsWord(from, "false") ? false : null;
            }

            if (to - from >= 3 && Is(from + 1, Kind.Cast) && IsTypeName(from + 2, to))
            {
                if (Is(from, Kind.String))
                {
                    return false;
                }

                if (IsWord(from, "NULL"))
                {
                    return true;
                }
            }

            return null;
        }

        // A type as the plan prints it: lower-case words and quoted names, with dots, a modifier of numbers in
        // parentheses, and brackets.  An upper-case word, AND, IS, COLLATE, is no part of a type's name.
        private bool IsTypeName(int from, int to)
        {
            if (!IsLowerWord(from) && !Is(from, Kind.QuotedName))
            {
                return false;
            }

            var at = from + 1;
            while (at < to)
            {
                if (IsLowerWord(at) || Is(at, Kind.QuotedName) || Is(at, Kind.Dot))
                {
                    at++;
                }
                else if (Is(at, Kind.OpenBracket) && _closes[at] == at + 1)
                {
                    at += 2;
                }
                else if (Is(at, Kind.Open) && IsModifier(at + 1, _closes[at]))
                {
                    at = _closes[at] + 1;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsModifier(int from, int to)
        {
            for (var at = from; at < to; at++)
            {
                if (!Is(at, (at - from) % 2 == 0 ? Kind.Number : Kind.Comma))
                {
                    return false;
                }
            }

            return to > from && (to - from) % 2 == 1;
        }

        private bool IsLowerWord(int index)
        {
            if (!Is(index, Kind.Word))
            {
                return false;
            }

            return !Text(index).Any(char.IsAsciiLetterUpper);
        }

        // count(*), with an optional FILTER (...) and an optional OVER, or a ranking with its OVER, to the end.  The
        // parentheses hold nothing else: a function of another schema whose argument's type is exact beats the
        // catalog's count("any") whatever the order of the path, and prints bare, while one of the catalog's own
        // signature, count(*) or rank(), is found in pg_catalog first.  With anyArgument count may hold an
        // argument too, which TypeOf takes: the type is the catalog's bigint, and a form that NullableOf does not
        // answer false never makes its column not null.
        private bool IsCountOrRanking(int from, int to, bool anyArgument = false)
        {
            var isCount = IsWord(from, "count");
            if (!isCount && !(Is(from, Kind.Word) && Rankings.Contains(Text(from))))
            {
                return false;
            }

            if (from + 1 >= to || !Is(from + 1, Kind.Open))
            {
                return false;
            }

            var holds = _closes[from + 1] - (from + 2);
            if (!isCount && holds != 0)
            {
                return false;
            }

            if (isCount && (anyArgument ? holds < 1 : holds != 1 || Text(from + 2) != "*"))
            {
                return false;
            }

            var at = _closes[from + 1] + 1;
            if (isCount && IsWord(at, "FILTER"))
            {
                if (at + 1 >= to || !Is(at + 1, Kind.Open))
                {
                    return false;
                }

                at = _closes[at + 1] + 1;
            }

            if (at == to)
            {
                return isCount;
            }

            if (!IsWord(at, "OVER") || at + 1 >= to)
            {
                return false;
            }

            var window = at + 1;
            return Is(window, Kind.Open)
                ? _closes[window] == to - 1
                : (Is(window, Kind.Word) || Is(window, Kind.QuotedName)) && window == to - 1;
        }

        // COALESCE(...), GREATEST(...) or LEAST(...), to the end.
        private bool IsVariadicCall(int from, int to) =>
            to - from >= 3
            && Is(from, Kind.Word)
            && Variadic.Contains(Text(from))
            && Is(from + 1, Kind.Open)
            && _closes[from + 1] == to - 1;

        // Whether an argument, between the commas of depth zero of [from, to), cannot be NULL.
        private bool AnyArgumentIsNotNull(int from, int to)
        {
            var start = from;
            var at = from;
            while (at < to)
            {
                if (Is(at, Kind.Comma))
                {
                    if (IsNotNull(start, at))
                    {
                        return true;
                    }

                    start = at + 1;
                }

                at = Next(at);
            }

            return IsNotNull(start, to);
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

        // The results of the outermost CASE are after each THEN and after its ELSE, each up to the next WHEN, ELSE or
        // its END; a CASE nested without parentheses is counted, and one in parentheses is skipped whole.  A CASE whose
        // END comes before the last token is no CASE of the whole span.
        private bool CaseResultsAreNotNull(int from, int to)
        {
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
                        return false;
                    }

                    closed = true;
                }
                else if (nested > 0 || !(IsWord(at, "WHEN") || IsWord(at, "THEN") || IsWord(at, "ELSE")))
                {
                    continue;
                }

                if (resultStart >= 0 && !IsNotNull(resultStart, at))
                {
                    return false;
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

            return closed && sawThen && sawElse;
        }

        // Inside one pair of parentheses: x IS NULL, x IS NOT NULL, x IS DISTINCT FROM y, or NOT of the last in a
        // pair of its own.  Each operand is one primary with an optional cast.
        private bool IsTest(int from, int to)
        {
            if (to - from >= 2 && IsWord(from, "NOT") && IsGroup(from + 1, to))
            {
                return IsDistinct(from + 2, to - 1);
            }

            if (to - from >= 3 && IsWord(to - 2, "IS") && IsWord(to - 1, "NULL") && IsOperand(from, to - 2))
            {
                return true;
            }

            if (
                to - from >= 4
                && IsWord(to - 3, "IS")
                && IsWord(to - 2, "NOT")
                && IsWord(to - 1, "NULL")
                && IsOperand(from, to - 3)
            )
            {
                return true;
            }

            return IsDistinct(from, to);
        }

        private bool IsDistinct(int from, int to)
        {
            for (var at = from; at + 2 < to; at = Next(at))
            {
                if (IsWord(at, "IS") && IsWord(at + 1, "DISTINCT") && IsWord(at + 2, "FROM"))
                {
                    return IsOperand(from, at) && IsOperand(at + 3, to);
                }
            }

            return false;
        }

        private bool IsName(int index) => Is(index, Kind.Word) || Is(index, Kind.QuotedName);

        // The index after the token, or after the group a parenthesis or a bracket opens.
        private int Next(int at) => _closes[at] >= 0 ? _closes[at] + 1 : at + 1;

        private bool IsOperand(int from, int to)
        {
            if (Primary(from, to) is not { } end)
            {
                return false;
            }

            return end == to || (Is(end, Kind.Cast) && IsTypeName(end + 1, to));
        }

        // The index after one primary that starts the span, or null: a group, a literal, a number, a name with its
        // dots, or such a name called with a group.
        private int? Primary(int from, int to)
        {
            if (from >= to)
            {
                return null;
            }

            if (Is(from, Kind.Open))
            {
                return _closes[from] + 1;
            }

            if (Is(from, Kind.String) || Is(from, Kind.Number))
            {
                return from + 1;
            }

            if (!IsName(from))
            {
                return null;
            }

            var at = from + 1;
            while (at + 1 < to && Is(at, Kind.Dot) && IsName(at + 1))
            {
                at += 2;
            }

            return at < to && Is(at, Kind.Open) ? _closes[at] + 1 : at;
        }
    }
}
