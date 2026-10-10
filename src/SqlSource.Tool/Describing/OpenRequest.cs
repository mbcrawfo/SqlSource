namespace SqlSource.Tool.Describing;

/// <summary>
/// What a describer needs to open a session.  It is the one thing that holds a connection's value.
/// </summary>
/// <param name="Database">The logical name of the database, as the plan spells it.</param>
/// <param name="Connection">The connection's value.  A describer hands it to its driver and to nothing else.</param>
/// <param name="Exchange">What every call to the engine goes through.</param>
internal sealed record OpenRequest(string Database, string Connection, IDescribeExchange Exchange)
{
    /// <summary>
    /// The request without its connection: a record prints its members, and that one is a secret.
    /// </summary>
    public override string ToString() => $"OpenRequest {{ Database = {Database} }}";
}
