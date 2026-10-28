using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// Sessions of the SQL Server describer for a container test, on the database fixtures unless given, opened through
// Describers.OpenAsync.
internal static class Sessions
{
    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        string connection,
        IDescribeExchange? exchange = null,
        IRunLog? log = null,
        string database = SqlServerDatabases.Database,
        CancellationToken? cancellationToken = null
    ) => new SqlServerDescriber().OpenAsync(database, connection, exchange, log, cancellationToken: cancellationToken);

    // A session that must open, as the describer's own type.
    public static async Task<SqlServerSession> OpenSessionAsync(
        string connection,
        IDescribeExchange? exchange = null,
        IRunLog? log = null
    )
    {
        var opened = await OpenAsync(connection, exchange, log);
        opened.Failure.ShouldBeNull(opened.Failure?.Descriptor.Id);
        return opened.Value.ShouldBeOfType<SqlServerSession>();
    }

    // A session that may open or not, through a capture of every call over the exchange, the live one unless the test
    // brings one.
    public static async Task<(
        Outcome<IDescribeSession, DescribeFailure> Opened,
        DescribeCapture Capture
    )> OpenCapturedAsync(string connection, IDescribeExchange? exchange = null)
    {
        var capture = new DescribeCapture(SidecarValues.Engine.SqlServer);
        var opened = await OpenAsync(connection, new CapturingExchange(exchange ?? LiveExchange.Instance, capture));
        return (opened, capture);
    }

    // Describes one query in a session of its own.
    public static async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        string connection,
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        await using var session = await OpenSessionAsync(connection, exchange);
        return await session.DescribeAsync(request, TestContext.Current.CancellationToken);
    }

    // Describes one query in a session of its own, through a capture of every call over the exchange, the live one
    // unless the test brings one.
    public static async Task<(
        Outcome<QueryDescription, DescribeFailure> Described,
        DescribeCapture Capture
    )> DescribeCapturedAsync(string connection, DescribeRequest request, IDescribeExchange? exchange = null)
    {
        var capture = new DescribeCapture(SidecarValues.Engine.SqlServer);
        var described = await DescribeAsync(
            connection,
            request,
            new CapturingExchange(exchange ?? LiveExchange.Instance, capture)
        );
        return (described, capture);
    }

    // A query that must be described, with no failure.
    public static async Task<QueryDescription> ShouldDescribeAsync(
        string connection,
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        var described = await DescribeAsync(connection, request, exchange);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }
}
