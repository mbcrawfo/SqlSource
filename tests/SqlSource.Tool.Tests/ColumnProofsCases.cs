using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Describing.SqlServer.Columns;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using static SqlSource.Tool.Tests.SqlServerScript;

namespace SqlSource.Tool.Tests;

// What the tests of ColumnProofs and of CteProofs share: the proofs of a describe's columns, read as the session reads
// them, and the nullability of the columns they correct.
internal static class ColumnProofsCases
{
    // The proofs of the SQL over the rows, built as the session builds them, the hidden row of browse mode 2 after
    // them.
    public static ColumnProofs Read(string sql, params SqlServerColumnRow[] visible)
    {
        IReadOnlyList<SqlServerColumnRow> rows = [.. visible, Rowstat with { ColumnOrdinal = visible.Length + 1 }];
        var built = SqlServerColumns.Build(rows, Database).Value.ShouldNotBeNull();
        return ColumnProofs.Read(sql, built, rows);
    }

    public static List<(bool? Nullable, string? Source)> Nullability(IReadOnlyList<BuiltColumn> columns) =>
        [.. columns.Select(static column => (column.Column.Nullable, column.Column.NullableSource))];
}
