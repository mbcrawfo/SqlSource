using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SqlSource.Tool.Describing.SqlServer.Columns;
using SqlSource.Tool.Describing.SqlServer.Types;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The text of <c>operands</c>, which asks the server whether each operand a proof needs is not null and whether its
/// type is no CLR type, and the reading of its answer.
/// </summary>
/// <remarks>
/// The text is the query's own characters, rearranged: its <c>WITH</c> clause, then <c>SELECT</c> and each operand
/// aliased <c>[1]</c>, <c>[2]</c>, ..., then its <c>FROM</c> to the end of its <c>WHERE</c>.  Each operand is so
/// evaluated over the rows the query's <c>FROM</c> and <c>WHERE</c> give, and the server's answer about it holds where
/// the proof reads it.  The text is sent only as <c>@tsql</c> of <c>sys.dm_exec_describe_first_result_set</c>, as the
/// query's is.  An operand asked over one side of the last <c>FULL JOIN</c> has a call of its own scope, whose text
/// has that side's pieces of the <c>FROM</c> in place of the <c>FROM</c> through the <c>WHERE</c>.
/// </remarks>
internal static class OperandsQuery
{
    /// <summary>
    /// The place of an operand that no call asks: far past the end of the answers, where no answer is, however many
    /// operands the calls before it ask.
    /// </summary>
    public const int Unasked = int.MaxValue / 2;

    // The scopes in the order of their calls.
    private static readonly OperandScope[] Order = [OperandScope.Rows, OperandScope.LeftSide, OperandScope.RightSide];

    /// <summary>
    /// The <c>operands</c> calls for the operands a branch's proofs ask, and the place of each operand's answer in the
    /// answers of these calls joined in order.
    /// </summary>
    /// <param name="sql">The query's SQL, which the tokens were read from.</param>
    /// <param name="tokens">The query's tokens.</param>
    /// <param name="with">The <c>WITH</c> clause the branch sees, empty when it has none.</param>
    /// <param name="body">The branch.</param>
    /// <param name="proofs">The proofs of the branch's items, whose operands and their scopes are asked.</param>
    /// <param name="filtered">
    /// What the branch's <c>FROM</c> and <c>WHERE</c> keep, which gives each side's rows.
    /// </param>
    /// <param name="cuts">
    /// The tokens to leave out of the <c>FROM</c> of the rows' call, as <see cref="Text" /> takes them.
    /// </param>
    public static (IReadOnlyList<ColumnProofs.OperandQuery> Queries, IReadOnlyList<int> Places) Calls(
        string sql,
        IReadOnlyList<TsqlToken> tokens,
        TokenRange with,
        SelectBody body,
        ExpressionProofs proofs,
        FilteredRows filtered,
        IReadOnlyList<int>? cuts
    ) => Calls(sql, tokens, with, body, proofs.Operands, proofs.Scopes, filtered, cuts);

    /// <summary>
    /// The <c>operands</c> calls for operands, each asked in its scope, and the place of each operand's answer in the
    /// answers of these calls joined in order.
    /// </summary>
    /// <param name="sql">The query's SQL, which the tokens were read from.</param>
    /// <param name="tokens">The query's tokens.</param>
    /// <param name="with">The <c>WITH</c> clause the branch sees, empty when it has none.</param>
    /// <param name="body">The branch.</param>
    /// <param name="operands">The operands, none empty.</param>
    /// <param name="scopes">The scope of each operand, by its index.</param>
    /// <param name="filtered">
    /// What the branch's <c>FROM</c> and <c>WHERE</c> keep, which gives each side's rows.
    /// </param>
    /// <param name="cuts">
    /// The tokens to leave out of the <c>FROM</c> of the rows' call, as <see cref="Text" /> takes them.
    /// </param>
    /// <remarks>
    /// One call is made for each scope that has operands: <see cref="OperandScope.Rows" /> first, whose text is
    /// <see cref="Text" />'s, then <see cref="OperandScope.LeftSide" />, then <see cref="OperandScope.RightSide" />,
    /// each asking its operands in the order of their indexes.  A side's text is the <c>WITH</c> clause,
    /// <c>SELECT</c> and its operands aliased as <see cref="Text" /> aliases them, then the side's pieces of the
    /// <c>FROM</c> (<see cref="FilteredRows.FromOf" />), each on a line of its own, and no <c>WHERE</c>.  A side that
    /// <see cref="FilteredRows.FromOf" /> gives no pieces for gets no call, and its operands' places are
    /// <see cref="Unasked" />.
    /// </remarks>
    public static (IReadOnlyList<ColumnProofs.OperandQuery> Queries, IReadOnlyList<int> Places) Calls(
        string sql,
        IReadOnlyList<TsqlToken> tokens,
        TokenRange with,
        SelectBody body,
        IReadOnlyList<TokenRange> operands,
        IReadOnlyList<OperandScope> scopes,
        FilteredRows filtered,
        IReadOnlyList<int>? cuts
    )
    {
        var queries = new List<ColumnProofs.OperandQuery>();
        var places = Enumerable.Repeat(Unasked, operands.Count).ToArray();
        var asked = 0;
        foreach (var scope in Order)
        {
            var indexes = Enumerable.Range(0, operands.Count).Where(index => scopes[index] == scope).ToList();
            if (indexes.Count == 0)
            {
                continue;
            }

            IReadOnlyList<TokenRange> ofScope = [.. indexes.Select(index => operands[index])];
            string text;
            if (scope == OperandScope.Rows)
            {
                text = Text(sql, tokens, with, body, ofScope, cuts);
            }
            else if (filtered.FromOf(scope) is { } from)
            {
                text = Built(sql, tokens, with, ofScope, from);
            }
            else
            {
                continue;
            }

            queries.Add(new ColumnProofs.OperandQuery(text, ofScope.Count));
            foreach (var index in indexes)
            {
                places[index] = asked++;
            }
        }

        return (queries, places);
    }

    /// <summary>The text of <c>operands</c> for a query and the operands its proofs ask.</summary>
    /// <param name="sql">The query's SQL, which the tokens were read from.</param>
    /// <param name="tokens">The query's tokens.</param>
    /// <param name="with">The query's <c>WITH</c> clause, empty when it has none.</param>
    /// <param name="body">The <c>SELECT</c> or branch whose rows the operands are read over.</param>
    /// <param name="operands">The operands, none empty, in the order they are numbered from one.</param>
    /// <param name="cuts">
    /// The tokens to leave out of its <c>FROM</c>, each the <c>LEFT</c> or <c>OUTER</c> of a join that the <c>WHERE</c>
    /// makes inner, in order.
    /// </param>
    /// <remarks>
    /// Each piece is the SQL from its first token's start to its last token's end, so a comment inside it is kept.
    /// The alias of each operand stands on a line of its own, where no line comment of the operand reaches it, and so
    /// does the text after a cut.
    /// </remarks>
    public static string Text(
        string sql,
        IReadOnlyList<TsqlToken> tokens,
        TokenRange with,
        SelectBody body,
        IReadOnlyList<TokenRange> operands,
        IReadOnlyList<int>? cuts = null
    ) => Built(sql, tokens, with, operands, body.FromThroughWhere is { } from ? [.. Uncut(from, cuts ?? [])] : []);

    // The WITH clause, SELECT and the operands aliased, then each piece of the FROM on a line of its own.
    private static string Built(
        string sql,
        IReadOnlyList<TsqlToken> tokens,
        TokenRange with,
        IReadOnlyList<TokenRange> operands,
        IReadOnlyList<TokenRange> from
    )
    {
        var text = new StringBuilder();
        if (!with.IsEmpty)
        {
            _ = text.Append(Piece(sql, tokens, with)).Append('\n');
        }

        _ = text.Append("SELECT ")
            .AppendJoin(
                ",\n",
                operands.Select(
                    (operand, index) =>
                        Piece(sql, tokens, operand)
                        + "\nAS ["
                        + (index + 1).ToString(CultureInfo.InvariantCulture)
                        + "]"
                )
            );
        if (from.Count > 0)
        {
            _ = text.Append('\n').AppendJoin('\n', from.Select(part => Piece(sql, tokens, part)));
        }

        return text.ToString();
    }

    /// <summary>
    /// What the server answered of each operand, by its index: whether it is not null, whether its type is no CLR
    /// type or may be one, and whether it is a stored column of a table; or null when the answer proves none.
    /// </summary>
    /// <param name="answer">The answer of <c>operands</c>.</param>
    /// <param name="count">The number of operands the text asked.</param>
    /// <remarks>
    /// The answer proves none when it holds an error or error rows, or its visible rows are not
    /// <paramref name="count" /> rows named <c>1</c> to <paramref name="count" /> in order.  An operand is not null
    /// only when its row says <c>is_nullable</c> false; a row that cannot say is nullable.  Its type is no CLR type
    /// only when <see cref="SqlServerTypes.MayBeClr" /> says so of its row, and may be one otherwise.  It is stored
    /// only when <see cref="SqlServerColumns.IsStored" /> says so of its row.
    /// </remarks>
    public static IReadOnlyList<OperandFacts>? AnswersOf(SqlServerColumnsAnswer answer, int count)
    {
        if (answer is not { Error: null, Errors: null or { Count: 0 }, Rows: { } rows })
        {
            return null;
        }

        var visible = SqlServerColumns.Visible(rows).ToList();
        if (visible.Count != count)
        {
            return null;
        }

        for (var index = 0; index < count; index++)
        {
            var number = (index + 1).ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(visible[index].Name, number, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return
        [
            .. visible.Select(static row => new OperandFacts(
                !SqlServerColumns.NullableAsRecorded(row),
                !SqlServerTypes.MayBeClr(row),
                SqlServerColumns.IsStored(row),
                SqlServerTypes.MayBeClr(row)
            )),
        ];
    }

    // The runs of the range between the tokens to leave out.
    private static IEnumerable<TokenRange> Uncut(TokenRange range, IReadOnlyList<int> cuts)
    {
        var start = range.Start;
        foreach (var cut in cuts)
        {
            if (cut > start)
            {
                yield return new TokenRange(start, cut);
            }

            start = cut + 1;
        }

        if (start < range.End)
        {
            yield return new TokenRange(start, range.End);
        }
    }

    private static string Piece(string sql, IReadOnlyList<TsqlToken> tokens, TokenRange range) =>
        sql[tokens[range.Start].Start..tokens[range.End - 1].End];
}
