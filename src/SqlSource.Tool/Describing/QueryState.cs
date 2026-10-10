namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run does, or did, with one query.
/// </summary>
internal enum QueryState
{
    /// <summary>The query needs no entry: its output is <c>sql</c>.</summary>
    NotNeeded,

    /// <summary>
    /// It cannot be described, or could not: a problem of the plan, a file that is not ready, a sidecar of a newer
    /// tool, no describer, no connection, or a failure of the describer.
    /// </summary>
    Failed,

    /// <summary>The run will describe it.  No query is left in this state when the databases are done.</summary>
    ToDescribe,

    /// <summary>The run described it, and <see cref="QueryWork.Entry" /> is the new entry.</summary>
    Described,

    /// <summary>Its entry is current, and <see cref="QueryWork.Entry" /> is that entry, which is kept.</summary>
    Skipped,

    /// <summary>It is not selected and has no current entry, so its file cannot be written.</summary>
    LeftOut,
}
