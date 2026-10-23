using System;
using System.Collections.Generic;
using System.Linq;
using Kind = SqlSource.Tool.Describing.Postgres.Walk.PlanTokens.Kind;
using Token = SqlSource.Tool.Describing.Postgres.Walk.PlanTokens.Token;

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
/// A <c>COALESCE</c>, a <c>GREATEST</c> or a <c>LEAST</c> that is not <c>NULL</c> is of the one type that every
/// argument proved of a known type has, and a <c>CASE</c> whose every result is not <c>NULL</c> of the one its results
/// proved of a known type have, when the session finds <c>pg_catalog</c> first: the parser casts each argument and
/// each result to their common type, and the plan prints the casts.  With no such type, or two, the form is of no
/// known type and feeds no operator.  A reference in parentheses of its own, <c>(u.id)</c>, is unknown.
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
/// A name of <see cref="CatalogOverloads" /> that only <c>pg_catalog</c> holds
/// (<see cref="ExpressionContext.CatalogAlone" />) is read by its name, since the call is then one of
/// <c>pg_catalog</c>'s overloads of it, whichever the server chose: <c>count(...)</c> with any argument, a
/// <c>bigint</c>; <c>array_agg(...)</c> of one argument where an aggregate of the table would be read, of no known
/// type; <c>jsonb_build_object(...)</c> and <c>json_build_object(...)</c> with no argument written <c>VARIADIC</c>, a
/// <c>jsonb</c> and a <c>json</c>; the constructor of a built-in range type with two or three arguments, of that type;
/// and <c>(A * B)</c> over two values proved of one of those range types, of that type.  The arguments of all but the
/// intersection are read for their balance only.  <see cref="CatalogNamesIn" /> gives the names of the table an entry
/// calls.
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
internal static partial class PlanExpression
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
        PlanTokens.Read(entry) is { Count: > 0 } tokens
            ? new Reader(entry, tokens, context).Form(0, tokens.Count)
            : null;

    /// <summary>
    /// The type of an entry that is, as a whole, a value proved not <c>NULL</c> of a known type, or null: a constant,
    /// a test, <c>count(*)</c> or a ranking as <see cref="TypeOf" /> types them, or an operator, a function or a cast
    /// of the forms the remarks give, a <c>COALESCE</c>, a <c>GREATEST</c>, a <c>LEAST</c> and a <c>CASE</c> among
    /// them, of the type their proved arguments or results share.  A reference that is the whole entry is never asked.
    /// </summary>
    /// <param name="entry">The entry as the plan prints it.</param>
    /// <param name="context">What the walk says of the plan the entry is read in.</param>
    /// <returns>The OID of the type, or null.</returns>
    public static long? ProvedTypeOf(string entry, ExpressionContext context) =>
        PlanTokens.Read(entry) is { Count: > 0 } tokens
            ? new Reader(entry, tokens, context).Proved(0, tokens.Count)
            : null;

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
        PlanTokens.Read(entry) is { Count: > 0 } tokens
            ? new Reader(entry, tokens, NoContext with { CatalogFunctions = catalogFunctions }).TypeOid(0, tokens.Count)
            : null;

    /// <summary>
    /// The text inside the parentheses of an entry that is, as a whole, a group with its cast, <c>(E)::T</c>, or null:
    /// how a node prints its cast of a value, and how a node above a set operation prints the cast of a branch's value
    /// to the result's type, <c>(("*SELECT* 1".id)::bigint)</c>, once the parentheses that wrap it whole are taken off.
    /// </summary>
    public static string? CastOperand(string entry)
    {
        if (PlanTokens.Read(entry) is not [{ Kind: Kind.Open, Start: 0 }, ..] tokens)
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
        PlanTokens.Read(entry) is [{ Kind: Kind.Open, Start: 0 }, .., { Kind: Kind.Close } last] tokens
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
        if (PlanTokens.Read(entry) is not { } tokens)
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
        if (PlanTokens.Read(entry) is not { } tokens)
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

    /// <summary>
    /// The names of <see cref="CatalogOverloads" /> the entry calls, each once in the order first found: a lower-case
    /// bare name followed by its parenthesis, but <c>count</c> whose parenthesis holds only <c>*</c>, and an operator
    /// between two operands.  None when its quotes or its parentheses do not balance.  A name found that the reader
    /// does not read costs a name to ask about, and is never wrong.
    /// </summary>
    /// <param name="entry">The entry as the plan prints it.</param>
    /// <returns>The names of functions and the names of operators.</returns>
    public static (IReadOnlyList<string> Functions, IReadOnlyList<string> Operators) CatalogNamesIn(string entry) =>
        PlanTokens.Read(entry) is { } tokens ? new Reader(entry, tokens, NoContext).CatalogNames() : ([], []);

    // Reads spans of the tokens, each [from, to), as one form or none.  This part holds the reader's state, its span
    // helpers and the grammar that both its jobs read, of a test, a constant, count, a ranking and a type's name; the
    // forms of nullability are in PlanExpression.Nullability.cs, the proofs of types in PlanExpression.Proofs.cs, and
    // the catalog's forms that the proofs read in PlanExpression.Forms.cs.
    private sealed partial class Reader
    {
        private readonly string _entry;
        private readonly List<Token> _tokens;
        private readonly ExpressionContext _context;

        // The index of the token that closes each opening parenthesis or bracket, and -1 for any other.
        private readonly int[] _closes;

        // The type Proved gave each span read so far, by the span and the state of _groupsHaveRows it was read in.
        private readonly Dictionary<(int From, int To, bool GroupsHaveRows), long?> _proved = [];

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
        // answer false never makes its column not null.  Such a count is read only where no other schema holds a
        // count (IsCatalogsCount).
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

        // count with any argument, an optional FILTER and an optional OVER, to the end, where only pg_catalog holds a
        // function of the name: then it is count("any"), which is never NULL.
        private bool IsCatalogsCount(int from, int to) =>
            IsWord(from, "count") && HeldAlone("count") && IsCountOrRanking(from, to, anyArgument: true);

        // The names of CatalogOverloads the tokens call, as CatalogNamesIn gives them.
        public (IReadOnlyList<string> Functions, IReadOnlyList<string> Operators) CatalogNames()
        {
            var functions = new List<string>();
            var operators = new List<string>();
            for (var at = 0; at < _tokens.Count; at++)
            {
                var text = Text(at);
                if (IsBareCall(at) && CatalogOverloads.FunctionNames.Contains(text, StringComparer.Ordinal))
                {
                    if (
                        !(text == "count" && _closes[at + 1] == at + 3 && Text(at + 2) == "*")
                        && !functions.Contains(text)
                    )
                    {
                        functions.Add(text);
                    }
                }
                else if (
                    Is(at, Kind.Other)
                    && CatalogOverloads.OperatorNames.Contains(text, StringComparer.Ordinal)
                    && at > 0
                    && EndsOperand(at - 1)
                    && at + 1 < _tokens.Count
                    && StartsOperand(at + 1)
                    && !operators.Contains(text)
                )
                {
                    operators.Add(text);
                }
            }

            return (functions, operators);
        }

        // A lower-case name with no schema before it, followed by its parenthesis.
        private bool IsBareCall(int at) =>
            IsLowerWord(at) && (at == 0 || !Is(at - 1, Kind.Dot)) && at + 1 < _tokens.Count && Is(at + 1, Kind.Open);

        private bool EndsOperand(int at) =>
            _tokens[at].Kind
                is Kind.Close
                    or Kind.Word
                    or Kind.Number
                    or Kind.String
                    or Kind.QuotedName
                    or Kind.CloseBracket;

        // A parameter, $1, is a symbol and its number.
        private bool StartsOperand(int at) =>
            _tokens[at].Kind is Kind.Open or Kind.Word or Kind.Number or Kind.String or Kind.QuotedName
            || (Is(at, Kind.Other) && Text(at).StartsWith('$'));

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
