namespace SqlSource.Generation.Types;

/// <summary>The C# types of one query whose entry is usable, every one of which has a C# type.</summary>
/// <param name="Name">The query's name.</param>
/// <param name="Parameters">The type of each parameter of the entry, in order.</param>
/// <param name="Columns">The type of each column of the entry, in order, or null for an entry without rows.</param>
internal sealed record QueryTypes(
    string Name,
    EquatableArray<PropertyType> Parameters,
    EquatableArray<PropertyType>? Columns
);
