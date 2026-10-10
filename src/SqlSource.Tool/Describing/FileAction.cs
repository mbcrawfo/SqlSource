namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run does with the sidecar of one file.
/// </summary>
internal enum FileAction
{
    /// <summary>
    /// Nothing, and nothing is said: the file is not ready, has no selected query that needs an entry, or needs no
    /// sidecar and has none to delete or the run has a filter.
    /// </summary>
    None,

    /// <summary>The target is what the file on the disk holds.</summary>
    Unchanged,

    /// <summary>The target is written.</summary>
    Write,

    /// <summary>The sidecar that is there is deleted: no query of the file needs an entry.</summary>
    Delete,

    /// <summary>
    /// The sidecar is left as it was, because a query that needs an entry failed or was left out.  When a query of
    /// the file was described in this run, that is <c>SQLSRC217</c>.
    /// </summary>
    HeldBack,
}
