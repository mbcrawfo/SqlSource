using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// An entry of a plan's <c>Output</c> read as a whole: whether the server can ever return <c>NULL</c> for it, for the
/// few forms whose answer the text alone settles.
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
/// The forms: <c>count(*)</c>, with a <c>FILTER</c> or an <c>OVER</c> or neither; <c>row_number()</c>,
/// <c>rank()</c>, <c>dense_rank()</c>, <c>percent_rank()</c> and <c>cume_dist()</c> with an <c>OVER</c>; a constant,
/// a quoted literal with its cast, a number, <c>true</c> or <c>false</c>, and <c>NULL::type</c>, which is the one
/// that can be <c>NULL</c>; the tests <c>IS NULL</c>, <c>IS NOT NULL</c> and <c>IS DISTINCT FROM</c>, and the
/// <c>NOT</c> of the last, which is how <c>IS NOT DISTINCT FROM</c> prints; <c>COALESCE</c>, <c>GREATEST</c> and
/// <c>LEAST</c> with an argument that cannot be <c>NULL</c>; and a <c>CASE</c> with an <c>ELSE</c> whose every result
/// cannot be.  A <c>CASE</c> written without an <c>ELSE</c> prints <c>ELSE NULL::type</c>.  A cast of anything but a
/// literal is none: the text does not say which function casts.
/// </para>
/// <para>
/// A function name is the catalog's only when the plan prints it unqualified and the session finds
/// <c>pg_catalog</c>'s functions first (<see cref="PostgresSearchPath" />): the plan prints a function bare whenever
/// the search path finds it, so <c>count(*)</c> may be another schema's aggregate.  <c>COALESCE</c>,
/// <c>GREATEST</c>, <c>LEAST</c>, <c>CASE</c> and the tests are syntax, and no search path changes them.
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

    private static readonly HashSet<string> Variadic = [with(StringComparer.Ordinal), "COALESCE", "GREATEST", "LEAST"];

    /// <summary>What the entry says of its column.</summary>
    /// <param name="entry">The entry as the plan prints it.</param>
    /// <param name="catalogFunctions">Whether an unqualified function name is <c>pg_catalog</c>'s.</param>
    /// <param name="provedNotNull">
    /// Whether the walk proves a reference that is an argument not <c>NULL</c>.  A reference that is the whole entry
    /// is never asked.
    /// </param>
    /// <param name="groupPreserved">
    /// Whether a span in parentheses inside the entry, given as the text inside them, may be read as what it says, or
    /// null when every one may.  The walk says no for a value a node under a join that null-extends computed, which
    /// the node that computes the entry takes from it and prints in parentheses: such a span is unknown, whatever its
    /// form.  The entry as a whole is never asked.
    /// </param>
    /// <returns>
    /// False when the server can never return <c>NULL</c> for the entry, true for a <c>NULL</c> constant, and null
    /// when the entry as a whole is no form this class knows.
    /// </returns>
    public static bool? NullableOf(
        string entry,
        bool catalogFunctions,
        Func<OutputReference, bool> provedNotNull,
        Func<string, bool>? groupPreserved = null
    ) =>
        Tokens.Read(entry) is { Count: > 0 } tokens
            ? new Reader(entry, tokens, catalogFunctions, provedNotNull, groupPreserved).Form(0, tokens.Count)
            : null;

    /// <summary>
    /// The text inside the one pair of parentheses that wraps the whole entry, with its quotes and its parentheses
    /// balanced, or null when none does: how a node prints a value a node under it computed.
    /// </summary>
    public static string? Unwrapped(string entry) =>
        Tokens.Read(entry) is [{ Kind: Kind.Open, Start: 0 }, .., { Kind: Kind.Close } last] tokens
        && last.End == entry.Length
        && new Reader(entry, tokens, false, static _ => false, null).WrapsWhole(tokens.Count)
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
                named = text[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
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

    private enum Kind
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

    private readonly record struct Token(Kind Kind, int Start, int End);

    // The entry as tokens, or null when a quote does not close or a parenthesis or a bracket does not balance.  A
    // string is '...' with '' for a quote, and a quoted name "..." with "" for a quote; a space only separates.
    private static class Tokens
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
                    if (!SkipQuoted(entry, ref at))
                    {
                        return null;
                    }

                    kind = character == '\'' ? Kind.String : Kind.QuotedName;
                }
                else if (IsNameStart(character))
                {
                    while (at < entry.Length && IsNamePart(entry[at]))
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

        private static bool SkipQuoted(string entry, ref int at)
        {
            var quote = entry[at];
            at++;
            while (at < entry.Length)
            {
                if (entry[at] != quote)
                {
                    at++;
                }
                else if (at + 1 < entry.Length && entry[at + 1] == quote)
                {
                    at += 2;
                }
                else
                {
                    at++;
                    return true;
                }
            }

            return false;
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

            if (at < entry.Length && (IsNamePart(entry[at]) || entry[at] == '.'))
            {
                while (at < entry.Length && (IsNamePart(entry[at]) || entry[at] == '.'))
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

        private static bool IsNameStart(char character) => char.IsAsciiLetter(character) || character == '_';

        private static bool IsNamePart(char character) =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '$';

        private static bool IsSymbol(char character) =>
            !char.IsWhiteSpace(character)
            && !char.IsAsciiLetterOrDigit(character)
            && character is not ('_' or '\'' or '"' or ':')
            && Punctuation(character) is null;
    }

    // Reads spans of the tokens, each [from, to), as one form or none.
    private sealed class Reader
    {
        private readonly string _entry;
        private readonly List<Token> _tokens;
        private readonly bool _catalogFunctions;
        private readonly Func<OutputReference, bool> _provedNotNull;
        private readonly Func<string, bool>? _groupPreserved;

        // The index of the token that closes each opening parenthesis or bracket, and -1 for any other.
        private readonly int[] _closes;

        public Reader(
            string entry,
            List<Token> tokens,
            bool catalogFunctions,
            Func<OutputReference, bool> provedNotNull,
            Func<string, bool>? groupPreserved
        )
        {
            _entry = entry;
            _tokens = tokens;
            _catalogFunctions = catalogFunctions;
            _provedNotNull = provedNotNull;
            _groupPreserved = groupPreserved;
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

        /// <summary>False, true or null, as <see cref="NullableOf" /> answers, for the span as a whole.</summary>
        public bool? Form(int from, int to)
        {
            if (to <= from)
            {
                return null;
            }

            if (IsGroup(from, to))
            {
                if (
                    (from > 0 || to < _tokens.Count)
                    && _groupPreserved?.Invoke(_entry[_tokens[from].End.._tokens[to - 1].Start]) == false
                )
                {
                    return null;
                }

                return IsTest(from + 1, to - 1) ? false : Form(from + 1, to - 1);
            }

            if (Constant(from, to) is { } constant)
            {
                return constant;
            }

            if (IsCountOrRanking(from, to))
            {
                return _catalogFunctions ? false : null;
            }

            if (IsVariadicCall(from, to))
            {
                return AnyArgumentIsNotNull(from + 2, to - 1) ? false : null;
            }

            return IsCase(from, to) && CaseResultsAreNotNull(from, to) ? false : null;
        }

        // Whether the first token is a parenthesis that the last closes.
        public bool WrapsWhole(int count) => IsGroup(0, count);

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
        // signature, count(*) or rank(), is found in pg_catalog first.
        private bool IsCountOrRanking(int from, int to)
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
            if (isCount ? holds != 1 || Text(from + 2) != "*" : holds != 0)
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
                || (OutputReference.Parse(Text(from, to)) is { } reference && _provedNotNull(reference))
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
