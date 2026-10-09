namespace SqlSource.Snapshot;

/// <summary>The base column a result column comes from.</summary>
internal sealed record SidecarOrigin(string? Schema, string? Table, string? Column);
