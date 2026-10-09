namespace SqlSource.Snapshot;

/// <summary>The table whose column list a result is exactly.</summary>
internal sealed record SidecarTable(string? Schema, string? Table);
