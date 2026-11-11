namespace SqlSource.Settings;

/// <summary>
/// How a collection method wraps its runs in a transaction, as the <c>transaction</c> generator parameter names it.
/// The members are in the order of the parameter's words, so the index of a word is the member's value.
/// </summary>
internal enum TransactionMode
{
    /// <summary><c>on</c>: the runs share one transaction, at the database's default isolation.</summary>
    On = 0,

    /// <summary><c>off</c>: the runs are not wrapped in a transaction.</summary>
    Off = 1,

    /// <summary><c>read-uncommitted</c>: the runs share one transaction at the read uncommitted level.</summary>
    ReadUncommitted = 2,

    /// <summary><c>read-committed</c>: the runs share one transaction at the read committed level.</summary>
    ReadCommitted = 3,

    /// <summary><c>repeatable-read</c>: the runs share one transaction at the repeatable read level.</summary>
    RepeatableRead = 4,

    /// <summary><c>serializable</c>: the runs share one transaction at the serializable level.</summary>
    Serializable = 5,

    /// <summary><c>snapshot</c>: the runs share one transaction at the snapshot level.</summary>
    Snapshot = 6,
}
