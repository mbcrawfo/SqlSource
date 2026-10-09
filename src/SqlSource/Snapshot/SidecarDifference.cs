namespace SqlSource.Snapshot;

/// <summary>The first difference between a committed entry and what the database says today.</summary>
/// <param name="Path">The leaf that differs: <c>hash</c>, <c>columns[2].type.element.internalName</c>.</param>
/// <param name="Committed">The committed value as text: its JSON, or <c>an object</c>.</param>
/// <param name="Described">The described value, the same way.</param>
internal sealed record SidecarDifference(string Path, string Committed, string Described);
