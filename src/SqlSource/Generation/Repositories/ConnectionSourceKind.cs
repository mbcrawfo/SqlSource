namespace SqlSource.Generation.Repositories;

/// <summary>
/// How a repository's method gets its connection from a <see cref="ConnectionSource" />, which decides who owns it.
/// </summary>
internal enum ConnectionSourceKind
{
    /// <summary>
    /// A connection string, from a field, property, parameter or method: the method makes the driver's connection, and
    /// disposes it.
    /// </summary>
    ConnectionString,

    /// <summary>A <c>DbDataSource</c>: the method opens a connection from it, and disposes it.</summary>
    DataSource,

    /// <summary>
    /// A method that gives a connection: the method calls it each time, opens it if it is closed, and disposes it.
    /// </summary>
    Factory,

    /// <summary>
    /// A <c>DbConnection</c> field, property or parameter: the method opens it if closed and closes it again, and never
    /// disposes it.
    /// </summary>
    Borrowed,
}
