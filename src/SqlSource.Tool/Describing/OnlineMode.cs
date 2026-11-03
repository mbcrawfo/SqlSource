namespace SqlSource.Tool.Describing;

/// <summary>
/// What the build that calls the tool says of <c>SqlSourceOnline</c>, through <c>--online</c>.
/// </summary>
internal enum OnlineMode
{
    /// <summary>The run is not part of a build: no <c>--online</c> was given.</summary>
    Off,

    /// <summary>
    /// <c>--online auto</c>: the run says it is offline, and fails nothing, when no database with a query to describe
    /// has a connection, <c>SQLSOURCE_CONNECTION</c> is not set for several (<c>SQLSRC215</c>) and no variable that
    /// two of them share is set (<c>SQLSRC214</c>).  Otherwise a missing connection is reported as in any run.
    /// </summary>
    Auto,

    /// <summary><c>--online true</c>: the build asked for the databases, so a missing connection is an error.</summary>
    True,
}
