using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.SqlServer.Columns;
using SqlSource.Tool.Describing.SqlServer.Types;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The proofs that a describe's columns cannot be <c>NULL</c> where the server answers nullable: what they ask the
/// server in <c>operands</c>, and the columns as the answer corrects them.
/// </summary>
/// <remarks>
/// <para>
/// The query is read only when <see cref="SelectStatement.Read" /> follows it, as one <c>SELECT</c> or the branches of
/// its set operators, and the items of the first are the visible columns one for one: as many, none a <c>*</c>, and
/// each item's name the server's, compared ordinally; or when it is one <c>SELECT</c> whose one item is a <c>*</c> or
/// a <c>q.*</c>, which every visible column is.  Every other branch must have as many items, none a <c>*</c>, and a
/// column is proved when its item is proved in every branch, over that branch's own <c>FROM</c> and <c>WHERE</c>.  One
/// item that does not agree proves nothing for the whole query, since the reader has misread its select list.  A
/// column the server answers nullable whose row may be of a CLR type (<see cref="SqlServerTypes.MayBeClr" />) gets no
/// proof, since its expression may convert a string to that type's Null: a set operator converts every branch to the
/// type of highest precedence, so a CLR type in one branch is the column's.
/// </para>
/// <para>
/// The server answers such a conversion not null, through a derived table, a CTE, a view, a <c>VALUES</c> list and the
/// expressions over it too, so a column it answers not null whose row may be of a CLR type is nullable, with the
/// <c>nullableSource</c> <c>heuristic</c>, unless its row is a stored column (<see cref="SqlServerColumns.IsStored" />)
/// or a <c>PERSISTED NOT NULL</c> one, which no conversion made; the query is a set operator whose every branch's item
/// <c>operands</c> answers as a stored column of a CLR type (<see cref="ExpressionProofs.OfStoredClr" />), which
/// nothing converts; or its row names a view's or an inline function's column that <c>origins</c>, the query
/// described in browse mode 1, answers as a stored column, which the view or the function passes on.  Every other
/// column the server answers not null keeps its answer and asks nothing.
/// </para>
/// <para>
/// A computed column of a table is read as a row of that table only in a query of one <c>SELECT</c> whose
/// <c>GROUP BY</c> has no grouping set and none of <c>ALL</c>, by an item that is a column alone of one table alone
/// (<see cref="SelectBody.IsOneTable" />) or of a table no join makes <c>NULL</c>
/// (<see cref="FilteredRows.IsOfATableNoJoinMakesNull" />), or by the one <c>*</c> of one table alone or <c>q.*</c> of
/// such a table.  Through an outer join the column is <c>NULL</c> while the server still names it as the source, so
/// nothing else is such a row.  One whose row says <see cref="SqlServerColumnRow.PersistedNotNull" /> is then proved
/// by the catalog, with the <c>nullableSource</c> <c>catalog</c>.  Any other, of no CLR type, with its
/// <see cref="SqlServerColumnRow.ComputedDefinition" />, is proved with the <c>nullableSource</c> <c>expression</c>
/// when <see cref="ComputedProofs.Proves" /> holds of its definition over the rows of <c>table</c> for its table, which
/// <see cref="ComputedTables" /> names.  A <c>*</c> gets no proof of a form and asks no operand: these are the proofs
/// it can have.
/// </para>
/// </remarks>
internal sealed class ColumnProofs
{
    /// <summary>One call of <c>operands</c>.</summary>
    /// <param name="Sql">Its text.</param>
    /// <param name="Count">The number of operands it asks.</param>
    internal readonly record struct OperandQuery(string Sql, int Count);

    private readonly IReadOnlyList<BuiltColumn> _columns;

    // The proof of each column, by its place, or null for a column that has none.
    private readonly IReadOnlyList<Proof?> _proofs;

    // Whether the catalog proves each column, by its place: a PERSISTED NOT NULL column read from a table no join makes
    // NULL.
    private readonly IReadOnlyList<bool> _catalog;

    // Whether each column, by its place, is answered not null and may hold a CLR type's Null, so that it is nullable
    // unless its proof, which shows that nothing converts it, holds.
    private readonly IReadOnlyList<bool> _clrNull;

    // Whether each column, by its place, may hold a CLR type's Null and is read from a view or an inline function,
    // whose column "origins" may show passes a stored column on.
    private readonly IReadOnlyList<bool> _asksOrigin;

    // The computed column of a table that each column, by its place, is read as a row of and may be proved by, or null.
    private readonly IReadOnlyList<ComputedColumn?> _computed;

    // The CTE columns the proofs read.
    private readonly CteProofs _ctes;

    private ColumnProofs(
        IReadOnlyList<BuiltColumn> columns,
        IReadOnlyList<Proof?> proofs,
        IReadOnlyList<bool> catalog,
        IReadOnlyList<bool> clrNull,
        IReadOnlyList<bool> asksOrigin,
        IReadOnlyList<OperandQuery> operandQueries,
        IReadOnlyList<ComputedColumn?>? computed = null,
        CteProofs? ctes = null
    )
    {
        _columns = columns;
        _proofs = proofs;
        _catalog = catalog;
        _clrNull = clrNull;
        _asksOrigin = asksOrigin;
        _computed = computed ?? [];
        _ctes = ctes ?? CteProofs.None;
        OperandQueries = operandQueries;
        ComputedTables =
        [
            .. _computed.OfType<ComputedColumn>().Select(static column => (column.Schema, column.Table)).Distinct(),
        ];
    }

    /// <summary>
    /// The <c>operands</c> calls to make, in order, one for each branch whose proofs need the server, then those of
    /// <see cref="CteProofs" />; none when no proof does.  The answers, joined in this order, are what
    /// <see cref="Apply" /> reads.
    /// </summary>
    public IReadOnlyList<OperandQuery> OperandQueries { get; }

    /// <summary>
    /// Whether to ask <c>origins</c>, the query described in browse mode 1: a column that may hold a CLR type's Null
    /// is read from a view or an inline function, which may pass a stored column on.
    /// </summary>
    public bool AsksOrigins => _asksOrigin.Any(static asks => asks);

    /// <summary>
    /// The tables to ask <c>table</c> for, each once, in the order of the columns: each of a computed column that a
    /// column is read as a row of, whose definition its rows may prove.  <see cref="Apply" /> reads their answers.
    /// </summary>
    public IReadOnlyList<(string Schema, string Table)> ComputedTables { get; }

    /// <summary>
    /// Whether each column, by its place, is a stored column of a table in the answer of <c>origins</c>, which names
    /// the column a view's or an inline function's column passes on; or null when the answer shows none: it holds an
    /// error or error rows, or its visible rows are not the columns one for one, by place and by name.
    /// </summary>
    /// <param name="answer">The answer of <c>origins</c>.</param>
    public IReadOnlyList<bool>? OriginsOf(SqlServerColumnsAnswer answer)
    {
        if (answer is not { Error: null, Errors: null or { Count: 0 }, Rows: { } rows })
        {
            return null;
        }

        var visible = SqlServerColumns.Visible(rows).ToList();
        return
            visible.Count == _columns.Count
            && visible
                .Zip(_columns)
                .All(static pair => string.Equals(pair.First.Name, pair.Second.Column.Name, StringComparison.Ordinal))
            ? [.. visible.Select(SqlServerColumns.IsStored)]
            : null;
    }

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
        var visible = SqlServerColumns.Visible(rows).ToList();
        if (visible.Count != columns.Count)
        {
            return new ColumnProofs(columns, [], [], [], [], []);
        }

        bool[] clrNull = [.. visible.Select(MayHoldClrNull)];
        bool[] asksOrigin =
        [
            .. visible.Select(
                (row, index) =>
                    clrNull[index] && row.SourceType is SqlServerColumns.View or SqlServerColumns.InlineFunction
            ),
        ];
        var none = new ColumnProofs(columns, [], [], clrNull, asksOrigin, []);

        var tokens = TsqlTokens.Read(sql);
        if (SelectStatement.Read(tokens) is not { } statement)
        {
            return none;
        }

        var body = statement.Body;
        var isOneSelect = statement.Branches.Count == 1;
        if (isOneSelect && body.Items is [{ IsStar: true } star])
        {
            var readsATable =
                IsOfGroupsOfRows(body)
                && (
                    body.IsOneTable
                    || FilteredRows.Read(tokens, body, statement.Ctes).IsOfATableNoJoinMakesNull(star.Expression)
                );
            return new ColumnProofs(
                columns,
                [],
                [.. visible.Select(row => readsATable && IsProvedByTheCatalog(row))],
                clrNull,
                asksOrigin,
                [],
                [.. visible.Select(row => readsATable ? ComputedOf(row) : null)]
            );
        }

        if (!AreTheColumns(body.Items, visible) || !AreOfTheSameShape(statement.Branches, visible.Count))
        {
            return none;
        }

        var branches = statement
            .Branches.Select(branch =>
            {
                var filtered = FilteredRows.Read(tokens, branch, statement.Ctes);
                var asksLoneLeaf = !isOneSelect || filtered.Cuts.Count > 0;
                return (
                    Body: branch,
                    Filtered: filtered,
                    Proofs: new ExpressionProofs(tokens, branch, filtered, asksLoneLeaf)
                );
            })
            .ToList();

        var parts = new List<Proof>?[visible.Count];
        var catalog = new bool[visible.Count];
        var computed = new ComputedColumn?[visible.Count];
        for (var index = 0; index < visible.Count; index++)
        {
            var readsATable =
                isOneSelect
                && IsOfGroupsOfRows(body)
                && body.Items[index].IsColumn(tokens)
                && (body.IsOneTable || branches[0].Filtered.IsOfATableNoJoinMakesNull(body.Items[index].Expression));
            if (readsATable && IsProvedByTheCatalog(visible[index]))
            {
                catalog[index] = true;
                continue;
            }

            if (readsATable)
            {
                computed[index] = ComputedOf(visible[index]);
            }

            if (SqlServerColumns.NullableAsRecorded(visible[index]) && !SqlServerTypes.MayBeClr(visible[index]))
            {
                parts[index] = [.. branches.Select(branch => branch.Proofs.Of(branch.Body.Items[index].Expression))];
            }
            else if (clrNull[index] && !isOneSelect)
            {
                parts[index] =
                [
                    .. branches.Select(branch => branch.Proofs.OfStoredClr(branch.Body.Items[index].Expression)),
                ];
            }
        }

        // What the server answers of a branch whose text cuts a LEFT holds only when that cut does.  A proof of the
        // text alone asks nothing of that text.
        var guards = branches
            .Select(static branch =>
                branch.Filtered.Cuts.Count > 0 && branch.Proofs.Operands.Count > 0
                    ? branch.Proofs.OfInnerJoins(branch.Filtered.CutGuards)
                    : Proof.Yes
            )
            .ToList();

        // Each branch numbers its operands from 0, and the answers are one list, so a proof moves to the places of its
        // operands' answers, after the operands of the branches before its own.
        var queries = new List<OperandQuery>();
        var places = new Func<int, int>[branches.Count];
        var asked = 0;
        for (var at = 0; at < branches.Count; at++)
        {
            var (calls, of) = OperandsQuery.Calls(
                sql,
                tokens,
                statement.With,
                branches[at].Body,
                branches[at].Proofs,
                branches[at].Filtered,
                branches[at].Filtered.Cuts
            );
            var before = asked;
            places[at] = index => before + of[index];
            queries.AddRange(calls);
            asked += calls.Sum(static call => call.Count);
        }

        var proofs = parts
            .Select(of =>
                of is null
                    ? null
                    : ExpressionProofs.AllOf(
                        of.Select((part, at) => WithGuard(part, guards[at]).Renumbered(places[at]))
                    )
            )
            .ToList();
        var ctes = CteProofs.Read(sql, tokens, statement, proofs.OfType<Proof>(), asked);
        return new ColumnProofs(
            columns,
            proofs,
            catalog,
            clrNull,
            asksOrigin,
            [.. queries, .. ctes.OperandQueries],
            computed,
            ctes
        );
    }

    /// <summary>
    /// The columns, each the catalog proves <c>nullable</c> false with the <c>nullableSource</c> <c>catalog</c>, each
    /// whose proof holds <c>nullable</c> false with the <c>nullableSource</c> <c>expression</c>, each that may hold a
    /// CLR type's Null, whose proof does not hold and whose origin is not stored, <c>nullable</c> true with the
    /// <c>nullableSource</c> <c>heuristic</c>, and every other as it came.
    /// </summary>
    /// <param name="answers">
    /// The answer of <c>operands</c> as <see cref="OperandsQuery.AnswersOf" /> reads it, or null when it was not asked
    /// or proves nothing: the proofs of the text alone still hold.
    /// </param>
    /// <param name="origins">
    /// The answer of <c>origins</c> as <see cref="OriginsOf" /> reads it, or null when it was not asked or shows none.
    /// </param>
    /// <param name="tables">
    /// The rows of <c>table</c> for each table of <see cref="ComputedTables" /> that it answered with rows, the hidden
    /// ones included; a table it was not asked for, or that it answered with an error or error rows, proves nothing.
    /// </param>
    public IReadOnlyList<BuiltColumn> Apply(
        IReadOnlyList<OperandFacts>? answers,
        IReadOnlyList<bool>? origins = null,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<SqlServerColumnRow>>? tables = null
    ) => [.. _columns.Select((column, index) => Applied(column, index, answers, origins, tables))];

    /// <summary>
    /// The rows of <c>table</c>, each the catalog or its definition over these rows proves not nullable, as for the
    /// columns of a query whose one item is <c>*</c> of the table alone, which is what <c>table</c> describes, and each
    /// that may hold a CLR type's Null
    /// nullable, as for any column, unless the query's column of that origin is answered not null; every other row as
    /// it came.  So the table match compares a corrected query with a corrected table.
    /// </summary>
    /// <remarks>
    /// <c>table</c> is asked only when every column has its origin in the one table or view.  A row that may hold a
    /// CLR type's Null names a view's or a function's column, and a query's column whose row named that same column
    /// was kept not null only when <c>origins</c> showed that column passes a stored one on, which holds of the row as
    /// much.
    /// </remarks>
    /// <param name="rows">The rows of <c>table</c>, the hidden ones included.</param>
    /// <param name="columns">The query's columns as the proofs answered them.</param>
    public static IReadOnlyList<SqlServerColumnRow> TableRows(
        IReadOnlyList<SqlServerColumnRow> rows,
        IReadOnlyList<SidecarColumn> columns
    ) => [.. rows.Select(row => TableRow(row, rows, columns))];

    private static SqlServerColumnRow TableRow(
        SqlServerColumnRow row,
        IReadOnlyList<SqlServerColumnRow> rows,
        IReadOnlyList<SidecarColumn> columns
    )
    {
        if (
            IsPersistedNotNull(row)
            || (ComputedOf(row) is { } computed && ComputedProofs.Proves(computed.Definition, rows))
        )
        {
            return row with { IsNullable = false };
        }

        return MayHoldClrNull(row) && !columns.Any(column => IsKeptOf(column, row))
            ? row with
            {
                IsNullable = true,
            }
            : row;
    }

    // True when the query's column, whose origin is the row's column, is answered not null.
    private static bool IsKeptOf(SidecarColumn column, SqlServerColumnRow row) =>
        column is { Nullable: false, Origin: { } origin }
        && string.Equals(origin.Schema, row.SourceSchema, StringComparison.Ordinal)
        && string.Equals(origin.Table, row.SourceTable, StringComparison.Ordinal)
        && string.Equals(origin.Column, row.SourceColumn, StringComparison.Ordinal);

    // True when the server answers the row not null, its type may be a CLR type, and it is neither a stored column
    // nor a PERSISTED NOT NULL one: a set operator may have converted a string, or a binary, to the type's Null, and
    // the server answers that not null wherever the column is read.  A hidden row is never a column.
    private static bool MayHoldClrNull(SqlServerColumnRow row) =>
        !row.IsHidden
        && !SqlServerColumns.NullableAsRecorded(row)
        && SqlServerTypes.MayBeClr(row)
        && !SqlServerColumns.IsStored(row)
        && !IsPersistedNotNull(row);

    // True when the catalog proves the column of the row, read as a row of its table: its row says PERSISTED NOT NULL.
    private static bool IsProvedByTheCatalog(SqlServerColumnRow row) =>
        SqlServerColumns.NullableAsRecorded(row) && IsPersistedNotNull(row);

    // True when each row of the body is a group of its rows, or one of them: no grouping set gives a total's row, and
    // GROUP BY ALL no group of rows its WHERE left out.
    private static bool IsOfGroupsOfRows(SelectBody body) =>
        body.Grouping is SelectGrouping.None or SelectGrouping.Plain;

    // The computed column of a table, of no CLR type, that the server answers nullable, with its definition; or null.
    private static ComputedColumn? ComputedOf(SqlServerColumnRow row) =>
        row
            is {
                IsHidden: false,
                IsComputedColumn: true,
                SourceType: SqlServerColumns.Table,
                SourceSchema: { } schema,
                SourceTable: { } table,
                ComputedDefinition: { } definition,
            }
        && SqlServerColumns.NullableAsRecorded(row)
        && !SqlServerTypes.MayBeClr(row)
            ? new ComputedColumn(schema, table, definition)
            : null;

    // The row's source is a table of the session's database whose column is PERSISTED NOT NULL.  The SQL of "columns"
    // already asks for a table, and SourceType says so again, for a capture made by hand.
    private static bool IsPersistedNotNull(SqlServerColumnRow row) =>
        row is { PersistedNotNull: true, SourceType: SqlServerColumns.Table };

    // The nullableSource that proves the column in the place not null, or null when nothing does.
    private string? SourceOf(
        int index,
        IReadOnlyList<OperandFacts>? answers,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<SqlServerColumnRow>>? tables
    )
    {
        if (index < _catalog.Count && _catalog[index])
        {
            return SidecarValues.NullableSource.Catalog;
        }

        var isProved =
            (index < _proofs.Count && _proofs[index] is { } proof && _ctes.Holds(proof, answers))
            || (
                index < _computed.Count
                && _computed[index] is { } computed
                && tables is not null
                && tables.TryGetValue((computed.Schema, computed.Table), out var rows)
                && ComputedProofs.Proves(computed.Definition, rows)
            );
        return isProved ? SidecarValues.NullableSource.Expression : null;
    }

    // The column as the proofs answer it.
    private BuiltColumn Applied(
        BuiltColumn column,
        int index,
        IReadOnlyList<OperandFacts>? answers,
        IReadOnlyList<bool>? origins,
        IReadOnlyDictionary<(string Schema, string Table), IReadOnlyList<SqlServerColumnRow>>? tables
    )
    {
        if (index < _clrNull.Count && _clrNull[index])
        {
            var isKept =
                (index < _proofs.Count && _proofs[index] is { } proof && proof.Holds(answers))
                || (_asksOrigin[index] && origins is not null && index < origins.Count && origins[index]);
            return isKept
                ? column
                : column with
                {
                    Column = column.Column with
                    {
                        Nullable = true,
                        NullableSource = SidecarValues.NullableSource.Heuristic,
                    },
                };
        }

        return SourceOf(index, answers, tables) is { } source ? NotNull(column, source) : column;
    }

    private static BuiltColumn NotNull(BuiltColumn column, string source) =>
        column with
        {
            Column = column.Column with { Nullable = false, NullableSource = source },
        };

    private static Proof WithGuard(Proof part, Proof guard) =>
        part is Proof.Always or Proof.Never ? part : ExpressionProofs.AllOf([part, guard]);

    // A computed column of a table: its schema, its table and its definition, as the catalog holds them.
    private sealed record ComputedColumn(string Schema, string Table, string Definition);

    // True when every branch has an item for each column and none is a star, whose columns are not read.  The server
    // takes a set operator's names from its first branch alone.
    private static bool AreOfTheSameShape(IReadOnlyList<SelectBody> branches, int count) =>
        branches.All(branch => branch.Items.Count == count && !branch.Items.Any(static item => item.IsStar));

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
