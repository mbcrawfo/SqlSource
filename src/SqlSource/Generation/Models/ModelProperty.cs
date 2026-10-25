using SqlSource.Generation.Types;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Models;

/// <summary>
/// One property of a model: a parameter of the query in its input model, or a column in its output model.
/// </summary>
/// <param name="Name">The property's name, the PascalCase of the parameter's or the column's.</param>
/// <param name="Type">The property's C# type, without the <c>?</c> that <paramref name="IsNullable" /> adds.</param>
/// <param name="IsNullable">Whether the property can be null.</param>
/// <param name="Documentation">The property's documentation, as escaped XML.</param>
/// <param name="Subject">The parameter or the column as a message names it: <c>column 'id'</c>.</param>
/// <param name="IsArray">
/// Whether the column's PostgreSQL type is an array, or a domain whose base type is one.  Always false for a parameter.
/// It changes no property's C#, so <c>ModelCollector.FindDifference</c> does not compare it.
/// </param>
/// <param name="Described">
/// The parameter's type as the sidecar describes it, which a query's method binds it by; null for a column.  It
/// changes no property's C#, so <c>ModelCollector.FindDifference</c> does not compare it, and two queries that share
/// a model each bind by their own.
/// </param>
internal sealed record ModelProperty(
    string Name,
    PropertyType Type,
    bool IsNullable,
    string Documentation,
    string Subject,
    bool IsArray,
    SidecarType? Described = null
);
