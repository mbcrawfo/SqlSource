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
/// <para>
/// The query is read only when it is one <c>SELECT</c> that <see cref="SelectStatement.Read" /> follows, whose items
/// are the visible columns one for one: as many, none a <c>*</c>, and each item's name the server's, compared
/// ordinally; or whose one item is a <c>*</c> or a <c>q.*</c>, which every visible column is.  One item that does not
/// agree proves nothing for the whole query, since the reader has misread its select list.  A column the server
/// answers not null keeps its answer and asks nothing.
/// </para>
/// <para>
/// A column whose row says <see cref="SqlServerColumnRow.PersistedNotNull" /> is proved by the catalog, with the
/// <c>nullableSource</c> <c>catalog</c>, only when its item is a column alone or the one <c>*</c>, the <c>FROM</c> is
/// one table alone (<see cref="SelectBody.IsOneTable" />) and the <c>GROUP BY</c> has no grouping set: then every row
/// of the result is a row of that table, which the constraint keeps not null.  Through an outer join the column is
/// <c>NULL</c> while the server still names it as the source, so nothing else proves it.  A <c>*</c> gets no proof of
/// a form and asks no operand: this is the one proof it can have.
/// </para>
/// </remarks>
internal sealed class ColumnProofs
{
    private readonly IReadOnlyList<BuiltColumn> _columns;

    // The proof of each column, by its place, or null for a column that has none.
    private readonly IReadOnlyList<Proof?> _proofs;

    // Whether the catalog proves each column, by its place: a PERSISTED NOT NULL column read from its one table.
    private readonly IReadOnlyList<bool> _catalog;

    private ColumnProofs(
        IReadOnlyList<BuiltColumn> columns,
        IReadOnlyList<Proof?> proofs,
        IReadOnlyList<bool> catalog,
        string? operandsSql,
        int operandCount
    )
    {
        _columns = columns;
        _proofs = proofs;
        _catalog = catalog;
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
        var none = new ColumnProofs(columns, [], [], null, 0);
        var visible = rows.Where(static row => !row.IsHidden).ToList();
        if (visible.Count != columns.Count)
        {
            return none;
        }

        var tokens = TsqlTokens.Read(sql);
        if (SelectStatement.Read(tokens) is not { } statement)
        {
            return none;
        }

        var body = statement.Body;
        if (body.Items is [{ IsStar: true }])
        {
            return new ColumnProofs(columns, [], [.. visible.Select(row => IsProvedByTheCatalog(body, row))], null, 0);
        }

        if (!AreTheColumns(body.Items, visible))
        {
            return none;
        }

        var expressions = new ExpressionProofs(tokens, body);
        var proofs = new Proof?[visible.Count];
        var catalog = new bool[visible.Count];
        for (var index = 0; index < visible.Count; index++)
        {
            var item = body.Items[index];
            if (item.IsColumn(tokens) && IsProvedByTheCatalog(body, visible[index]))
            {
                catalog[index] = true;
            }
            else if (visible[index].IsNullable != false)
            {
                proofs[index] = expressions.Of(item.Expression);
            }
        }

        var operands = expressions.Operands;
        return new ColumnProofs(
            columns,
            proofs,
            catalog,
            operands.Count == 0 ? null : OperandsQuery.Text(sql, tokens, statement, operands),
            operands.Count
        );
    }

    /// <summary>
    /// The columns, each the catalog proves <c>nullable</c> false with the <c>nullableSource</c> <c>catalog</c>, each
    /// whose proof holds <c>nullable</c> false with the <c>nullableSource</c> <c>expression</c>, and every other as it
    /// came.
    /// </summary>
    /// <param name="operandNotNull">
    /// The answer of <c>operands</c> as <see cref="OperandsQuery.NotNullOf" /> reads it, or null when it was not asked
    /// or proves nothing: the proofs of the text alone still hold.
    /// </param>
    public IReadOnlyList<BuiltColumn> Apply(IReadOnlyList<bool>? operandNotNull) =>
        [
            .. _columns.Select(
                (column, index) => SourceOf(index, operandNotNull) is { } source ? NotNull(column, source) : column
            ),
        ];

    /// <summary>
    /// The rows of <c>table</c>, each the catalog proves not nullable, as for the columns of a query whose one item is
    /// <c>*</c> of the table alone, which is what <c>table</c> describes; every other row as it came.  So the table
    /// match compares a corrected query with a corrected table.
    /// </summary>
    /// <param name="rows">The rows of <c>table</c>, the hidden ones included.</param>
    public static IReadOnlyList<SqlServerColumnRow> TableRows(IReadOnlyList<SqlServerColumnRow> rows) =>
        [.. rows.Select(static row => IsPersistedNotNull(row) ? row with { IsNullable = false } : row)];

    // True when the catalog proves the column of the row, whose item is a column alone or the one *, in the body: its
    // row says PERSISTED NOT NULL, the FROM is its one table, and no grouping set gives a total row.
    private static bool IsProvedByTheCatalog(SelectBody body, SqlServerColumnRow row) =>
        row.IsNullable != false
        && IsPersistedNotNull(row)
        && body.IsOneTable
        && body.Grouping is SelectGrouping.None or SelectGrouping.Plain;

    // The row's source is a table of the session's database whose column is PERSISTED NOT NULL.  The SQL of "columns"
    // already asks for a table, and SourceType says so again, for a capture made by hand.
    private static bool IsPersistedNotNull(SqlServerColumnRow row) =>
        row is { PersistedNotNull: true, SourceType: "U" };

    // The nullableSource that proves the column in the place not null, or null when nothing does.
    private string? SourceOf(int index, IReadOnlyList<bool>? operandNotNull)
    {
        if (index < _catalog.Count && _catalog[index])
        {
            return SidecarValues.NullableSource.Catalog;
        }

        return index < _proofs.Count && _proofs[index] is { } proof && proof.Holds(operandNotNull)
            ? SidecarValues.NullableSource.Expression
            : null;
    }

    private static BuiltColumn NotNull(BuiltColumn column, string source) =>
        column with
        {
            Column = column.Column with { Nullable = false, NullableSource = source },
        };

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
