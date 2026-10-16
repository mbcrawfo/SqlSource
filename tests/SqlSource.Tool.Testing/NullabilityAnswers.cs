using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Testing;

// What a test expects of a column's nullability: whether it can be NULL, true, false or null for unknown, and the
// source of that answer.  Each is named for the two, so one pair has one name in every class.
internal static class NullabilityAnswers
{
    public static (bool? Nullable, string? Source) NotNullCatalog { get; } =
        (false, SidecarValues.NullableSource.Catalog);

    public static (bool? Nullable, string? Source) NullableCatalog { get; } =
        (true, SidecarValues.NullableSource.Catalog);

    public static (bool? Nullable, string? Source) NullableOuterJoin { get; } =
        (true, SidecarValues.NullableSource.OuterJoin);

    public static (bool? Nullable, string? Source) NullableHeuristic { get; } =
        (true, SidecarValues.NullableSource.Heuristic);

    public static (bool? Nullable, string? Source) NotNullExpression { get; } =
        (false, SidecarValues.NullableSource.Expression);

    public static (bool? Nullable, string? Source) NullableExpression { get; } =
        (true, SidecarValues.NullableSource.Expression);

    public static (bool? Nullable, string? Source) NotNullSetOperation { get; } =
        (false, SidecarValues.NullableSource.SetOperation);

    public static (bool? Nullable, string? Source) UnknownNoOrigin { get; } =
        (null, SidecarValues.NullableSource.NoOrigin);

    public static (bool? Nullable, string? Source) UnknownView { get; } = (null, SidecarValues.NullableSource.View);

    public static (bool? Nullable, string? Source) UnknownForeignTable { get; } =
        (null, SidecarValues.NullableSource.ForeignTable);

    // The answers of the columns, in their order.
    public static (bool? Nullable, string? Source)[] Of(IEnumerable<SidecarColumn> columns) =>
        [.. columns.Select(static column => (column.Nullable, column.NullableSource))];
}
