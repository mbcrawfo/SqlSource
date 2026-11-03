namespace SqlSource.Tool.Describing;

/// <summary>
/// What the build that calls the tool says of <c>SqlSourceOnline</c>, through <c>--online</c>.
/// </summary>
internal enum OnlineMode
{
    /// <summary>The run is not part of a build: no <c>--online</c> was given.</summary>
    Off,

    /// <summary>
    /// <c>--online auto</c>: a database with no connection is left alone and the run goes on, where it would report
    /// <c>SQLSRC213</c> otherwise.
    /// </summary>
    Auto,

    /// <summary><c>--online true</c>: the build asked for the databases, so a missing connection is an error.</summary>
    True,
}
