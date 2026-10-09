namespace SqlSource.Snapshot;

/// <summary>A parameter of a described query.</summary>
/// <param name="Name">The bare name, without the dialect's prefix.</param>
/// <param name="Ordinal">The zero-based position, which is the index in the entry's list.</param>
/// <param name="Type">The engine's type, or null when nothing gave one.</param>
/// <param name="Nullable">What a <c>-- param:</c> marker says, or null.</param>
/// <param name="TypeSource">Where the type came from.  Provenance, kept as written.</param>
internal sealed record SidecarParameter(
    string Name,
    int Ordinal,
    SidecarType? Type,
    bool? Nullable,
    string? TypeSource
);
