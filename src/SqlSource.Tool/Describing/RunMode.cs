namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run of <c>describe</c> does with the queries it selected and with the sidecars on the disk.
/// </summary>
internal enum RunMode
{
    /// <summary>A selected query whose entry is current is skipped, and the sidecars are written.</summary>
    Describe,

    /// <summary><c>--force</c>: a selected query is described though its entry is current.</summary>
    Force,

    /// <summary>
    /// <c>--check</c>: every selected query is described, as under <c>--force</c>, the sidecars on the disk are
    /// compared with what was described, and no file is changed.
    /// </summary>
    Check,
}
