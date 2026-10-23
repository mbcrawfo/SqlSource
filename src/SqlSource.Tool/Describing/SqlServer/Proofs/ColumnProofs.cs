using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The proofs that a describe's columns cannot be <c>NULL</c> where the server answers nullable: what they ask the
/// server in <c>operands</c>, and the columns as the answer corrects them.
/// </summary>
/// <remarks>
/// The query is read only when it is one <c>SELECT</c> that <see cref="SelectStatement.Read" /> follows, whose items
/// are the visible columns one for one: as many, none a <c>*</c>, and each item's name the server's, compared
/// ordinally.  One item that does not agree proves nothing for the whole query, since the reader has misread its
/// select list.  A column the server answers not null keeps its answer and asks nothing.
/// </remarks>
internal sealed class ColumnProofs
{
    private readonly IReadOnlyList<BuiltColumn> _columns;

    // The proof of each column, by its place, or null for a column that has none.
    private readonly IReadOnlyList<Proof?> _proofs;

    private ColumnProofs(
        IReadOnlyList<BuiltColumn> columns,
        IReadOnlyList<Proof?> proofs,
        string? operandsSql,
        int operandCount
    )
    {
        _columns = columns;
        _proofs = proofs;
        OperandsSql = operandsSql;
        OperandCount = operandCount;
    }

    /// <summary>The text of <c>operands</c>, or null when no proof needs the server.</summary>
    public string? OperandsSql { get; }

    /// <summary>The number of operands <see cref="OperandsSql" /> asks, 0 when it is null.</summary>
    public int OperandCount { get; }

    /// <summary>The proofs of a describe's columns.</summary>
    /// <param name="sql">The query's SQL, as <c>columns</c> was asked it.</param>
    /// <param name="columns">The columns built from the rows of <c>columns</c>.</param>
    /// <param name="rows">
    /// The rows of <c>columns</c>, the hidden ones included, which the columns were built from.
    /// </param>
    public static ColumnProofs Read(
        string sql,
        IReadOnlyList<BuiltColumn> columns,
        IReadOnlyList<SqlServerColumnRow> rows
    )
    {
        var none = new ColumnProofs(columns, [], null, 0);
        var visible = rows.Where(static row => !row.IsHidden).ToList();
        if (visible.Count != columns.Count)
        {
            return none;
        }

        var tokens = TsqlTokens.Read(sql);
        if (SelectStatement.Read(tokens) is not { } statement || !AreTheColumns(statement.Body.Items, visible))
        {
            return none;
        }

        var expressions = new ExpressionProofs(tokens, statement.Body);
        var proofs = new Proof?[visible.Count];
        for (var index = 0; index < visible.Count; index++)
        {
            if (visible[index].IsNullable != false)
            {
                proofs[index] = expressions.Of(statement.Body.Items[index].Expression);
            }
        }

        var operands = expressions.Operands;
        return new ColumnProofs(
            columns,
            proofs,
            operands.Count == 0 ? null : OperandsQuery.Text(sql, tokens, statement, operands),
            operands.Count
        );
    }

    /// <summary>
    /// The columns, each whose proof holds <c>nullable</c> false with the <c>nullableSource</c> <c>expression</c>, and
    /// every other as it came.
    /// </summary>
    /// <param name="operandNotNull">
    /// The answer of <c>operands</c> as <see cref="OperandsQuery.NotNullOf" /> reads it, or null when it was not asked
    /// or proves nothing: the proofs of the text alone still hold.
    /// </param>
    public IReadOnlyList<BuiltColumn> Apply(IReadOnlyList<bool>? operandNotNull) =>
        [
            .. _columns.Select(
                (column, index) =>
                    index < _proofs.Count && _proofs[index] is { } proof && proof.Holds(operandNotNull)
                        ? column with
                        {
                            Column = column.Column with
                            {
                                Nullable = false,
                                NullableSource = SidecarValues.NullableSource.Expression,
                            },
                        }
                        : column
            ),
        ];

    // True when the items are the visible rows one for one, by place and by the server's name.
    private static bool AreTheColumns(IReadOnlyList<SelectItem> items, List<SqlServerColumnRow> visible) =>
        items.Count == visible.Count
        && items
            .Zip(visible)
            .All(static pair =>
                !pair.First.IsStar
                && pair.First.Name is { } name
                && string.Equals(name, pair.Second.Name, StringComparison.Ordinal)
            );
}
