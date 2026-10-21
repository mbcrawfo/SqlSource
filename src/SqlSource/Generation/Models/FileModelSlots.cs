namespace SqlSource.Generation.Models;

/// <summary>The models of the queries of one file, each in the slot of its query and role.</summary>
/// <param name="NormalizedPath">The path of the file, in the form <c>SqlPath.Normalize</c> gives.</param>
/// <param name="Slots">
/// Two slots for each query of the file up to the last one that has a model, in the order of the queries: the slot
/// <c>QueryIndex * 2 + (int)Role</c> holds the models of that query and role, ordered by the type that asked for
/// them, and is empty when it has none.  A slot stays with its query and role, so a model added or removed moves no
/// model of another slot.  Inside its slot, it moves the models of the types sorted after its own.
/// </param>
internal sealed record FileModelSlots(string NormalizedPath, EquatableArray<EquatableArray<Model>> Slots);
