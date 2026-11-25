using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>The statement an <see cref="OutputStatement" /> is.</summary>
internal enum OutputKind
{
    /// <summary>An <c>INSERT</c>.</summary>
    Insert,

    /// <summary>An <c>UPDATE</c>.</summary>
    Update,

    /// <summary>A <c>DELETE</c>.</summary>
    Delete,
}

/// <summary>An item of an <c>UPDATE</c>'s <c>SET</c> that writes a column of its target.</summary>
/// <param name="Column">The column's name, without quotes.</param>
/// <param name="Value">The value it writes: the tokens after its <c>=</c>.</param>
internal sealed record Assignment(string Column, TokenRange Value);

/// <summary>
/// An <c>INSERT</c>, an <c>UPDATE</c> or a <c>DELETE</c> with <c>OUTPUT</c>, with the <c>WITH</c> clause before it.
/// </summary>
/// <param name="Kind">Whether it is an <c>INSERT</c>, an <c>UPDATE</c> or a <c>DELETE</c>.</param>
/// <param name="With">
/// The <c>WITH</c> clause, from the word <c>WITH</c> to the <c>)</c> of its last CTE, or an empty range before the
/// statement when there is none.
/// </param>
/// <param name="Ctes">The CTEs of the <c>WITH</c> clause, in order.</param>
/// <param name="Rows">
/// The rows it outputs, read as a <c>SELECT</c>'s: its items are the <c>OUTPUT</c> items, and its <c>FROM</c> and
/// <c>WHERE</c> the <c>UPDATE</c>'s or the <c>DELETE</c>'s, the <c>FROM</c> after the <c>OUTPUT</c>.  An
/// <c>INSERT</c>'s has neither, and none has a <c>GROUP BY</c>, a <c>HAVING</c>, a <c>TOP</c> or an <c>OFFSET</c>.
/// </param>
/// <param name="Target">
/// The last part of the name the statement writes: the table's, or, for an <c>UPDATE</c> or a <c>DELETE</c> with a
/// <c>FROM</c>, the name of the source of the <c>FROM</c> that is its target.
/// </param>
/// <param name="TargetIsAlias">
/// True when the <c>UPDATE</c> or the <c>DELETE</c> has a <c>FROM</c>, so that <see cref="Target" /> names one of its
/// sources.
/// </param>
/// <param name="Assignments">
/// The items of the <c>SET</c> that write a column, in order; empty for a <c>DELETE</c>, which writes none, and null
/// for an <c>INSERT</c> and for a <c>SET</c> one of whose items is not <c>c = v</c>, <c>q.c = v</c> with <c>q</c> the
/// target, or <c>@v = e</c>.
/// </param>
internal sealed record OutputStatement(
    OutputKind Kind,
    TokenRange With,
    IReadOnlyList<CommonTableExpression> Ctes,
    SelectBody Rows,
    string Target,
    bool TargetIsAlias,
    IReadOnlyList<Assignment>? Assignments
)
{
    // How a column the SET writes is matched: a collation may make Shipped_At, or a name written in other widths or
    // accents, the column shipped_at.
    private const CompareOptions Loosely =
        CompareOptions.IgnoreCase
        | CompareOptions.IgnoreNonSpace
        | CompareOptions.IgnoreKanaType
        | CompareOptions.IgnoreWidth;

    // The clauses of an UPDATE after its SET, or of a DELETE after its target, in the order T-SQL takes them.
    private enum Clause
    {
        Output,
        From,
        Where,
        Option,
    }

    /// <summary>
    /// The statement, or null when its text is not an optional <c>;</c>, an optional <c>WITH</c> clause and one
    /// <c>INSERT</c>, <c>UPDATE</c> or <c>DELETE</c> with <c>OUTPUT</c> that the reader follows, ending the text or
    /// followed by <c>;</c> alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A misread must prove nothing, so whatever the reader cannot follow gives null.  <c>OUTPUT</c> is no reserved
    /// word, so the statement holds exactly one at its level; and it holds no <c>INTO</c> in its items, no <c>;</c>,
    /// and no word that starts a statement at its level but its first, an <c>UPDATE</c>'s <c>SET</c> and the word that
    /// starts an <c>INSERT</c>'s source.
    /// </para>
    /// <para>
    /// An <c>UPDATE</c> is <c>UPDATE [TOP ( ... ) [PERCENT]] name [WITH ( ... )] SET ... OUTPUT ... [FROM ...]
    /// [WHERE ...] [OPTION ( ... )]</c>, its name of one to four parts.  With a <c>FROM</c>, the name is of one part
    /// and exactly one source of the <c>FROM</c> is so named, a table with an alias or a name of one part: the server
    /// may join a target that no source names as another instance.  A <c>WHERE CURRENT OF</c> gives null.
    /// </para>
    /// <para>
    /// A <c>DELETE</c> is <c>DELETE [TOP ( ... ) [PERCENT]] [FROM] name [WITH ( ... )] OUTPUT ... [FROM ...]
    /// [WHERE ...] [OPTION ( ... )]</c>, read by the rules of an <c>UPDATE</c>: its first <c>FROM</c> comes before the
    /// name, and the <c>FROM</c> of its sources after the <c>OUTPUT</c>.
    /// </para>
    /// <para>
    /// An <c>INSERT</c> is <c>INSERT [TOP ( ... ) [PERCENT]] [INTO] name [WITH ( ... )] [( columns )] OUTPUT ...</c>
    /// followed by <c>VALUES</c>, <c>SELECT</c>, <c>DEFAULT VALUES</c>, <c>EXEC</c> or <c>EXECUTE</c>.
    /// </para>
    /// <para>A target, or a source that is the target, named as a CTE is, ignoring case, gives null.</para>
    /// </remarks>
    public static OutputStatement? Read(IReadOnlyList<TsqlToken> tokens)
    {
        var at = tokens.Count > 0 && tokens[0].IsSymbol(";") ? 1 : 0;
        var end = tokens.Count;
        if (end > at && tokens[end - 1].IsSymbol(";"))
        {
            end--;
        }

        var withStart = at;
        var ctes = new List<CommonTableExpression>();
        if (at < end && tokens[at].Is("WITH"))
        {
            at = SelectStatement.ReadWith(tokens, at, end, ctes);
            if (at < 0)
            {
                return null;
            }
        }

        var statement = new TokenRange(at, end);
        if (statement.IsEmpty || !SelectBody.IsBalanced(tokens, statement))
        {
            return null;
        }

        var level = SelectBody.AtLevel(tokens, statement).ToList();
        var outputs = level.Where(index => tokens[index].Is("OUTPUT")).ToList();
        if (outputs is not [var output] || level.Exists(index => tokens[index].IsSymbol(";")))
        {
            return null;
        }

        var shape = new Shape(new TokenRange(withStart, at), ctes, statement, level, output);
        if (tokens[at].Is("UPDATE"))
        {
            return ReadChange(tokens, shape, OutputKind.Update);
        }

        if (tokens[at].Is("DELETE"))
        {
            return ReadChange(tokens, shape, OutputKind.Delete);
        }

        return tokens[at].Is("INSERT") ? ReadInsert(tokens, shape) : null;
    }

    /// <summary>
    /// True when <paramref name="parts" /> name <paramref name="column" /> of the target, ordinally: the column alone
    /// when the statement has no <c>FROM</c> or a <c>FROM</c> of one source, or <see cref="Target" /> and the column.
    /// </summary>
    /// <param name="tokens">The tokens it was read from.</param>
    /// <param name="parts">The parts of a name, as <see cref="RowFilter.NameOf" /> gives them.</param>
    /// <param name="column">The target's column.</param>
    public bool IsTargetColumn(IReadOnlyList<TsqlToken> tokens, string[] parts, string column)
    {
        if (parts.Length == 0 || !string.Equals(parts[^1], column, StringComparison.Ordinal))
        {
            return false;
        }

        return parts.Length switch
        {
            1 => Rows.From is not { } from || JoinChain.Read(tokens, from) is [_],
            2 => string.Equals(parts[0], Target, StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <summary>
    /// True when an assignment writes <paramref name="column" />, its name equal ignoring case, accents, kana and
    /// width, or when <see cref="Assignments" /> is null, since a <c>SET</c> the reader cannot follow may write any.
    /// </summary>
    /// <param name="column">The target's column.</param>
    public bool Writes(string column) =>
        Assignments is null || Assignments.Any(assignment => IsLooselyEqual(assignment.Column, column));

    /// <summary>
    /// The one assignment that writes <paramref name="column" />, by <see cref="Writes" />'s match, when its name is
    /// the column's ordinally; or null when none or several write it, or <see cref="Assignments" /> is null.
    /// </summary>
    /// <param name="column">The target's column.</param>
    public Assignment? AssignmentOf(string column) =>
        Assignments?.Where(assignment => IsLooselyEqual(assignment.Column, column)).ToList() is [var one]
        && string.Equals(one.Column, column, StringComparison.Ordinal)
            ? one
            : null;

    private static bool IsLooselyEqual(string left, string right) =>
        CultureInfo.InvariantCulture.CompareInfo.Compare(left, right, Loosely) == 0;

    // An UPDATE or a DELETE, read by the same rules but for what comes between the name and the OUTPUT.
    private static OutputStatement? ReadChange(IReadOnlyList<TsqlToken> tokens, Shape shape, OutputKind kind)
    {
        var end = shape.Statement.End;
        var at = TopEnd(tokens, shape.Statement.Start + 1, end);
        if (kind == OutputKind.Delete && at >= 0 && at < end && tokens[at].Is("FROM"))
        {
            at++;
        }

        // An UPDATE's name is followed by its SET, and a DELETE's by its OUTPUT.
        if (
            TargetOf(tokens, at, end, shape.Ctes) is not var (parts, nameEnd)
            || (kind == OutputKind.Update ? !tokens[nameEnd].Is("SET") : nameEnd != shape.Output)
        )
        {
            return null;
        }

        // A DELETE has no SET: its first word stands for it, as no other word may.
        var set = kind == OutputKind.Update ? nameEnd : shape.Statement.Start;
        var clauses = new List<(Clause Kind, int Start)>();
        foreach (var index in shape.Level.Where(index => index > set && index >= nameEnd))
        {
            if (ClauseAt(tokens[index]) is not { } clause)
            {
                continue;
            }

            if (clauses.Count > 0 && clauses[^1].Kind >= clause)
            {
                return null;
            }

            clauses.Add((clause, index));
        }

        if (clauses is not [(Clause.Output, _), ..] || HasOtherStatementWord(tokens, shape, set))
        {
            return null;
        }

        var items = ItemsOf(tokens, new TokenRange(shape.Output + 1, clauses.Count > 1 ? clauses[1].Start : end));
        var from = RangeOf(clauses, Clause.From, end);
        var where = RangeOf(clauses, Clause.Where, end);
        if (
            items is null
            || (where is { } filter && filter.Count > 1 && tokens[filter.Start + 1].Is("CURRENT"))
            || (from is { } sources && (parts.Length != 1 || !IsTheTarget(tokens, sources, parts[0], shape.Ctes)))
        )
        {
            return null;
        }

        var rows = new SelectBody(
            items,
            from is { } fromRange ? new TokenRange(fromRange.Start, (where ?? fromRange).End) : null,
            SelectGrouping.None,
            HasHaving: false,
            HasTop: false,
            HasOffset: false,
            HasFrom: from is not null,
            IsOneTable: false
        )
        {
            From = from,
            Where = where,
        };
        return new OutputStatement(
            kind,
            shape.With,
            shape.Ctes,
            rows,
            parts[^1],
            TargetIsAlias: from is not null,
            kind == OutputKind.Update ? AssignmentsOf(tokens, new TokenRange(set + 1, shape.Output), parts[^1]) : []
        );
    }

    private static OutputStatement? ReadInsert(IReadOnlyList<TsqlToken> tokens, Shape shape)
    {
        var end = shape.Statement.End;
        var at = TopEnd(tokens, shape.Statement.Start + 1, end);
        if (at >= 0 && at < end && tokens[at].Is("INTO"))
        {
            at++;
        }

        if (TargetOf(tokens, at, end, shape.Ctes) is not var (parts, nameEnd))
        {
            return null;
        }

        // A list of columns; a "(" that closes nowhere leaves at -1.
        at = tokens[nameEnd].IsSymbol("(") ? SelectBody.ClosingParenthesis(tokens, nameEnd, end) + 1 : nameEnd;
        var found = shape.Level.FindIndex(index => index > shape.Output && IsSourceWord(tokens[index]));
        if (at != shape.Output || found < 0)
        {
            return null;
        }

        var source = shape.Level[found];
        if (
            (tokens[source].Is("DEFAULT") && (source + 2 != end || !tokens[source + 1].Is("VALUES")))
            || HasOtherStatementWord(tokens, shape, source)
            || ItemsOf(tokens, new TokenRange(shape.Output + 1, source)) is not { } items
        )
        {
            return null;
        }

        var rows = new SelectBody(
            items,
            FromThroughWhere: null,
            SelectGrouping.None,
            HasHaving: false,
            HasTop: false,
            HasOffset: false,
            HasFrom: false,
            IsOneTable: false
        );
        return new OutputStatement(
            OutputKind.Insert,
            shape.With,
            shape.Ctes,
            rows,
            parts[^1],
            TargetIsAlias: false,
            Assignments: null
        );
    }

    // The end of "TOP ( ... ) [PERCENT]" at the token, the token when it is no TOP, or -1.
    private static int TopEnd(IReadOnlyList<TsqlToken> tokens, int at, int end)
    {
        if (at >= end || !tokens[at].Is("TOP"))
        {
            return at;
        }

        if (at + 1 >= end || !tokens[at + 1].IsSymbol("("))
        {
            return -1;
        }

        var close = SelectBody.ClosingParenthesis(tokens, at + 1, end);
        if (close < 0)
        {
            return -1;
        }

        return close + 1 < end && tokens[close + 1].Is("PERCENT") ? close + 2 : close + 1;
    }

    // "name [WITH ( ... )]" at the token, the name of one to four parts and not a CTE's, with the parts and the index
    // after it, which is a token of the statement; or null.
    private static (string[] Parts, int End)? TargetOf(
        IReadOnlyList<TsqlToken> tokens,
        int at,
        int end,
        List<CommonTableExpression> ctes
    )
    {
        if (at < 0)
        {
            return null;
        }

        var nameEnd = NameEnd(tokens, at, end);
        if (
            RowFilter.NameOf(tokens, new TokenRange(at, nameEnd)) is not { Length: <= 4 } parts
            || (parts.Length == 1 && IsCte(parts[0], ctes))
        )
        {
            return null;
        }

        if (nameEnd + 1 < end && tokens[nameEnd].Is("WITH") && tokens[nameEnd + 1].IsSymbol("("))
        {
            var close = SelectBody.ClosingParenthesis(tokens, nameEnd + 1, end);
            if (close < 0)
            {
                return null;
            }

            nameEnd = close + 1;
        }

        return nameEnd < end ? (parts, nameEnd) : null;
    }

    // The index after the dotted name at the token: each part a name, followed by "." but the last.
    private static int NameEnd(IReadOnlyList<TsqlToken> tokens, int at, int end)
    {
        while (at < end && tokens[at].IsName)
        {
            at++;
            if (at >= end || !tokens[at].IsSymbol("."))
            {
                break;
            }

            at++;
        }

        return at;
    }

    private static bool IsCte(string name, List<CommonTableExpression> ctes) =>
        ctes.Exists(cte => string.Equals(cte.Name, name, StringComparison.OrdinalIgnoreCase));

    // True when exactly one source of the FROM is named as the target, ordinally, and it is a table with an alias or
    // of a name of one part that is no CTE's.  A table of several parts without an alias is refused, since whether the
    // server takes it for the target depends on what the target's name resolves to.
    private static bool IsTheTarget(
        IReadOnlyList<TsqlToken> tokens,
        TokenRange from,
        string target,
        List<CommonTableExpression> ctes
    )
    {
        if (
            JoinChain.Read(tokens, from) is not { } sources
            || sources.Where(source => string.Equals(source.Name, target, StringComparison.Ordinal)).ToList()
                is not [{ IsDerived: false } named]
        )
        {
            return false;
        }

        if (named.OnePartName is { } onePart)
        {
            return !IsCte(onePart, ctes);
        }

        var nameEnd = NameEnd(tokens, named.Reference.Start, named.Reference.End);
        return nameEnd < named.Reference.End && !tokens[nameEnd].Is("WITH");
    }

    private static Clause? ClauseAt(TsqlToken token)
    {
        if (token.Kind != TsqlTokenKind.Word)
        {
            return null;
        }

        return token.Text.ToUpperInvariant() switch
        {
            "OUTPUT" => Clause.Output,
            "FROM" => Clause.From,
            "WHERE" => Clause.Where,
            "OPTION" => Clause.Option,
            _ => null,
        };
    }

    private static TokenRange? RangeOf(List<(Clause Kind, int Start)> clauses, Clause kind, int end)
    {
        var position = clauses.FindIndex(clause => clause.Kind == kind);
        if (position < 0)
        {
            return null;
        }

        return new TokenRange(
            clauses[position].Start,
            position + 1 < clauses.Count ? clauses[position + 1].Start : end
        );
    }

    private static bool IsSourceWord(TsqlToken token) =>
        token.Is("VALUES") || token.Is("SELECT") || token.Is("DEFAULT") || token.Is("EXEC") || token.Is("EXECUTE");

    // True when a word that starts a statement stands at the statement's level other than its first word and the one
    // at the index, an UPDATE's SET or the word that starts an INSERT's source, or the first word again for a DELETE:
    // a batch of several.
    private static bool HasOtherStatementWord(IReadOnlyList<TsqlToken> tokens, Shape shape, int allowed) =>
        shape.Level.Exists(index =>
            index != shape.Statement.Start
            && index != allowed
            && tokens[index].Kind == TsqlTokenKind.Word
            && SelectBody.StatementWords.Contains(tokens[index].Text)
        );

    // The OUTPUT items, or null when one is empty or the list holds an INTO, which sends the rows to a table, or a
    // FROM, WHERE or OPTION at its level.
    private static List<SelectItem>? ItemsOf(IReadOnlyList<TsqlToken> tokens, TokenRange list)
    {
        if (
            list.IsEmpty
            || SelectBody
                .AtLevel(tokens, list)
                .Any(index => tokens[index].Is("INTO") || ClauseAt(tokens[index]) is not null)
        )
        {
            return null;
        }

        return SelectBody.SplitItems(tokens, list);
    }

    // The assignments of the SET's items, or null when one is not "c = v", "q.c = v" with q the target, or "@v = e",
    // which writes no column.
    private static List<Assignment>? AssignmentsOf(IReadOnlyList<TsqlToken> tokens, TokenRange list, string target)
    {
        var assignments = new List<Assignment>();
        var start = list.Start;
        foreach (var index in SelectBody.AtLevel(tokens, list).Append(list.End))
        {
            if (index < list.End && !tokens[index].IsSymbol(","))
            {
                continue;
            }

            var item = new TokenRange(start, index);
            var equals = SelectBody.AtLevel(tokens, item).Where(at => tokens[at].IsSymbol("=")).ToList();
            if (equals is not [var sign] || sign + 1 == item.End)
            {
                return null;
            }

            var value = new TokenRange(sign + 1, item.End);
            var written = new TokenRange(item.Start, sign);
            if (written.Count == 1 && tokens[written.Start].Kind == TsqlTokenKind.Parameter)
            {
                start = index + 1;
                continue;
            }

            var column = RowFilter.NameOf(tokens, written) switch
            {
                [var alone] => alone,
                [var qualifier, var qualified] when string.Equals(qualifier, target, StringComparison.Ordinal) =>
                    qualified,
                _ => null,
            };
            if (column is null)
            {
                return null;
            }

            assignments.Add(new Assignment(column, value));
            start = index + 1;
        }

        return assignments;
    }

    // What Read found of the statement before it reads its kind.
    private sealed record Shape(
        TokenRange With,
        List<CommonTableExpression> Ctes,
        TokenRange Statement,
        List<int> Level,
        int Output
    );
}
