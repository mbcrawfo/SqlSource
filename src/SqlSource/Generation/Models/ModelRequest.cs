using SqlSource.Settings;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Models;

/// <summary>What one type that claims a query needs of one of the query's models.</summary>
/// <param name="Namespace">
/// The model's namespace as a value, without the <c>@</c> of an escaped keyword.  Empty for the global namespace.
/// </param>
/// <param name="NamespaceText">The model's namespace as code writes it.  Empty for the global namespace.</param>
/// <param name="Name">The model's name.</param>
/// <param name="Role">Whether the model is of the query's parameters or of a row.</param>
/// <param name="Kind">The model's shape.</param>
/// <param name="IsSorted">Whether a generator parameter sorts the model's properties by name.</param>
/// <param name="IsPublic">Whether the type that asked for the model is public, which makes the model public.</param>
/// <param name="Table">The table the model is a row of, for a table model, or null.</param>
/// <param name="Properties">The model's properties, in order: already sorted when <paramref name="IsSorted" />.</param>
/// <param name="Source">The query the request comes from, and the type that asked for it.</param>
internal sealed record ModelRequest(
    string Namespace,
    string NamespaceText,
    string Name,
    ModelRole Role,
    ModelKind Kind,
    bool IsSorted,
    bool IsPublic,
    SidecarTable? Table,
    EquatableArray<ModelProperty> Properties,
    ModelSource Source
)
{
    /// <summary>The model's full name: the namespace and the name joined by a period, or the name alone.</summary>
    public string FullName => Namespace.Length == 0 ? Name : Namespace + "." + Name;
}
