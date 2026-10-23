using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// Asks the databases of one dialect to describe queries.  <see cref="DescriberRegistry" /> holds one for a dialect.
/// </summary>
/// <remarks>
/// <para>
/// A describer never throws for what a server or a user can cause: it returns a <see cref="DescribeFailure" />.  An
/// exception from one is a bug, and <c>SQLSRC200</c>.
/// </para>
/// <para>
/// Every call it makes to an engine goes through <see cref="OpenRequest.Exchange" />, so that a run can be recorded
/// and replayed.  It puts the connection's value, or a part of it, in nothing it logs, and in nothing it returns but
/// the parts that <see cref="SecretsOf" /> gives the run so that it can remove them, and the text that
/// <see cref="RewriteOf" /> gives.  The log it is given removes the whole value all the same, and the rewrite whole,
/// and each of those parts where it stands as a token, and puts the engine before each event's name.
/// </para>
/// </remarks>
internal interface IQueryDescriber
{
    /// <summary>The dialect whose databases it describes.</summary>
    SqlDialect Dialect { get; }

    /// <summary>
    /// The parts of a connection's value that are secret, its passwords, as the describer's driver reads the value.
    /// The run removes each from what it prints and logs of the database, as it removes the whole value.  It is the
    /// one thing a describer returns that holds a part of the value, with <see cref="RewriteOf" />, and the run gives
    /// them to nothing else.
    /// </summary>
    /// <remarks>
    /// It is asked once for a database, before its session is opened.  It gives none for a value the driver cannot
    /// read, and for what a user can cause it throws nothing: a file that cannot be read for an I/O or access reason
    /// gives none.  Any other exception is a bug, and <c>SQLSRC200</c>.  A password that the driver takes from
    /// elsewhere is not in the value, and is given all the same when the describer can know it: from the host's
    /// environment, through <paramref name="getEnvironmentVariable" /> and never the process's own, and from a file
    /// that the driver would read.  The run keeps no part shorter than
    /// <see cref="ConnectionSecret.MinimumPartLength" />, and none that equals the database's name or the engine's.
    /// </remarks>
    /// <param name="connection">The connection's value.</param>
    /// <param name="getEnvironmentVariable">The host's environment, which a driver may read a password from.</param>
    IReadOnlyList<string> SecretsOf(string connection, Func<string, string?> getEnvironmentVariable);

    /// <summary>
    /// The connection's value as the driver writes it again, or null when the driver cannot read it.
    /// </summary>
    /// <remarks>
    /// A driver quotes a connection in its errors from what it has read, which is not the text that was given: its
    /// keywords are in another order and its words in another spelling.  The run removes this text whole, as it
    /// removes the value, wherever it stands.  It is asked under the same guard as <see cref="SecretsOf" />, and it
    /// throws nothing for what a user can cause: an exception is a bug, and <c>SQLSRC200</c>.
    /// </remarks>
    /// <param name="connection">The connection's value.</param>
    string? RewriteOf(string connection);

    /// <summary>Opens a session with one database.</summary>
    /// <param name="request">The database, its connection and the exchange.</param>
    /// <param name="log">
    /// Where the describer's own events go, for the session it opens too.  It is beside the request and not in
    /// it: the request holds the secret.
    /// </param>
    /// <param name="cancellationToken">Ends the run.</param>
    Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    );
}
