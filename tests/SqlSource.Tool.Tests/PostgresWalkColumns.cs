using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Columns;
using SqlSource.Tool.Describing.Postgres.Types;
using SqlSource.Tool.Describing.Postgres.Walk;
using static SqlSource.Tool.Testing.NullabilityAnswers;

namespace SqlSource.Tool.Tests;

// What the classes of the walk's tests share: the tables of a test, the columns it asks for, and those columns as the
// session builds them, with their origins, and the answers of a walk.
internal static class PostgresWalkColumns
{
    // A table with a NOT NULL column and a nullable one, and a view of the same two.
    public static readonly PostgresTableRow Table = PostgresTables.TableOf(
        16700,
        "t",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "opt", PostgresRows.Int4.Oid)
    );

    public static readonly PostgresTableRow View = Table with { Oid = 16701, Name = "v", Relkind = "v" };

    public static readonly PostgresTableRow Foreign = Table with { Oid = 16703, Name = "ft", Relkind = "f" };

    // Two more tables like the first, for the scans "a" and "b" of PostgresPlans, which name the table of the alias.
    public static readonly PostgresTableRow TableA = Table with { Oid = 16706, Name = "a" };

    public static readonly PostgresTableRow TableB = Table with { Oid = 16707, Name = "b" };

    public static readonly IReadOnlyDictionary<long, PostgresTableRow> Tables = PostgresTables.Of(
        Table,
        View,
        Foreign,
        TableA,
        TableB
    );

    // A column of the table, of the view, or of an expression when the table is null.
    public sealed record Asked(PostgresTableRow? Of, int Attnum);

    public static readonly Asked NotNull = new(Table, 1);
    public static readonly Asked Optional = new(Table, 2);
    public static readonly Asked OfView = new(View, 1);
    public static readonly Asked OfForeign = new(Foreign, 1);
    public static readonly Asked Expression = new(null, 0);

    // The columns as the session builds them for a gated query, with the heuristic's answer, and their origins.
    public static (SidecarColumn[] Columns, ColumnOrigin?[] Origins) Built(params Asked[] asked) => Built(true, asked);

    // The columns as the session builds them, for a gated query or for one that is not.
    public static (SidecarColumn[] Columns, ColumnOrigin?[] Origins) Built(bool gated, params Asked[] asked)
    {
        var columns = new SidecarColumn[asked.Length];
        var origins = new ColumnOrigin?[asked.Length];
        for (var index = 0; index < asked.Length; index++)
        {
            var row = asked[index].Of is { } table
                ? PostgresTables.ColumnOf(index, $"c{index}", PostgresRows.Int4.Oid, table, asked[index].Attnum)
                : PostgresTables.ExpressionOf(index, $"c{index}", PostgresRows.Int4.Oid);
            origins[index] = ColumnOrigin.Find(row, Tables);
            columns[index] = PostgresColumns.Build(
                row,
                origins[index],
                PostgresRows.Of(PostgresRows.Int4),
                TypeSpellings.None,
                gated
            );
        }

        return (columns, origins);
    }

    public static (bool? Nullable, string? Source)[] AnswersOf(WalkOutcome outcome) =>
        Of(outcome.Columns.Select(static walked => walked.Column));
}
