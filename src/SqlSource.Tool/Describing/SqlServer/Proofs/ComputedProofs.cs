using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.SqlServer.Columns;
using SqlSource.Tool.Describing.SqlServer.Types;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The proof that a computed column of a table cannot be <c>NULL</c> in a row of that table, from its definition in
/// <c>sys.computed_columns</c> and what the server says of the table's other columns.
/// </summary>
/// <remarks>
/// <para>
/// The server judges a computed column by its expression, and calls it nullable wherever it could be, as it does an
/// expression of a query.  So the definition is read as the one item of <c>SELECT</c> with no <c>FROM</c>, and proved
/// by the forms (<see cref="ExpressionProofs" />), each operand answered from the row of <c>table</c>, the server's
/// description of <c>SELECT *</c> from the table, whose name is the operand's one name, compared ordinally.  Anything
/// else, a definition the reader does not follow, and an operand that is no column of the table, proves nothing.
/// </para>
/// <para>
/// A computed column is not null as an operand only when it is not one, since its own nullability is the server's
/// judgment of another expression; SQL Server refuses a definition that reads another computed column all the same.
/// The proof holds of a row of the table, so a query's column is proved by it only when its row is one: the caller
/// decides that (<see cref="FilteredRows.IsOfATableNoJoinMakesNull" />).
/// </para>
/// </remarks>
internal static class ComputedProofs
{
    /// <summary>True when the definition is not null in every row of the table.</summary>
    /// <param name="definition">The column's definition, as <c>sys.computed_columns</c> holds it.</param>
    /// <param name="table">The rows of <c>table</c> for the column's table, the hidden ones included.</param>
    public static bool Proves(string definition, IReadOnlyList<SqlServerColumnRow> table)
    {
        var tokens = TsqlTokens.Read("SELECT " + definition);
        if (
            SelectStatement.Read(tokens)
            is not {
                With.IsEmpty: true,
                Ctes.Count: 0,
                Branches: [
                    {
                        HasFrom: false,
                        Where: null,
                        Grouping: SelectGrouping.None,
                        HasHaving: false,
                        HasTop: false,
                        HasOffset: false,
                        Items: [{ IsStar: false } item],
                    } body,
                ],
            }
        )
        {
            return false;
        }

        var proofs = new ExpressionProofs(tokens, body, null, asksLoneLeaf: true);
        var proof = proofs.Of(item.Expression);
        var visible = SqlServerColumns.Visible(table).ToList();
        return proof.Holds([.. proofs.Operands.Select(operand => FactsOf(tokens, operand, visible))]);
    }

    // What the table's row of the operand's one name says of it, or nothing for an operand that is no such name.
    private static OperandFacts FactsOf(
        IReadOnlyList<TsqlToken> tokens,
        TokenRange operand,
        IReadOnlyList<SqlServerColumnRow> visible
    )
    {
        if (operand.Count != 1 || tokens[operand.Start] is not { IsName: true, Name: { } name })
        {
            return default;
        }

        return visible.Where(row => string.Equals(row.Name, name, StringComparison.Ordinal)).ToList() is [var row]
            ? new OperandFacts(
                !SqlServerColumns.NullableAsRecorded(row) && row.IsComputedColumn != true,
                !SqlServerTypes.MayBeClr(row),
                SqlServerColumns.IsStored(row),
                SqlServerTypes.MayBeClr(row)
            )
            : default;
    }
}
