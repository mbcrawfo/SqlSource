using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing;

/// <summary>
/// An open session with one database.  It is used by one thread, one query at a time, and closed when the run is
/// done with the database.
/// </summary>
internal interface IDescribeSession : IAsyncDisposable
{
    /// <summary>What the session knows of its server.</summary>
    ServerInfo Server { get; }

    /// <summary>Describes one query.  The SQL is text: a describer that needs its lexemes lexes it itself.</summary>
    Task<DescribeResult> DescribeAsync(DescribeRequest request, CancellationToken cancellationToken);
}
