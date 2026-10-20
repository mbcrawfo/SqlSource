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
/// and replayed.  It puts the connection's value, or a part of it, in nothing it returns and in nothing it logs.
/// The log it is given removes the whole value all the same, and each part that <see cref="SecretsOf" /> gave
/// where it stands as a token, and puts the engine before each event's name.
/// </para>
/// </remarks>
internal interface IQueryDescriber
{
    /// <summary>The dialect whose databases it describes.</summary>
    SqlDialect Dialect { get; }

    /// <summary>
    /// The parts of a connection's value that are secret, its passwords, as the describer's driver reads the value.
    /// The run removes each from what it prints and logs of the database, as it removes the whole value, and looks
    /// for each in what the describer describes.
    /// </summary>
    /// <remarks>
    /// It is asked once for a database, before its session is opened.  It gives none for a value the driver cannot
    /// read, and it never throws: an exception is <c>SQLSRC200</c>.  A password that the driver takes from
    /// elsewhere, the environment or a file, is not in the value and is not given.
    /// </remarks>
    /// <param name="connection">The connection's value.</param>
    IReadOnlyList<string> SecretsOf(string connection);

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
