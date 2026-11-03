namespace SqlSource.Tool.Watching;

/// <summary>
/// What a change to a file asks of a <c>describe --watch</c> session.  The values are ordered: a larger one covers a
/// smaller, so the effect of several changes is the largest of them.
/// </summary>
internal enum WatchEffect
{
    /// <summary>Nothing: the change is not one the session reads.</summary>
    None,

    /// <summary>Describe again, from the project evaluations the session already has.</summary>
    Describe,

    /// <summary>Evaluate the projects again, then describe: what a project lists may have changed.</summary>
    Evaluate,
}
