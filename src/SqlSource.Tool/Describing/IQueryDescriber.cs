using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

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
/// and replayed.  It puts the connection's value, or a part of it, in nothing it returns.
/// </para>
/// </remarks>
internal interface IQueryDescriber
{
    /// <summary>The dialect whose databases it describes.</summary>
    SqlDialect Dialect { get; }

    /// <summary>Opens a session with one database.</summary>
    Task<OpenResult> OpenAsync(OpenRequest request, CancellationToken cancellationToken);
}
