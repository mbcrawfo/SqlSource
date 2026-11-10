namespace SqlSource.Generation.Repositories;

/// <summary>
/// What a <see cref="ConnectionCandidate" /> gives a repository's methods.
/// </summary>
internal enum CandidateType
{
    /// <summary>A <c>string</c>, or a method that returns one: a connection string.</summary>
    ConnectionString,

    /// <summary>A field, property or parameter of a type that derives from <c>DbDataSource</c>.</summary>
    DataSource,

    /// <summary>
    /// A field, property or parameter of a type that derives from <c>DbConnection</c>, which the methods borrow.  Only
    /// a marked member is one.
    /// </summary>
    Connection,

    /// <summary>
    /// A method that returns a type that derives from <c>DbConnection</c>, or a <c>Task</c> or <c>ValueTask</c> of
    /// one, and takes nothing or a <c>CancellationToken</c>: each call gives a new connection.
    /// </summary>
    Factory,

    /// <summary>A marked member that is none of the others.</summary>
    Unsupported,
}
