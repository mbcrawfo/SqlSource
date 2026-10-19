using SqlSource.Diagnostics;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Models;

/// <summary>
/// The properties of one query's models, and what else its models are written from.  Only a query whose entry is
/// usable, whose types have no error and whose names have none has one.
/// </summary>
/// <param name="Name">The query's name.</param>
/// <param name="Index">The query's index among the queries of its file.</param>
/// <param name="NameLocation">Where the query's name is in its file.</param>
/// <param name="Summary">The text of the query's <c>-- summary:</c> markers, or null.</param>
/// <param name="InputMarker">The query's <c>-- input-model:</c> marker, or null.</param>
/// <param name="OutputMarker">The query's <c>-- output-model:</c> marker, or null.</param>
/// <param name="Input">
/// The properties of the input model, one for each parameter of the entry, in order, or null when it has none.
/// </param>
/// <param name="Output">
/// The properties of the output model, one for each column of the entry, in order, or null when its statement returns
/// no rows.  Empty for rows of no column.
/// </param>
/// <param name="Table">The table whose column list the rows are, as the entry says, or null.</param>
internal sealed record QueryModels(
    string Name,
    int Index,
    LocationInfo NameLocation,
    string? Summary,
    ModelMarker? InputMarker,
    ModelMarker? OutputMarker,
    EquatableArray<ModelProperty>? Input,
    EquatableArray<ModelProperty>? Output,
    SidecarTable? Table
);
