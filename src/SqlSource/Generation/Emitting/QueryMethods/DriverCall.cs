namespace SqlSource.Generation.Emitting.QueryMethods;

/// <summary>
/// A call of the driver that a query's method makes, which <see cref="CallSpelling" /> spells as the method's
/// synchrony asks: the member's <c>Async</c> form awaited, or the member itself.
/// </summary>
internal enum DriverCall
{
    /// <summary><c>DbConnection.Open</c>.</summary>
    Open,

    /// <summary><c>DbConnection.Close</c>.</summary>
    Close,

    /// <summary><c>DbDataSource.OpenConnection</c>.</summary>
    OpenConnection,

    /// <summary><c>ExecuteReader</c> of a command or a batch.</summary>
    ExecuteReader,

    /// <summary><c>ExecuteNonQuery</c> of a command or a batch.</summary>
    ExecuteNonQuery,

    /// <summary><c>DbCommand.ExecuteScalar</c>.</summary>
    ExecuteScalar,

    /// <summary><c>DbDataReader.Read</c>.</summary>
    Read,

    /// <summary><c>DbDataReader.NextResult</c>.</summary>
    NextResult,

    /// <summary><c>Dispose</c> of a reader or a transaction.</summary>
    Dispose,

    /// <summary><c>DbConnection.BeginTransaction</c>.</summary>
    BeginTransaction,

    /// <summary><c>DbTransaction.Commit</c>.</summary>
    Commit,

    /// <summary><c>DbTransaction.Rollback</c>.</summary>
    Rollback,
}
