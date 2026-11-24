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
/// query's is.
/// </remarks>
internal static class OperandsQuery
{
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
        if (body.FromThroughWhere is { } from)
        {
            _ = text.Append('\n').AppendJoin('\n', Uncut(from, cuts ?? []).Select(part => Piece(sql, tokens, part)));
        }

        return text.ToString();
    }

    /// <summary>
    /// What the server answered of each operand, by its index: whether it is not null, whether its type is no CLR
    /// type, and whether it is a stored column of a table; or null when the answer proves none.
    /// </summary>
    /// <param name="answer">The answer of <c>operands</c>.</param>
    /// <param name="count">The number of operands the text asked.</param>
    /// <remarks>
    /// The answer proves none when it holds an error or error rows, or its visible rows are not
    /// <paramref name="count" /> rows named <c>1</c> to <paramref name="count" /> in order.  An operand is not null
    /// only when its row says <c>is_nullable</c> false; a row that cannot say is nullable.  Its type is no CLR type
    /// only when <see cref="SqlServerTypes.MayBeClr" /> says so of its row.  It is stored only when its row names a
    /// source column of a table and says it is not computed.
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
                row is { SourceType: SqlServerColumns.Table, IsComputedColumn: false, SourceColumn: not null }
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
