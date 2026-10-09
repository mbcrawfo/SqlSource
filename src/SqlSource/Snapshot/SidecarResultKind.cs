namespace SqlSource.Snapshot;

/// <summary>Whether a statement produces a result set.</summary>
internal enum SidecarResultKind
{
    /// <summary><c>rows</c>: it does.</summary>
    Rows,

    /// <summary><c>none</c>: it does not.</summary>
    None,
}
