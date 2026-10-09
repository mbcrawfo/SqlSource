namespace SqlSource.Snapshot;

/// <summary>A column of a described query's result.</summary>
/// <param name="Ordinal">The zero-based position, which is the index in the entry's list.</param>
/// <param name="Name">The name as the server returned it, an override suffix included.</param>
/// <param name="Type">The engine's type.</param>
/// <param name="Nullable">What the server and the tool's inference established; null is unknown.</param>
/// <param name="NullableSource">Which layer decided <paramref name="Nullable" />.  Provenance, kept as written.</param>
/// <param name="Origin">The base column, or null for an expression.</param>
/// <param name="Identity">Whether the base column is an identity column, or null when the engine cannot say.</param>
/// <param name="Computed">Whether the base column is computed, or null when the engine cannot say.</param>
internal sealed record SidecarColumn(
    int Ordinal,
    string Name,
    SidecarType Type,
    bool? Nullable,
    string? NullableSource,
    SidecarOrigin? Origin,
    bool? Identity,
    bool? Computed
);
